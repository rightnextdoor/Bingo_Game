using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DefaultExecutionOrder(-1050)]
[DisallowMultipleComponent]
public class ChatManager : MonoBehaviour
{
    #region Fields

    public static ChatManager instance;

    private OnlineServicesRoot onlineServicesRoot;
    private OnlineConnectionManager connectionManager;
    private VivoxChatService vivoxChatService;
    private IChatService chatService;
    private ChatCommandCatalog commandCatalog;
    private ChatCommandProcessor commandProcessor;
    private ChatSettingsManager chatSettingsManager;
    private PlayerProfileRegistry profileRegistry;
    private LobbyManager lobbyManager;

    private readonly Dictionary<string, ChatConversationData> conversations = new Dictionary<string, ChatConversationData>();
    private readonly HashSet<string> blockedUserIds = new HashSet<string>(StringComparer.Ordinal);

    private bool isReady;
    private bool isChatEnabled = true;
    private bool isSubscribedToConnectionManager;
    private bool isSubscribedToUserManager;
    private bool isSubscribedToChatService;
    private bool isSubscribedToChatSettingsManager;
    private bool isSubscribedToProfileRegistry;
    private bool isSubscribedToLobbyManager;
    private bool lastReportedChatAvailable;

    private Task<bool> serviceReadyTask;
    private Task<bool> sessionJoinTask;
    private Task<bool> sessionLeaveTask;

    private string activeConversationKey = string.Empty;
    private string sessionConversationKey = string.Empty;
    private string lastPrivateMessageUserId = string.Empty;
    private string lastSuggestedUserId = string.Empty;
    private string lastServiceError = string.Empty;

    public bool IsReady => isReady;
    public bool IsChatEnabled => isChatEnabled;
    public bool IsChatAvailable => isReady && isChatEnabled && chatService != null && chatService.IsReady;
    public bool HasSessionConversation => !string.IsNullOrWhiteSpace(sessionConversationKey) && conversations.ContainsKey(sessionConversationKey);
    public string LastServiceError => lastServiceError;
    public string LastPrivateMessageUserId => lastPrivateMessageUserId;
    public string LastSuggestedUserId => lastSuggestedUserId;
    public IReadOnlyDictionary<string, ChatConversationData> Conversations => conversations;
    public IReadOnlyCollection<string> BlockedUserIds => blockedUserIds;

    public ChatConversationData ActiveConversation =>
        conversations.TryGetValue(activeConversationKey, out ChatConversationData conversation) ? conversation : null;

    public ChatConversationData SessionConversation =>
        conversations.TryGetValue(sessionConversationKey, out ChatConversationData conversation) ? conversation : null;

    public event Action<bool> ChatAvailabilityChanged;
    public event Action<ChatConversationData> ConversationJoined;
    public event Action<ChatConversationReference> ConversationLeft;
    public event Action<ChatMessageData> MessageReceived;
    public event Action<ChatConversationData> ConversationMessagesChanged;
    public event Action<ChatConversationData> ActiveConversationChanged;
    public event Action<ChatConversationData> SessionParticipantsChanged;
    public event Action BlockedUsersChanged;

    #endregion

    #region Unity Methods

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        onlineServicesRoot = GetComponent<OnlineServicesRoot>();

        if (onlineServicesRoot == null || !onlineServicesRoot.IsPrimaryInstance)
        {
            enabled = false;
            return;
        }

        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        UnsubscribeFromConnectionManager();
        UnsubscribeFromUserManager();
        UnsubscribeFromChatService();
        UnsubscribeFromChatSettingsManager();
        UnsubscribeFromProfileRegistry();
        UnsubscribeFromLobbyManager();

        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Initialization

    public bool Initialize()
    {
        if (isReady)
        {
            return true;
        }

        onlineServicesRoot = OnlineServicesRoot.instance;

        if (onlineServicesRoot == null || onlineServicesRoot != GetComponent<OnlineServicesRoot>() || !onlineServicesRoot.IsPrimaryInstance)
        {
            return false;
        }

        connectionManager = OnlineConnectionManager.instance;
        vivoxChatService = GetComponent<VivoxChatService>();
        commandCatalog = GetComponent<ChatCommandCatalog>();
        commandProcessor = GetComponent<ChatCommandProcessor>();
        chatSettingsManager = GetComponent<ChatSettingsManager>();
        profileRegistry = PlayerProfileRegistry.instance;
        lobbyManager = LobbyManager.instance;

        if (connectionManager == null || !connectionManager.IsReady || vivoxChatService == null ||
            commandCatalog == null || !commandCatalog.IsReady || commandProcessor == null || !commandProcessor.IsReady ||
            chatSettingsManager == null)
        {
            return false;
        }

        chatService = vivoxChatService;

        SubscribeToConnectionManager();
        SubscribeToUserManager();
        SubscribeToChatService();
        SubscribeToChatSettingsManager();
        SubscribeToProfileRegistry();
        SubscribeToLobbyManager();
        ApplyCurrentChatSettings();

        isReady = true;
        lastReportedChatAvailable = IsChatAvailable;

        if (connectionManager.IsOnline && isChatEnabled)
        {
            StartEnsureChatReady();
        }

        if (lobbyManager != null && lobbyManager.HasEnteredLobby)
        {
            OnLobbyViewUpdated(lobbyManager.CurrentLobbyViewData);

            if (lobbyManager.RuntimeType == SessionRuntimeType.Network)
            {
                StartJoinSession(lobbyManager.CurrentLobbyId);
            }
        }

        return true;
    }

    #endregion

    #region Availability

    public async Task<bool> EnsureChatReadyAsync()
    {
        if (!isReady || !isChatEnabled || connectionManager == null || !connectionManager.IsOnline || chatService == null)
        {
            PublishChatAvailabilityIfChanged();
            return false;
        }

        ChatParticipantData localParticipant = GetCurrentLocalParticipant();

        if (localParticipant == null)
        {
            lastServiceError = "A valid player is required before chat can connect.";
            PublishChatAvailabilityIfChanged();
            return false;
        }

        chatService.UpdateLocalParticipant(localParticipant);

        if (chatService.IsReady)
        {
            lastServiceError = string.Empty;
            PublishChatAvailabilityIfChanged();
            return true;
        }

        if (serviceReadyTask != null && !serviceReadyTask.IsCompleted)
        {
            return await serviceReadyTask;
        }

        Task<bool> activeTask = EnsureChatServiceReadyInternalAsync(localParticipant);
        serviceReadyTask = activeTask;

        try
        {
            return await activeTask;
        }
        finally
        {
            if (serviceReadyTask == activeTask)
            {
                serviceReadyTask = null;
            }
        }
    }

    private async Task<bool> EnsureChatServiceReadyInternalAsync(ChatParticipantData localParticipant)
    {
        bool ready = await chatService.EnsureReadyAsync(localParticipant);
        lastServiceError = ready ? string.Empty : chatService.LastError;
        PublishChatAvailabilityIfChanged();
        return ready;
    }

    private async void StartEnsureChatReady()
    {
        if (!await EnsureChatReadyAsync())
        {
            return;
        }

        TryJoinCurrentNetworkSession();
    }

    private void PublishChatAvailabilityIfChanged()
    {
        bool currentAvailability = IsChatAvailable;

        if (lastReportedChatAvailable == currentAvailability)
        {
            return;
        }

        lastReportedChatAvailable = currentAvailability;
        ChatAvailabilityChanged?.Invoke(currentAvailability);
    }

    #endregion

    #region Session Lifecycle

    public async Task<bool> JoinSessionAsync(string lobbyId)
    {
        if (!isReady || !isChatEnabled || string.IsNullOrWhiteSpace(lobbyId))
        {
            return false;
        }

        ChatConversationReference requestedSession = new ChatConversationReference(lobbyId.Trim(), ChatConversationType.Session);

        if (HasSessionConversation && string.Equals(SessionConversation.conversationId, requestedSession.conversationId, StringComparison.Ordinal))
        {
            RefreshSessionParticipants(lobbyManager?.CurrentLobbyViewData);
            return true;
        }

        if (sessionJoinTask != null && !sessionJoinTask.IsCompleted)
        {
            return await sessionJoinTask;
        }

        Task<bool> activeTask = JoinSessionInternalAsync(requestedSession);
        sessionJoinTask = activeTask;

        try
        {
            return await activeTask;
        }
        finally
        {
            if (sessionJoinTask == activeTask)
            {
                sessionJoinTask = null;
            }
        }
    }

    private async Task<bool> JoinSessionInternalAsync(ChatConversationReference requestedSession)
    {
        if (HasSessionConversation)
        {
            ChatConversationReference currentSession = SessionConversation.Reference;

            if (!await LeaveConversationAsync(currentSession))
            {
                return false;
            }
        }

        ClearSessionBlocks();

        if (!await JoinConversationAsync(requestedSession))
        {
            return false;
        }

        sessionConversationKey = requestedSession.Key;
        SetActiveConversation(requestedSession);
        RefreshSessionParticipants(lobbyManager?.CurrentLobbyViewData);
        return true;
    }

    public async Task<bool> LeaveSessionAsync()
    {
        if (sessionLeaveTask != null && !sessionLeaveTask.IsCompleted)
        {
            return await sessionLeaveTask;
        }

        Task<bool> activeTask = LeaveSessionInternalAsync();
        sessionLeaveTask = activeTask;

        try
        {
            return await activeTask;
        }
        finally
        {
            if (sessionLeaveTask == activeTask)
            {
                sessionLeaveTask = null;
            }
        }
    }

    private async Task<bool> LeaveSessionInternalAsync()
    {
        if (!HasSessionConversation)
        {
            sessionConversationKey = string.Empty;
            lastPrivateMessageUserId = string.Empty;
            lastSuggestedUserId = string.Empty;
            ClearSessionBlocks();
            return true;
        }

        ChatConversationReference session = SessionConversation.Reference;
        bool left = await LeaveConversationAsync(session);

        if (left)
        {
            sessionConversationKey = string.Empty;
            lastPrivateMessageUserId = string.Empty;
            lastSuggestedUserId = string.Empty;
            ClearSessionBlocks();
        }

        return left;
    }

    private async void StartJoinSession(string lobbyId)
    {
        await JoinSessionAsync(lobbyId);
    }

    private async void StartLeaveSession()
    {
        await LeaveSessionAsync();
    }

    private void TryJoinCurrentNetworkSession()
    {
        lobbyManager ??= LobbyManager.instance;

        if (lobbyManager == null || !lobbyManager.HasEnteredLobby || lobbyManager.RuntimeType != SessionRuntimeType.Network)
        {
            return;
        }

        StartJoinSession(lobbyManager.CurrentLobbyId);
    }

    #endregion

    #region Conversations

    public async Task<bool> JoinConversationAsync(ChatConversationReference conversation)
    {
        if (conversation == null || !conversation.IsValid || !isChatEnabled)
        {
            return false;
        }

        if (!await EnsureChatReadyAsync())
        {
            return false;
        }

        string conversationKey = conversation.Key;

        if (conversations.ContainsKey(conversationKey))
        {
            return true;
        }

        bool joined = await chatService.JoinConversationAsync(conversation);

        if (!joined)
        {
            lastServiceError = chatService.LastError;
            return false;
        }

        ChatConversationData conversationData = new ChatConversationData(
            new ChatConversationReference(conversation.conversationId, conversation.conversationType));

        conversations[conversationKey] = conversationData;

        if (string.IsNullOrWhiteSpace(activeConversationKey))
        {
            SetActiveConversation(conversation);
        }

        ConversationJoined?.Invoke(conversationData);
        return true;
    }

    public async Task<bool> LeaveConversationAsync(ChatConversationReference conversation)
    {
        if (conversation == null || !conversation.IsValid)
        {
            return false;
        }

        string conversationKey = conversation.Key;

        if (!conversations.ContainsKey(conversationKey))
        {
            return true;
        }

        bool left = chatService == null || await chatService.LeaveConversationAsync(conversation);

        if (!left)
        {
            lastServiceError = chatService?.LastError ?? string.Empty;
            return false;
        }

        conversations.Remove(conversationKey);

        if (activeConversationKey == conversationKey)
        {
            activeConversationKey = string.Empty;
            ActiveConversationChanged?.Invoke(null);
        }

        ConversationLeft?.Invoke(new ChatConversationReference(conversation.conversationId, conversation.conversationType));
        return true;
    }

    public bool SetActiveConversation(ChatConversationReference conversation)
    {
        if (conversation == null || !conversation.IsValid ||
            !conversations.TryGetValue(conversation.Key, out ChatConversationData conversationData))
        {
            return false;
        }

        activeConversationKey = conversation.Key;
        conversationData.unreadCount = 0;
        ActiveConversationChanged?.Invoke(conversationData);
        return true;
    }

    public bool TryGetConversation(ChatConversationReference conversation, out ChatConversationData conversationData)
    {
        conversationData = null;
        return conversation != null && conversation.IsValid && conversations.TryGetValue(conversation.Key, out conversationData);
    }

    #endregion

    #region Messages

    public async Task<ChatSendResult> SubmitMessageAsync(string input)
    {
        if (!isChatEnabled)
        {
            return ChatSendResult.Failed("Chat is disabled.");
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            return ChatSendResult.Failed("The chat message is empty.");
        }

        string trimmedInput = input.Trim();

        if (trimmedInput.StartsWith("/", StringComparison.Ordinal))
        {
            ChatCommandResult commandResult = await commandProcessor.ProcessAsync(trimmedInput);

            if (commandResult.handled)
            {
                return commandResult.success ? ChatSendResult.Succeeded() : ChatSendResult.Failed(commandResult.responseMessage);
            }
        }

        ChatConversationData conversation = ActiveConversation ?? SessionConversation;

        if (conversation == null)
        {
            return ChatSendResult.Failed("There is no active chat conversation.");
        }

        return await SendMessageAsync(conversation.Reference, trimmedInput);
    }

    public async Task<ChatSendResult> SendMessageAsync(ChatConversationReference conversation, string message)
    {
        if (!isChatEnabled)
        {
            return ChatSendResult.Failed("Chat is disabled.");
        }

        if (conversation == null || !conversation.IsValid)
        {
            return ChatSendResult.Failed("The chat conversation is not valid.");
        }

        if (!conversations.ContainsKey(conversation.Key))
        {
            return ChatSendResult.Failed("The chat conversation has not been joined.");
        }

        if (!await EnsureChatReadyAsync())
        {
            return ChatSendResult.Failed(string.IsNullOrWhiteSpace(lastServiceError) ? "Chat is not available." : lastServiceError);
        }

        ChatParticipantData localParticipant = GetCurrentLocalParticipant();

        if (localParticipant != null)
        {
            chatService.UpdateLocalParticipant(localParticipant);
        }

        ChatSendResult result = await chatService.SendMessageAsync(conversation, message);

        if (!result.success)
        {
            lastServiceError = result.failureMessage;
        }

        return result;
    }

    public async Task<ChatSendResult> SendPrivateSessionMessageAsync(string recipientUserId, string message)
    {
        if (!isChatEnabled || !HasSessionConversation)
        {
            return ChatSendResult.Failed("Session chat is not available.");
        }

        if (!TryGetSessionParticipant(recipientUserId, out ChatParticipantData recipient))
        {
            return ChatSendResult.Failed("That player is not in the current Session chat.");
        }

        ChatParticipantData localParticipant = GetCurrentLocalParticipant();

        if (localParticipant == null || string.Equals(localParticipant.userId, recipient.userId, StringComparison.Ordinal))
        {
            return ChatSendResult.Failed("A private message must target another Session player.");
        }

        if (!await EnsureChatReadyAsync())
        {
            return ChatSendResult.Failed(string.IsNullOrWhiteSpace(lastServiceError) ? "Chat is not available." : lastServiceError);
        }

        chatService.UpdateLocalParticipant(localParticipant);
        ChatSendResult result = await chatService.SendDirectMessageAsync(SessionConversation.Reference, recipient.userId, message);

        if (!result.success)
        {
            lastServiceError = result.failureMessage;
            return result;
        }

        lastPrivateMessageUserId = recipient.userId;

        ChatMessageData localMessage = new ChatMessageData(
            Guid.NewGuid().ToString("N"),
            string.Empty,
            SessionConversation.conversationId,
            ChatConversationType.Session,
            localParticipant.userId,
            localParticipant.playerName,
            localParticipant.iconId,
            string.Empty,
            message,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            true,
            true,
            recipient.userId);

        AddMessageToConversation(SessionConversation, localMessage);
        return result;
    }

    public bool AddLocalSystemMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        ChatConversationData targetConversation = ActiveConversation ?? SessionConversation;

        if (targetConversation == null)
        {
            return false;
        }

        ChatMessageData localMessage = new ChatMessageData(
            Guid.NewGuid().ToString("N"),
            string.Empty,
            targetConversation.conversationId,
            targetConversation.conversationType,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            message.Trim(),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            true,
            false,
            string.Empty,
            true);

        AddMessageToConversation(targetConversation, localMessage);
        return true;
    }

    public async Task<bool> LoadHistoryAsync(ChatConversationReference conversation, int maximumMessages)
    {
        if (conversation == null || !conversation.IsValid || maximumMessages <= 0 ||
            !conversations.TryGetValue(conversation.Key, out ChatConversationData conversationData))
        {
            return false;
        }

        if (!await EnsureChatReadyAsync())
        {
            return false;
        }

        maximumMessages = Mathf.Min(maximumMessages, GetMaximumRetainedMessages());

        IReadOnlyList<ChatMessageData> history = await chatService.GetHistoryAsync(new ChatHistoryRequest(conversation, maximumMessages));
        MergeHistory(conversationData, history);
        return true;
    }

    private void OnChatMessageReceived(ChatMessageData message)
    {
        if (message == null || IsUserBlocked(message.senderUserId))
        {
            return;
        }

        string conversationKey = ChatConversationReference.GetKey(message.conversationId, message.conversationType);

        if (!conversations.TryGetValue(conversationKey, out ChatConversationData conversation))
        {
            return;
        }

        if (message.isPrivate)
        {
            ChatParticipantData localParticipant = GetCurrentLocalParticipant();

            if (message.conversationType != ChatConversationType.Session ||
                localParticipant == null ||
                !string.Equals(message.recipientUserId, localParticipant.userId, StringComparison.Ordinal) ||
                !TryGetSessionParticipant(message.senderUserId, out _))
            {
                return;
            }

            lastPrivateMessageUserId = message.senderUserId;
        }

        AddMessageToConversation(conversation, message);
    }

    private void AddMessageToConversation(ChatConversationData conversation, ChatMessageData message)
    {
        if (conversation == null || message == null || IsUserBlocked(message.senderUserId) ||
            ContainsMessage(conversation, message.messageId))
        {
            return;
        }

        conversation.messages.Add(message);
        TrimConversationMessages(conversation);

        if (!message.isLocalSystemMessage && activeConversationKey != conversation.Key)
        {
            conversation.unreadCount++;
        }

        MessageReceived?.Invoke(message);
        ConversationMessagesChanged?.Invoke(conversation);
    }

    private void MergeHistory(ChatConversationData conversation, IReadOnlyList<ChatMessageData> history)
    {
        if (conversation == null || history == null || history.Count == 0)
        {
            return;
        }

        bool changed = false;

        for (int i = 0; i < history.Count; i++)
        {
            ChatMessageData message = history[i];

            if (message == null || IsUserBlocked(message.senderUserId) || ContainsMessage(conversation, message.messageId))
            {
                continue;
            }

            conversation.messages.Add(message);
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        conversation.messages.Sort(CompareMessagesByTimestamp);
        TrimConversationMessages(conversation);
        ConversationMessagesChanged?.Invoke(conversation);
    }

    private void TrimConversationMessages(ChatConversationData conversation)
    {
        if (conversation?.messages == null)
        {
            return;
        }

        int maximumMessages = GetMaximumRetainedMessages();
        int removeCount = conversation.messages.Count - maximumMessages;

        if (removeCount <= 0)
        {
            return;
        }

        conversation.messages.RemoveRange(0, removeCount);
    }

    private int GetMaximumRetainedMessages()
    {
        return ChatSettings.instance != null
            ? ChatSettings.instance.MaximumRetainedMessages
            : 200;
    }

    private bool ContainsMessage(ChatConversationData conversation, string messageId)
    {
        if (conversation == null || conversation.messages == null || string.IsNullOrWhiteSpace(messageId))
        {
            return false;
        }

        for (int i = 0; i < conversation.messages.Count; i++)
        {
            if (conversation.messages[i] != null && conversation.messages[i].messageId == messageId)
            {
                return true;
            }
        }

        return false;
    }

    private int CompareMessagesByTimestamp(ChatMessageData left, ChatMessageData right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return -1;
        }

        if (right == null)
        {
            return 1;
        }

        return left.timestampUnixMilliseconds.CompareTo(right.timestampUnixMilliseconds);
    }

    #endregion

    #region Session Participants

    private void RefreshSessionParticipants(LobbyViewData lobbyViewData)
    {
        ChatConversationData session = SessionConversation;

        if (session == null || lobbyViewData == null || !string.Equals(session.conversationId, lobbyViewData.lobbyId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        session.participants.Clear();

        if (lobbyViewData.players != null)
        {
            for (int i = 0; i < lobbyViewData.players.Count; i++)
            {
                LobbyPlayerViewData lobbyPlayer = lobbyViewData.players[i];

                if (lobbyPlayer == null || lobbyPlayer.userTag == UserTag.Bot || string.IsNullOrWhiteSpace(lobbyPlayer.userId))
                {
                    continue;
                }

                PlayerProfileData profile = profileRegistry?.GetProfile(lobbyPlayer.userId) ?? new PlayerProfileData(lobbyPlayer);

                if (profile.IsValid)
                {
                    session.participants.Add(new ChatParticipantData(profile));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(lastPrivateMessageUserId) && !TryGetSessionParticipant(lastPrivateMessageUserId, out _))
        {
            lastPrivateMessageUserId = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(lastSuggestedUserId) && !TryGetSessionParticipant(lastSuggestedUserId, out _))
        {
            lastSuggestedUserId = string.Empty;
        }

        SessionParticipantsChanged?.Invoke(session);
    }

    public bool TryGetSessionParticipant(string userId, out ChatParticipantData participant)
    {
        participant = null;
        ChatConversationData session = SessionConversation;

        if (session?.participants == null || string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        for (int i = 0; i < session.participants.Count; i++)
        {
            ChatParticipantData candidate = session.participants[i];

            if (candidate != null && string.Equals(candidate.userId, userId, StringComparison.Ordinal))
            {
                participant = candidate;
                return true;
            }
        }

        return false;
    }

    public string GetParticipantDisplayName(string userId)
    {
        if (!TryGetSessionParticipant(userId, out ChatParticipantData participant))
        {
            return string.Empty;
        }

        return PlayerDisplayIdentityResolver.GetDisplayName(
            new PlayerProfileData(participant.userId, participant.playerName, participant.iconId),
            BuildSessionProfileList());
    }

    public IReadOnlyList<ChatUserSuggestion> GetUserSuggestions(string input, int maximumResults = 8)
    {
        List<ChatUserSuggestion> suggestions = new List<ChatUserSuggestion>();

        if (!TryGetSuggestionCommandAndQuery(input, out ChatCommandDefinition command, out string query) ||
            command == null || !command.targetsSessionUser || SessionConversation?.participants == null)
        {
            return suggestions;
        }

        if (IsResolvedTargetInput(query))
        {
            return suggestions;
        }

        string normalizedQuery = NormalizeTargetQuery(query);
        List<ChatParticipantData> matches = new List<ChatParticipantData>();
        ChatParticipantData localParticipant = GetCurrentLocalParticipant();

        for (int i = 0; i < SessionConversation.participants.Count; i++)
        {
            ChatParticipantData participant = SessionConversation.participants[i];

            if (participant == null || !participant.IsValid ||
                (localParticipant != null && string.Equals(participant.userId, localParticipant.userId, StringComparison.Ordinal)))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(normalizedQuery) || ParticipantMatchesQuery(participant, normalizedQuery))
            {
                matches.Add(participant);
            }
        }

        if (string.IsNullOrWhiteSpace(normalizedQuery) &&
            (command.name == "msg" || command.name == "reply") &&
            !string.IsNullOrWhiteSpace(lastPrivateMessageUserId))
        {
            MoveParticipantToFront(matches, lastPrivateMessageUserId);
        }
        else if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            matches.Sort((left, right) => CompareSuggestionMatches(left, right, normalizedQuery));
        }

        if (!string.IsNullOrWhiteSpace(lastSuggestedUserId))
        {
            MoveParticipantToFront(matches, lastSuggestedUserId);
        }

        List<PlayerProfileData> relevantProfiles = BuildSessionProfileList();
        int resultCount = Mathf.Min(Mathf.Max(1, maximumResults), matches.Count);

        for (int i = 0; i < resultCount; i++)
        {
            ChatParticipantData participant = matches[i];
            string displayName = PlayerDisplayIdentityResolver.GetDisplayName(
                new PlayerProfileData(participant.userId, participant.playerName, participant.iconId), relevantProfiles);

            suggestions.Add(new ChatUserSuggestion(participant, displayName));
        }

        return suggestions;
    }

    public bool RememberSuggestedUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || IsCurrentLocalUser(userId) ||
            !TryGetSessionParticipant(userId, out ChatParticipantData participant) || participant == null)
        {
            return false;
        }

        lastSuggestedUserId = participant.userId;
        return true;
    }

    public bool TryResolveCommandTargetAndRemainder(string arguments, bool requireRemainder, out ChatParticipantData participant, out string remainder)
    {
        participant = null;
        remainder = string.Empty;

        if (SessionConversation?.participants == null || string.IsNullOrWhiteSpace(arguments))
        {
            return false;
        }

        string trimmedArguments = arguments.TrimStart();
        List<PlayerProfileData> relevantProfiles = BuildSessionProfileList();

        for (int i = 0; i < SessionConversation.participants.Count; i++)
        {
            ChatParticipantData candidate = SessionConversation.participants[i];

            if (candidate == null || !candidate.IsValid || IsCurrentLocalUser(candidate.userId))
            {
                continue;
            }

            string displayName = PlayerDisplayIdentityResolver.GetDisplayName(
                new PlayerProfileData(candidate.userId, candidate.playerName, candidate.iconId), relevantProfiles);

            if (!StartsWithTarget(trimmedArguments, displayName))
            {
                continue;
            }

            participant = candidate;
            remainder = trimmedArguments.Length == displayName.Length ? string.Empty : trimmedArguments.Substring(displayName.Length).TrimStart();
            return !requireRemainder || !string.IsNullOrWhiteSpace(remainder);
        }

        int firstSpaceIndex = trimmedArguments.IndexOf(' ');
        string targetToken = firstSpaceIndex < 0 ? trimmedArguments : trimmedArguments.Substring(0, firstSpaceIndex);
        string tokenRemainder = firstSpaceIndex < 0 ? string.Empty : trimmedArguments.Substring(firstSpaceIndex + 1).TrimStart();

        if (!TryResolveSessionParticipantByQuery(targetToken, out participant))
        {
            return false;
        }

        remainder = tokenRemainder;
        return !requireRemainder || !string.IsNullOrWhiteSpace(remainder);
    }

    private bool TryResolveSessionParticipantByQuery(string query, out ChatParticipantData participant)
    {
        participant = null;

        if (SessionConversation?.participants == null || string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        string normalizedQuery = NormalizeTargetQuery(query);
        ChatParticipantData singleMatch = null;

        for (int i = 0; i < SessionConversation.participants.Count; i++)
        {
            ChatParticipantData candidate = SessionConversation.participants[i];

            if (candidate == null || !candidate.IsValid || IsCurrentLocalUser(candidate.userId))
            {
                continue;
            }

            bool exactName = string.Equals(candidate.playerName, normalizedQuery, StringComparison.OrdinalIgnoreCase);
            bool fullUserId = string.Equals(candidate.userId, normalizedQuery, StringComparison.OrdinalIgnoreCase);

            if (!exactName && !fullUserId)
            {
                continue;
            }

            if (singleMatch != null)
            {
                participant = null;
                return false;
            }

            singleMatch = candidate;
        }

        participant = singleMatch;
        return participant != null;
    }

    private bool TryGetSuggestionCommandAndQuery(string input, out ChatCommandDefinition command, out string query)
    {
        command = null;
        query = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string trimmedInput = input.TrimStart();

        if (!trimmedInput.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        int commandEndIndex = trimmedInput.IndexOf(' ');
        string commandToken = commandEndIndex < 0 ? trimmedInput.Substring(1) : trimmedInput.Substring(1, commandEndIndex - 1);
        command = commandCatalog?.FindCommand(commandToken);

        if (command == null || !command.targetsSessionUser)
        {
            return false;
        }

        query = commandEndIndex < 0 ? string.Empty : trimmedInput.Substring(commandEndIndex + 1);
        return true;
    }

    private bool IsResolvedTargetInput(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || SessionConversation?.participants == null)
        {
            return false;
        }

        string trimmedQuery = query.TrimStart();
        List<PlayerProfileData> profiles = BuildSessionProfileList();

        for (int i = 0; i < SessionConversation.participants.Count; i++)
        {
            ChatParticipantData participant = SessionConversation.participants[i];

            if (participant == null || IsCurrentLocalUser(participant.userId))
            {
                continue;
            }

            string displayName = PlayerDisplayIdentityResolver.GetDisplayName(
                new PlayerProfileData(participant.userId, participant.playerName, participant.iconId), profiles);

            if (trimmedQuery.Length > displayName.Length && StartsWithTarget(trimmedQuery, displayName))
            {
                return true;
            }
        }

        return false;
    }

    private bool ParticipantMatchesQuery(ChatParticipantData participant, string query)
    {
        string playerName = participant.playerName ?? string.Empty;
        string userId = participant.userId ?? string.Empty;
        string displayName = GetParticipantDisplayName(participant.userId);

        return playerName.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
               userId.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
               displayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private int CompareSuggestionMatches(ChatParticipantData left, ChatParticipantData right, string query)
    {
        int leftRank = GetSuggestionMatchRank(left, query);
        int rightRank = GetSuggestionMatchRank(right, query);

        if (leftRank != rightRank)
        {
            return leftRank.CompareTo(rightRank);
        }

        return string.Compare(left?.playerName, right?.playerName, StringComparison.OrdinalIgnoreCase);
    }

    private int GetSuggestionMatchRank(ChatParticipantData participant, string query)
    {
        if (participant == null)
        {
            return int.MaxValue;
        }

        if (string.Equals(participant.playerName, query, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(participant.userId, query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if ((participant.playerName ?? string.Empty).StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if ((participant.userId ?? string.Empty).StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 3;
    }

    private void MoveParticipantToFront(List<ChatParticipantData> participants, string userId)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            if (participants[i] == null || !string.Equals(participants[i].userId, userId, StringComparison.Ordinal))
            {
                continue;
            }

            ChatParticipantData participant = participants[i];
            participants.RemoveAt(i);
            participants.Insert(0, participant);
            return;
        }
    }

    private List<PlayerProfileData> BuildSessionProfileList()
    {
        if (profileRegistry != null)
        {
            List<PlayerProfileData> lobbyProfiles = profileRegistry.GetLobbyProfiles();

            if (lobbyProfiles.Count > 0)
            {
                return lobbyProfiles;
            }
        }

        List<PlayerProfileData> profiles = new List<PlayerProfileData>();

        if (SessionConversation?.participants == null)
        {
            return profiles;
        }

        for (int i = 0; i < SessionConversation.participants.Count; i++)
        {
            ChatParticipantData participant = SessionConversation.participants[i];

            if (participant != null && participant.IsValid)
            {
                profiles.Add(new PlayerProfileData(participant.userId, participant.playerName, participant.iconId));
            }
        }

        return profiles;
    }

    private bool StartsWithTarget(string arguments, string displayName)
    {
        if (string.IsNullOrWhiteSpace(arguments) || string.IsNullOrWhiteSpace(displayName) ||
            !arguments.StartsWith(displayName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return arguments.Length == displayName.Length || char.IsWhiteSpace(arguments[displayName.Length]);
    }

    private string NormalizeTargetQuery(string query)
    {
        string normalized = query?.Trim() ?? string.Empty;
        return normalized.StartsWith("#", StringComparison.Ordinal) ? normalized.Substring(1) : normalized;
    }

    private bool IsCurrentLocalUser(string userId)
    {
        return !string.IsNullOrWhiteSpace(userId) && UserManager.instance != null && UserManager.instance.HasUser &&
               string.Equals(UserManager.instance.UserId, userId, StringComparison.Ordinal);
    }

    #endregion

    #region Blocking

    public bool IsUserBlocked(string userId)
    {
        return !string.IsNullOrWhiteSpace(userId) && blockedUserIds.Contains(userId.Trim());
    }

    public bool SetUserBlocked(string userId, bool blocked)
    {
        if (string.IsNullOrWhiteSpace(userId) || IsCurrentLocalUser(userId))
        {
            return false;
        }

        string normalizedUserId = userId.Trim();
        bool changed = blocked ? blockedUserIds.Add(normalizedUserId) : blockedUserIds.Remove(normalizedUserId);

        if (!changed)
        {
            return false;
        }

        if (blocked)
        {
            RemoveMessagesFromBlockedUser(normalizedUserId);
        }

        BlockedUsersChanged?.Invoke();
        return true;
    }

    public void ApplyBlockChanges(IEnumerable<string> userIdsToBlock, IEnumerable<string> userIdsToUnblock)
    {
        bool changed = false;

        if (userIdsToBlock != null)
        {
            foreach (string userId in userIdsToBlock)
            {
                if (string.IsNullOrWhiteSpace(userId) || IsCurrentLocalUser(userId))
                {
                    continue;
                }

                string normalizedUserId = userId.Trim();

                if (!blockedUserIds.Add(normalizedUserId))
                {
                    continue;
                }

                changed = true;
                RemoveMessagesFromBlockedUser(normalizedUserId);
            }
        }

        if (userIdsToUnblock != null)
        {
            foreach (string userId in userIdsToUnblock)
            {
                if (string.IsNullOrWhiteSpace(userId))
                {
                    continue;
                }

                if (blockedUserIds.Remove(userId.Trim()))
                {
                    changed = true;
                }
            }
        }

        if (changed)
        {
            BlockedUsersChanged?.Invoke();
        }
    }

    private void RemoveMessagesFromBlockedUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        foreach (KeyValuePair<string, ChatConversationData> pair in conversations)
        {
            ChatConversationData conversation = pair.Value;

            if (conversation?.messages == null || conversation.messages.Count == 0)
            {
                continue;
            }

            bool removedAny = false;

            for (int i = conversation.messages.Count - 1; i >= 0; i--)
            {
                ChatMessageData message = conversation.messages[i];

                if (message == null || !string.Equals(message.senderUserId, userId, StringComparison.Ordinal))
                {
                    continue;
                }

                conversation.messages.RemoveAt(i);
                removedAny = true;
            }

            if (removedAny)
            {
                ConversationMessagesChanged?.Invoke(conversation);
            }
        }
    }

    private void ClearSessionBlocks()
    {
        if (blockedUserIds.Count == 0)
        {
            return;
        }

        blockedUserIds.Clear();
        BlockedUsersChanged?.Invoke();
    }

    #endregion

    #region Help

    public ChatCommandResult ToggleHelp(string helpMessage)
    {
        ToolTipManager toolTipManager = ToolTipManager.instance;
        UIMessageCatalog messageCatalog = UIMessageCatalog.instance;

        if (toolTipManager == null || messageCatalog == null)
        {
            return ChatCommandResult.Failed("Chat help is not available in this scene.");
        }

        if (toolTipManager.IsShowing(UIMessageType.ChatHelp))
        {
            toolTipManager.HideToolTip();
            return ChatCommandResult.Succeeded();
        }

        UIMessageData helpMessageData = messageCatalog.GetMessage(UIMessageType.ChatHelp);

        if (helpMessageData == null)
        {
            return ChatCommandResult.Failed("Chat help message data was not found.");
        }

        toolTipManager.ShowFixedToolTip(helpMessageData, helpMessage);
        return ChatCommandResult.Succeeded();
    }

    public void CloseHelp()
    {
        if (ToolTipManager.instance != null && ToolTipManager.instance.IsShowing(UIMessageType.ChatHelp))
        {
            ToolTipManager.instance.HideToolTip();
        }
    }

    #endregion

    #region Identity

    private ChatParticipantData GetCurrentLocalParticipant()
    {
        UserManager userManager = UserManager.instance;

        if (userManager == null || !userManager.HasUser)
        {
            return null;
        }

        UserData userData = userManager.CurrentUser;

        if (userData == null || !userData.HasUser || userData.userTag == UserTag.Bot)
        {
            return null;
        }

        PlayerProfileData profile = profileRegistry?.GetProfile(userData.userId) ?? new PlayerProfileData(userData);
        return new ChatParticipantData(profile);
    }

    private void SubscribeToUserManager()
    {
        if (isSubscribedToUserManager)
        {
            return;
        }

        UserManager.UserChanged += OnUserChanged;
        isSubscribedToUserManager = true;
    }

    private void UnsubscribeFromUserManager()
    {
        if (!isSubscribedToUserManager)
        {
            return;
        }

        UserManager.UserChanged -= OnUserChanged;
        isSubscribedToUserManager = false;
    }

    private void OnUserChanged()
    {
        ChatParticipantData localParticipant = GetCurrentLocalParticipant();
        chatService?.UpdateLocalParticipant(localParticipant);

        if (connectionManager != null && connectionManager.IsOnline && isChatEnabled && localParticipant != null)
        {
            StartEnsureChatReady();
        }
    }

    private void SubscribeToProfileRegistry()
    {
        if (isSubscribedToProfileRegistry)
        {
            return;
        }

        profileRegistry ??= PlayerProfileRegistry.instance;

        if (profileRegistry == null)
        {
            return;
        }

        profileRegistry.ProfileChanged += OnPlayerProfileChanged;
        isSubscribedToProfileRegistry = true;
    }

    private void UnsubscribeFromProfileRegistry()
    {
        if (isSubscribedToProfileRegistry && profileRegistry != null)
        {
            profileRegistry.ProfileChanged -= OnPlayerProfileChanged;
        }

        isSubscribedToProfileRegistry = false;
    }

    private void OnPlayerProfileChanged(PlayerProfileData profile)
    {
        if (profile == null)
        {
            return;
        }

        ChatConversationData session = SessionConversation;

        if (session?.participants != null)
        {
            for (int i = 0; i < session.participants.Count; i++)
            {
                ChatParticipantData participant = session.participants[i];

                if (participant == null || !string.Equals(participant.userId, profile.userId, StringComparison.Ordinal))
                {
                    continue;
                }

                participant.playerName = profile.playerName;
                participant.iconId = profile.iconId;
                SessionParticipantsChanged?.Invoke(session);
                break;
            }
        }

        if (IsCurrentLocalUser(profile.userId))
        {
            chatService?.UpdateLocalParticipant(new ChatParticipantData(profile));
        }
    }

    #endregion

    #region Lobby Events

    private void SubscribeToLobbyManager()
    {
        if (isSubscribedToLobbyManager)
        {
            return;
        }

        lobbyManager ??= LobbyManager.instance;

        if (lobbyManager == null)
        {
            return;
        }

        lobbyManager.LobbyEntryCompleted += OnLobbyEntryCompleted;
        lobbyManager.LobbyExitCompleted += OnLobbyExitCompleted;
        lobbyManager.LobbyForcedExit += OnLobbyForcedExit;
        lobbyManager.LobbyViewUpdated += OnLobbyViewUpdated;
        isSubscribedToLobbyManager = true;
    }

    private void UnsubscribeFromLobbyManager()
    {
        if (isSubscribedToLobbyManager && lobbyManager != null)
        {
            lobbyManager.LobbyEntryCompleted -= OnLobbyEntryCompleted;
            lobbyManager.LobbyExitCompleted -= OnLobbyExitCompleted;
            lobbyManager.LobbyForcedExit -= OnLobbyForcedExit;
            lobbyManager.LobbyViewUpdated -= OnLobbyViewUpdated;
        }

        isSubscribedToLobbyManager = false;
    }

    private void OnLobbyEntryCompleted(LobbyEntryResult result)
    {
        if (result == null || !result.success || lobbyManager == null || lobbyManager.RuntimeType != SessionRuntimeType.Network || !isChatEnabled)
        {
            return;
        }

        StartJoinSession(result.lobbyId);
    }

    private void OnLobbyExitCompleted(LobbyExitResult _)
    {
        StartLeaveSession();
    }

    private void OnLobbyForcedExit(LobbyExitNotification _)
    {
        StartLeaveSession();
    }

    private void OnLobbyViewUpdated(LobbyViewData lobbyViewData)
    {
        if (lobbyManager == null || lobbyManager.RuntimeType != SessionRuntimeType.Network)
        {
            return;
        }

        RefreshSessionParticipants(lobbyViewData);
    }

    #endregion

    #region Settings

    private void SubscribeToChatSettingsManager()
    {
        if (isSubscribedToChatSettingsManager || chatSettingsManager == null)
        {
            return;
        }

        chatSettingsManager.ChatSettingsChanged += OnChatSettingsChanged;
        isSubscribedToChatSettingsManager = true;
    }

    private void UnsubscribeFromChatSettingsManager()
    {
        if (isSubscribedToChatSettingsManager && chatSettingsManager != null)
        {
            chatSettingsManager.ChatSettingsChanged -= OnChatSettingsChanged;
        }

        isSubscribedToChatSettingsManager = false;
    }

    private void ApplyCurrentChatSettings()
    {
        if (chatSettingsManager == null || !chatSettingsManager.IsReady)
        {
            isChatEnabled = false;
            return;
        }

        ChatSettingsData chatSettings = chatSettingsManager.CurrentSettings;
        isChatEnabled = chatSettings == null || chatSettings.chatEnabled;
    }

    private void OnChatSettingsChanged(ChatSettingsData chatSettings)
    {
        bool wasEnabled = isChatEnabled;
        isChatEnabled = chatSettings == null || chatSettings.chatEnabled;

        if (wasEnabled == isChatEnabled)
        {
            PublishChatAvailabilityIfChanged();
            return;
        }

        if (!isChatEnabled)
        {
            StartShutdownChat();
            return;
        }

        if (connectionManager != null && connectionManager.IsOnline)
        {
            StartEnsureChatReady();
        }
    }

    #endregion

    #region Connection

    private void SubscribeToConnectionManager()
    {
        if (isSubscribedToConnectionManager || connectionManager == null)
        {
            return;
        }

        connectionManager.ConnectionStateChanged += OnOnlineConnectionStateChanged;
        isSubscribedToConnectionManager = true;
    }

    private void UnsubscribeFromConnectionManager()
    {
        if (isSubscribedToConnectionManager && connectionManager != null)
        {
            connectionManager.ConnectionStateChanged -= OnOnlineConnectionStateChanged;
        }

        isSubscribedToConnectionManager = false;
    }

    private void OnOnlineConnectionStateChanged(OnlineConnectionState state)
    {
        switch (state)
        {
            case OnlineConnectionState.Online:
                if (isChatEnabled)
                {
                    StartEnsureChatReady();
                }
                break;

            case OnlineConnectionState.Offline:
                StartShutdownChat();
                break;
        }
    }

    #endregion

    #region Chat Service

    private void SubscribeToChatService()
    {
        if (isSubscribedToChatService || chatService == null)
        {
            return;
        }

        chatService.MessageReceived += OnChatMessageReceived;
        chatService.ServiceUnavailable += OnChatServiceUnavailable;
        isSubscribedToChatService = true;
    }

    private void UnsubscribeFromChatService()
    {
        if (!isSubscribedToChatService || chatService == null)
        {
            return;
        }

        chatService.MessageReceived -= OnChatMessageReceived;
        chatService.ServiceUnavailable -= OnChatServiceUnavailable;
        isSubscribedToChatService = false;
    }

    private void OnChatServiceUnavailable(string reason)
    {
        lastServiceError = reason ?? string.Empty;
        PublishChatAvailabilityIfChanged();
    }

    #endregion

    #region Shutdown

    private async void StartShutdownChat()
    {
        await ShutdownChatAsync();
    }

    public async Task ShutdownChatAsync()
    {
        CloseHelp();

        if (chatService != null)
        {
            await chatService.ShutdownAsync();
        }

        bool hadConversations = conversations.Count > 0;

        conversations.Clear();
        activeConversationKey = string.Empty;
        sessionConversationKey = string.Empty;
        lastPrivateMessageUserId = string.Empty;
        lastSuggestedUserId = string.Empty;
        ClearSessionBlocks();

        if (hadConversations)
        {
            ActiveConversationChanged?.Invoke(null);
            SessionParticipantsChanged?.Invoke(null);
        }

        PublishChatAvailabilityIfChanged();
    }

    #endregion
}
