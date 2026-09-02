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
    private GameSessionManager gameSessionManager;

    private readonly Dictionary<string, ChatConversationData> conversations = new Dictionary<string, ChatConversationData>();
    private readonly Dictionary<string, ChatParticipantData> blockedUsers = new Dictionary<string, ChatParticipantData>(StringComparer.Ordinal);

    private const float SessionPreparationLoadTimeoutSeconds = 15f;

    private bool isReady;
    private bool isChatEnabled = true;
    private bool isSubscribedToConnectionManager;
    private bool isSubscribedToUserManager;
    private bool isSubscribedToChatService;
    private bool isSubscribedToChatSettingsManager;
    private bool isSubscribedToProfileRegistry;
    private bool isSubscribedToLobbyManager;
    private bool isSubscribedToGameSessionManager;
    private bool lastReportedChatAvailable;
    private bool isSessionPreparationResolved = true;
    private bool suppressChatSettingsChanged;

    private Task<bool> serviceReadyTask;
    private Task<bool> sessionJoinTask;
    private Task<bool> sessionLeaveTask;

    private string sessionJoinTaskLobbyId = string.Empty;
    private string activeConversationKey = string.Empty;
    private string sessionConversationKey = string.Empty;
    private string lastPrivateMessageUserId = string.Empty;
    private string lastSuggestedUserId = string.Empty;
    private string lastSuggestedCommandName = string.Empty;
    private string lastServiceError = string.Empty;
    private string pendingSessionLobbyId = string.Empty;
    private float sessionPreparationLoadDeadline;
    private int sessionPreparationVersion;
    private ChatConnectionState connectionState = ChatConnectionState.Disabled;

    public bool IsReady => isReady;
    public bool IsChatEnabled => isChatEnabled;
    public bool IsChatAvailable => isReady && isChatEnabled && chatService != null && chatService.IsReady;
    public bool HasSessionConversation => !string.IsNullOrWhiteSpace(sessionConversationKey) && conversations.ContainsKey(sessionConversationKey);
    public bool IsSessionPreparationResolved => isSessionPreparationResolved;
    public ChatConnectionState ConnectionState => connectionState;
    public string LastServiceError => lastServiceError;
    public string LastPrivateMessageUserId => lastPrivateMessageUserId;
    public string LastSuggestedUserId => lastSuggestedUserId;
    public string LastSuggestedCommandName => lastSuggestedCommandName;
    public IReadOnlyDictionary<string, ChatConversationData> Conversations => conversations;
    public IReadOnlyCollection<string> BlockedUserIds => blockedUsers.Keys;

    public ChatConversationData ActiveConversation =>
        conversations.TryGetValue(activeConversationKey, out ChatConversationData conversation) ? conversation : null;

    public ChatConversationData SessionConversation =>
        conversations.TryGetValue(sessionConversationKey, out ChatConversationData conversation) ? conversation : null;

    public event Action<bool> ChatAvailabilityChanged;
    public event Action<ChatConnectionState> ChatConnectionStateChanged;
    public event Action<ChatConversationData> ConversationJoined;
    public event Action<ChatConversationReference> ConversationLeft;
    public event Action<ChatMessageData> MessageReceived;
    public event Action<ChatConversationData> ConversationMessagesChanged;
    public event Action<ChatConversationData> ActiveConversationChanged;
    public event Action<ChatConversationData> SessionParticipantsChanged;
    public event Action BlockedUsersChanged;
    public event Action ChatHelpToggleRequested;
    public event Action ChatHelpCloseRequested;

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

    private void Start()
    {
        SubscribeToProfileRegistry();
        SubscribeToLobbyManager();
        SubscribeToGameSessionManager();
    }

    private void Update()
    {
        SubscribeToGameSessionManager();

        if (isSessionPreparationResolved || sessionPreparationLoadDeadline <= 0f)
        {
            return;
        }

        if (Time.realtimeSinceStartup < sessionPreparationLoadDeadline)
        {
            return;
        }

        sessionPreparationLoadDeadline = 0f;
        isSessionPreparationResolved = true;
    }

    private void OnDestroy()
    {
        UnsubscribeFromConnectionManager();
        UnsubscribeFromUserManager();
        UnsubscribeFromChatService();
        UnsubscribeFromChatSettingsManager();
        UnsubscribeFromProfileRegistry();
        UnsubscribeFromLobbyManager();
        UnsubscribeFromGameSessionManager();

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
        gameSessionManager = GameSessionManager.instance;

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
        SubscribeToGameSessionManager();
        ApplyCurrentChatSettings();

        isReady = true;
        lastReportedChatAvailable = IsChatAvailable;
        SetChatConnectionState(isChatEnabled ? ChatConnectionState.Connecting : ChatConnectionState.Disabled);

        if (connectionManager.IsOnline && isChatEnabled)
        {
            StartEnsureChatReady();
        }

        if (lobbyManager != null && lobbyManager.HasEnteredLobby)
        {
            OnLobbyViewUpdated(lobbyManager.CurrentLobbyViewData);

            if (lobbyManager.RuntimeType == SessionRuntimeType.Network)
            {
                BeginSessionPreparation(lobbyManager.CurrentLobbyId);
            }
        }
        else if (gameSessionManager != null &&
                 gameSessionManager.HasEnteredGame &&
                 gameSessionManager.RuntimeType == SessionRuntimeType.Network)
        {
            BeginSessionPreparation(gameSessionManager.CurrentLobbyId);
            RefreshSessionParticipants(gameSessionManager.CurrentGameSession);
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

            if (string.IsNullOrWhiteSpace(pendingSessionLobbyId) || HasMatchingSessionConversation(pendingSessionLobbyId))
            {
                SetChatConnectionState(ChatConnectionState.Ready);
            }

            PublishChatAvailabilityIfChanged();
            return true;
        }

        SetChatConnectionState(ChatConnectionState.Connecting);

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

        if (!ready)
        {
            if (isChatEnabled && connectionState != ChatConnectionState.Unavailable)
            {
                HandleChatConnectionFailure(chatService.LastError);
            }

            return false;
        }

        lastServiceError = string.Empty;

        if (isChatEnabled && (string.IsNullOrWhiteSpace(pendingSessionLobbyId) || HasMatchingSessionConversation(pendingSessionLobbyId)))
        {
            SetChatConnectionState(ChatConnectionState.Ready);
        }

        PublishChatAvailabilityIfChanged();
        return true;
    }

    private async void StartEnsureChatReady()
    {
        if (!await EnsureChatReadyAsync())
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(pendingSessionLobbyId))
        {
            StartJoinSession(pendingSessionLobbyId, sessionPreparationVersion);
            return;
        }

        TryJoinCurrentNetworkSession();
    }

    private void HandleChatConnectionFailure(string reason)
    {
        lastServiceError = string.IsNullOrWhiteSpace(reason) ? "Chat is unavailable." : reason;
        isChatEnabled = false;
        isSessionPreparationResolved = true;
        sessionPreparationLoadDeadline = 0f;
        SetChatConnectionState(ChatConnectionState.Unavailable);
        ClearChatConversationState();
        DisableSavedChatSettingAfterFailure();
        StartShutdownChat();
        PublishChatAvailabilityIfChanged();
    }

    private void DisableSavedChatSettingAfterFailure()
    {
        if (chatSettingsManager == null || !chatSettingsManager.IsReady || !chatSettingsManager.IsChatEnabled)
        {
            return;
        }

        ChatSettingsData settings = chatSettingsManager.CurrentSettings;
        settings.chatEnabled = false;

        suppressChatSettingsChanged = true;

        try
        {
            chatSettingsManager.UpdateChatSettings(settings);
        }
        finally
        {
            suppressChatSettingsChanged = false;
        }
    }

    private void SetChatConnectionState(ChatConnectionState state)
    {
        if (connectionState == state)
        {
            return;
        }

        connectionState = state;
        ChatConnectionStateChanged?.Invoke(connectionState);
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

    public void BeginSessionPreparation(string lobbyId)
    {
        string normalizedLobbyId = lobbyId?.Trim() ?? string.Empty;

        if (!isReady || string.IsNullOrWhiteSpace(normalizedLobbyId))
        {
            isSessionPreparationResolved = true;
            sessionPreparationLoadDeadline = 0f;
            return;
        }

        pendingSessionLobbyId = normalizedLobbyId;
        sessionPreparationVersion++;

        if (!isChatEnabled)
        {
            isSessionPreparationResolved = true;
            sessionPreparationLoadDeadline = 0f;

            if (connectionState != ChatConnectionState.Unavailable)
            {
                SetChatConnectionState(ChatConnectionState.Disabled);
            }

            return;
        }

        if (HasMatchingSessionConversation(normalizedLobbyId) && chatService != null && chatService.IsReady)
        {
            isSessionPreparationResolved = true;
            sessionPreparationLoadDeadline = 0f;
            SetChatConnectionState(ChatConnectionState.Ready);
            RefreshCurrentSessionParticipants(normalizedLobbyId);
            return;
        }

        isSessionPreparationResolved = false;
        sessionPreparationLoadDeadline = Time.realtimeSinceStartup + SessionPreparationLoadTimeoutSeconds;
        SetChatConnectionState(ChatConnectionState.Connecting);
        StartJoinSession(normalizedLobbyId, sessionPreparationVersion);
    }

    public async Task<bool> JoinSessionAsync(string lobbyId)
    {
        if (!isReady || !isChatEnabled || string.IsNullOrWhiteSpace(lobbyId))
        {
            return false;
        }

        string normalizedLobbyId = lobbyId.Trim();

        if (sessionJoinTask != null && !sessionJoinTask.IsCompleted)
        {
            Task<bool> activeJoinTask = sessionJoinTask;
            string activeJoinLobbyId = sessionJoinTaskLobbyId;
            bool activeResult = await activeJoinTask;

            if (string.Equals(activeJoinLobbyId, normalizedLobbyId, StringComparison.Ordinal))
            {
                return activeResult;
            }

            if (!isChatEnabled)
            {
                return false;
            }

            return await JoinSessionAsync(normalizedLobbyId);
        }

        ChatConversationReference requestedSession = new ChatConversationReference(normalizedLobbyId, ChatConversationType.Session);
        Task<bool> activeTask = JoinSessionRequestInternalAsync(requestedSession);
        sessionJoinTask = activeTask;
        sessionJoinTaskLobbyId = normalizedLobbyId;

        try
        {
            return await activeTask;
        }
        finally
        {
            if (sessionJoinTask == activeTask)
            {
                sessionJoinTask = null;
                sessionJoinTaskLobbyId = string.Empty;
            }
        }
    }

    private async Task<bool> JoinSessionRequestInternalAsync(ChatConversationReference requestedSession)
    {
        if (HasSessionConversation && string.Equals(SessionConversation.conversationId, requestedSession.conversationId, StringComparison.Ordinal))
        {
            if (!await EnsureChatReadyAsync())
            {
                return false;
            }

            if (!await chatService.JoinConversationAsync(requestedSession))
            {
                lastServiceError = chatService.LastError;
                return false;
            }

            RefreshCurrentSessionParticipants(requestedSession.conversationId);
            return true;
        }

        return await JoinSessionInternalAsync(requestedSession);
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
        RefreshCurrentSessionParticipants(requestedSession.conversationId);
        await LoadHistoryAsync(requestedSession, GetMaximumRetainedMessages());
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

    private async void StartJoinSession(string lobbyId, int preparationVersion)
    {
        bool joined = await JoinSessionAsync(lobbyId);

        if (preparationVersion != sessionPreparationVersion ||
            !string.Equals(pendingSessionLobbyId, lobbyId, StringComparison.Ordinal))
        {
            return;
        }

        isSessionPreparationResolved = true;
        sessionPreparationLoadDeadline = 0f;

        if (!isChatEnabled)
        {
            if (connectionState != ChatConnectionState.Unavailable)
            {
                SetChatConnectionState(ChatConnectionState.Disabled);
            }

            return;
        }

        if (joined)
        {
            lastServiceError = string.Empty;
            SetChatConnectionState(ChatConnectionState.Ready);
            PublishChatAvailabilityIfChanged();
            return;
        }

        HandleChatConnectionFailure(string.IsNullOrWhiteSpace(lastServiceError) ? chatService?.LastError : lastServiceError);
    }

    private async void StartLeaveSession()
    {
        await LeaveSessionAsync();
    }

    private void TryJoinCurrentNetworkSession()
    {
        lobbyManager ??= LobbyManager.instance;

        if (lobbyManager != null &&
            lobbyManager.HasEnteredLobby &&
            lobbyManager.RuntimeType == SessionRuntimeType.Network)
        {
            BeginSessionPreparation(lobbyManager.CurrentLobbyId);
            return;
        }

        gameSessionManager ??= GameSessionManager.instance;

        if (gameSessionManager != null &&
            gameSessionManager.HasEnteredGame &&
            gameSessionManager.RuntimeType == SessionRuntimeType.Network)
        {
            BeginSessionPreparation(gameSessionManager.CurrentLobbyId);
            RefreshSessionParticipants(gameSessionManager.CurrentGameSession);
        }
    }

    private bool HasMatchingSessionConversation(string lobbyId)
    {
        return HasSessionConversation &&
               !string.IsNullOrWhiteSpace(lobbyId) &&
               string.Equals(SessionConversation.conversationId, lobbyId.Trim(), StringComparison.Ordinal);
    }

    private void ClearPendingSessionPreparation()
    {
        pendingSessionLobbyId = string.Empty;
        sessionPreparationVersion++;
        isSessionPreparationResolved = true;
        sessionPreparationLoadDeadline = 0f;
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
            return ChatSendResult.Failed("That player is not in the current chat.");
        }

        ChatParticipantData localParticipant = GetCurrentLocalParticipant();

        if (localParticipant == null || string.Equals(localParticipant.userId, recipient.userId, StringComparison.Ordinal))
        {
            return ChatSendResult.Failed("A private message must target another player.");
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool InjectStressSessionMessage(ChatParticipantData sender, string message, bool isPrivate, string recipientUserId, out bool visible)
    {
        visible = false;

        if (!isReady || !HasSessionConversation || sender == null || !sender.IsValid || string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        string resolvedRecipientUserId = isPrivate ? recipientUserId?.Trim() ?? string.Empty : string.Empty;

        if (isPrivate && string.IsNullOrWhiteSpace(resolvedRecipientUserId))
        {
            return false;
        }

        string messageId = $"stress-{Guid.NewGuid():N}";
        ChatMessageData stressMessage = new ChatMessageData(
            messageId,
            string.Empty,
            SessionConversation.conversationId,
            ChatConversationType.Session,
            sender.userId,
            sender.playerName,
            sender.iconId,
            string.Empty,
            message.Trim(),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            false,
            isPrivate,
            resolvedRecipientUserId);

        OnChatMessageReceived(stressMessage);
        visible = ContainsMessage(SessionConversation, messageId);
        return true;
    }
#endif

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
        return ChatConfigSettings.instance != null
            ? ChatConfigSettings.instance.MaximumRetainedMessages
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

        ClearMissingSessionTargets();
        SessionParticipantsChanged?.Invoke(session);
    }

    private void RefreshSessionParticipants(GameSessionData gameSessionData)
    {
        ChatConversationData session = SessionConversation;

        if (session == null ||
            gameSessionData == null ||
            gameSessionData.runtimeType != SessionRuntimeType.Network ||
            !string.Equals(session.conversationId, gameSessionData.lobbyId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        session.participants.Clear();

        if (gameSessionData.players != null)
        {
            for (int i = 0; i < gameSessionData.players.Count; i++)
            {
                GamePlayerData gamePlayer = gameSessionData.players[i];

                if (gamePlayer == null ||
                    gamePlayer.userTag == UserTag.Bot ||
                    string.IsNullOrWhiteSpace(gamePlayer.userId))
                {
                    continue;
                }

                PlayerProfileData profile = profileRegistry?.GetProfile(gamePlayer.userId) ??
                                            new PlayerProfileData(
                                                gamePlayer.userId,
                                                gamePlayer.playerName,
                                                gamePlayer.iconId);

                if (profile.IsValid)
                {
                    session.participants.Add(new ChatParticipantData(profile));
                }
            }
        }

        ClearMissingSessionTargets();
        SessionParticipantsChanged?.Invoke(session);
    }

    private void RefreshCurrentSessionParticipants(string lobbyId)
    {
        string normalizedLobbyId = lobbyId?.Trim() ?? string.Empty;
        gameSessionManager ??= GameSessionManager.instance;

        GameSessionData gameSessionData = gameSessionManager?.CurrentGameSession;

        if (gameSessionData != null &&
            gameSessionData.runtimeType == SessionRuntimeType.Network &&
            string.Equals(gameSessionData.lobbyId, normalizedLobbyId, StringComparison.Ordinal))
        {
            RefreshSessionParticipants(gameSessionData);
            return;
        }

        LobbyViewData lobbyViewData = lobbyManager?.CurrentLobbyViewData;

        if (lobbyViewData != null &&
            string.Equals(lobbyViewData.lobbyId, normalizedLobbyId, StringComparison.Ordinal))
        {
            RefreshSessionParticipants(lobbyViewData);
        }
    }

    private void ClearMissingSessionTargets()
    {
        if (!string.IsNullOrWhiteSpace(lastPrivateMessageUserId) &&
            !TryGetSessionParticipant(lastPrivateMessageUserId, out _))
        {
            lastPrivateMessageUserId = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(lastSuggestedUserId) &&
            !TryGetSessionParticipant(lastSuggestedUserId, out _))
        {
            lastSuggestedUserId = string.Empty;
        }
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
            command.name == "msg" &&
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

    public bool RememberSuggestedCommand(string commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName) || commandCatalog == null || !commandCatalog.IsReady)
        {
            return false;
        }

        ChatCommandDefinition command = commandCatalog.FindCommand(commandName);

        if (command == null || !command.enabled)
        {
            return false;
        }

        lastSuggestedCommandName = command.name;
        return true;
    }

    public bool TryResolveCommandTargetAndRemainder(
        string arguments,
        bool requireRemainder,
        out ChatParticipantData participant,
        out string remainder,
        bool includeCurrentUser = false)
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

            if (candidate == null || !candidate.IsValid ||
                (!includeCurrentUser && IsCurrentLocalUser(candidate.userId)))
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

        if (!TryResolveSessionParticipantByQuery(targetToken, includeCurrentUser, out participant))
        {
            return false;
        }

        remainder = tokenRemainder;
        return !requireRemainder || !string.IsNullOrWhiteSpace(remainder);
    }

    private bool TryResolveSessionParticipantByQuery(string query, bool includeCurrentUser, out ChatParticipantData participant)
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

            if (candidate == null || !candidate.IsValid ||
                (!includeCurrentUser && IsCurrentLocalUser(candidate.userId)))
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
        if (participant == null || string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        string playerName = participant.playerName ?? string.Empty;
        string userId = participant.userId ?? string.Empty;
        string displayName = GetParticipantDisplayName(participant.userId);

        return playerName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
               userId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
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

        string playerName = participant.playerName ?? string.Empty;
        string userId = participant.userId ?? string.Empty;
        string displayName = GetParticipantDisplayName(participant.userId);

        if (string.Equals(playerName, query, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(userId, query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (playerName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (userId.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (playerName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 3;
        }

        if (userId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 4;
        }

        if (displayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 5;
        }

        return int.MaxValue;
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

    #region Friends

    public bool AddFriend(ChatParticipantData participant)
    {
        if (participant == null || !participant.IsValid || IsCurrentLocalUser(participant.userId))
        {
            return false;
        }

        Debug.Log($"[ChatFriend] {participant.playerName} ({participant.userId}) was added to friends.");
        return true;
    }

    #endregion

    #region Blocking

    public bool IsUserBlocked(string userId)
    {
        return !string.IsNullOrWhiteSpace(userId) && blockedUsers.ContainsKey(userId.Trim());
    }

    public IReadOnlyList<ChatParticipantData> GetBlockedUsersSnapshot()
    {
        List<ChatParticipantData> snapshot = new List<ChatParticipantData>(blockedUsers.Count);

        foreach (ChatParticipantData participant in blockedUsers.Values)
        {
            if (participant != null && participant.IsValid)
            {
                snapshot.Add(participant.Clone());
            }
        }

        return snapshot;
    }

    public bool TryGetBlockedUser(string userId, out ChatParticipantData participant)
    {
        participant = null;

        if (string.IsNullOrWhiteSpace(userId) ||
            !blockedUsers.TryGetValue(userId.Trim(), out ChatParticipantData blockedParticipant) ||
            blockedParticipant == null)
        {
            return false;
        }

        participant = blockedParticipant.Clone();
        return true;
    }

    public bool TryResolveBlockedUserCommandTarget(string arguments, out ChatParticipantData participant)
    {
        participant = null;

        if (string.IsNullOrWhiteSpace(arguments) || blockedUsers.Count == 0)
        {
            return false;
        }

        string query = arguments.Trim();
        string normalizedQuery = NormalizeTargetQuery(query);
        List<PlayerProfileData> profiles = BuildBlockedProfileList();
        ChatParticipantData singleMatch = null;

        foreach (ChatParticipantData candidate in blockedUsers.Values)
        {
            if (candidate == null || !candidate.IsValid)
            {
                continue;
            }

            string displayName = PlayerDisplayIdentityResolver.GetDisplayName(
                new PlayerProfileData(candidate.userId, candidate.playerName, candidate.iconId),
                profiles);

            bool matches =
                string.Equals(candidate.playerName, normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.userId, normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(displayName, query, StringComparison.OrdinalIgnoreCase);

            if (!matches)
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

        participant = singleMatch?.Clone();
        return participant != null;
    }

    public bool SetUserBlocked(ChatParticipantData participant, bool blocked)
    {
        if (participant == null || !participant.IsValid || IsCurrentLocalUser(participant.userId))
        {
            return false;
        }

        if (!blocked)
        {
            return SetUserBlocked(participant.userId, false);
        }

        string userId = participant.userId.Trim();

        if (blockedUsers.ContainsKey(userId))
        {
            return false;
        }

        blockedUsers[userId] = participant.Clone();
        RemoveMessagesFromBlockedUser(userId);
        BlockedUsersChanged?.Invoke();
        return true;
    }

    public bool SetUserBlocked(string userId, bool blocked)
    {
        if (string.IsNullOrWhiteSpace(userId) || IsCurrentLocalUser(userId))
        {
            return false;
        }

        string normalizedUserId = userId.Trim();

        if (!blocked)
        {
            if (!blockedUsers.Remove(normalizedUserId))
            {
                return false;
            }

            BlockedUsersChanged?.Invoke();
            return true;
        }

        if (blockedUsers.ContainsKey(normalizedUserId) ||
            !TryGetSessionParticipant(normalizedUserId, out ChatParticipantData participant))
        {
            return false;
        }

        blockedUsers[normalizedUserId] = participant.Clone();
        RemoveMessagesFromBlockedUser(normalizedUserId);
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

                if (blockedUsers.ContainsKey(normalizedUserId) ||
                    !TryGetSessionParticipant(normalizedUserId, out ChatParticipantData participant))
                {
                    continue;
                }

                blockedUsers[normalizedUserId] = participant.Clone();
                RemoveMessagesFromBlockedUser(normalizedUserId);
                changed = true;
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

                if (blockedUsers.Remove(userId.Trim()))
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

    private List<PlayerProfileData> BuildBlockedProfileList()
    {
        List<PlayerProfileData> profiles = new List<PlayerProfileData>(blockedUsers.Count);

        foreach (ChatParticipantData participant in blockedUsers.Values)
        {
            if (participant != null && participant.IsValid)
            {
                profiles.Add(new PlayerProfileData(participant.userId, participant.playerName, participant.iconId));
            }
        }

        return profiles;
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
        if (blockedUsers.Count == 0)
        {
            return;
        }

        blockedUsers.Clear();
        BlockedUsersChanged?.Invoke();
    }

    #endregion

    #region Help

    public ChatCommandResult RequestHelpToggle()
    {
        if (ChatHelpToggleRequested == null)
        {
            return ChatCommandResult.Failed("Chat help is not available in this scene.");
        }

        ChatHelpToggleRequested.Invoke();
        return ChatCommandResult.Succeeded();
    }

    private void RequestHelpClose()
    {
        ChatHelpCloseRequested?.Invoke();
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

                if (blockedUsers.TryGetValue(profile.userId, out ChatParticipantData blockedParticipant))
                {
                    blockedParticipant.playerName = profile.playerName;
                    blockedParticipant.iconId = profile.iconId;
                }

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

        lobbyManager.NetworkSessionTargetResolved += OnNetworkSessionTargetResolved;
        lobbyManager.LobbyEntryFailed += OnLobbyEntryFailed;
        lobbyManager.LobbyExitCompleted += OnLobbyExitCompleted;
        lobbyManager.LobbyForcedExit += OnLobbyForcedExit;
        lobbyManager.LobbyViewUpdated += OnLobbyViewUpdated;
        isSubscribedToLobbyManager = true;
    }

    private void UnsubscribeFromLobbyManager()
    {
        if (isSubscribedToLobbyManager && lobbyManager != null)
        {
            lobbyManager.NetworkSessionTargetResolved -= OnNetworkSessionTargetResolved;
            lobbyManager.LobbyEntryFailed -= OnLobbyEntryFailed;
            lobbyManager.LobbyExitCompleted -= OnLobbyExitCompleted;
            lobbyManager.LobbyForcedExit -= OnLobbyForcedExit;
            lobbyManager.LobbyViewUpdated -= OnLobbyViewUpdated;
        }

        isSubscribedToLobbyManager = false;
    }

    private void OnNetworkSessionTargetResolved(string lobbyId)
    {
        BeginSessionPreparation(lobbyId);
    }

    private void OnLobbyEntryFailed(LobbyEntryResult _)
    {
        ClearPendingSessionPreparation();
        StartLeaveSession();
    }

    private void OnLobbyExitCompleted(LobbyExitResult _)
    {
        ClearPendingSessionPreparation();
        StartLeaveSession();
    }

    private void OnLobbyForcedExit(LobbyExitNotification _)
    {
        ClearPendingSessionPreparation();
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

    #region Game Session Events

    private void SubscribeToGameSessionManager()
    {
        if (isSubscribedToGameSessionManager)
        {
            return;
        }

        gameSessionManager ??= GameSessionManager.instance;

        if (gameSessionManager == null)
        {
            return;
        }

        gameSessionManager.GameEntryCompleted += OnGameEntryCompleted;
        gameSessionManager.GameEntryFailed += OnGameEntryFailed;
        gameSessionManager.GameSessionUpdated += OnGameSessionUpdated;
        isSubscribedToGameSessionManager = true;
    }

    private void UnsubscribeFromGameSessionManager()
    {
        if (isSubscribedToGameSessionManager && gameSessionManager != null)
        {
            gameSessionManager.GameEntryCompleted -= OnGameEntryCompleted;
            gameSessionManager.GameEntryFailed -= OnGameEntryFailed;
            gameSessionManager.GameSessionUpdated -= OnGameSessionUpdated;
        }

        isSubscribedToGameSessionManager = false;
    }

    private void OnGameEntryCompleted(GameSessionResult result)
    {
        PrepareGameSessionChat(
            result?.gameSessionData ?? gameSessionManager?.CurrentGameSession);
    }

    private void OnGameEntryFailed(GameSessionResult _)
    {
        RestoreLobbySessionOrLeave();
    }

    private void OnGameSessionUpdated(GameSessionData gameSessionData)
    {
        if (gameSessionData == null)
        {
            RestoreLobbySessionOrLeave();
            return;
        }

        PrepareGameSessionChat(gameSessionData);
    }

    private void PrepareGameSessionChat(GameSessionData gameSessionData)
    {
        if (gameSessionData == null ||
            gameSessionData.runtimeType != SessionRuntimeType.Network ||
            string.IsNullOrWhiteSpace(gameSessionData.lobbyId))
        {
            return;
        }

        BeginSessionPreparation(gameSessionData.lobbyId);
        RefreshSessionParticipants(gameSessionData);
    }

    private void RestoreLobbySessionOrLeave()
    {
        lobbyManager ??= LobbyManager.instance;

        if (lobbyManager != null &&
            lobbyManager.HasEnteredLobby &&
            lobbyManager.RuntimeType == SessionRuntimeType.Network)
        {
            BeginSessionPreparation(lobbyManager.CurrentLobbyId);
            RefreshSessionParticipants(lobbyManager.CurrentLobbyViewData);
            return;
        }

        ClearPendingSessionPreparation();
        StartLeaveSession();
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
            SetChatConnectionState(ChatConnectionState.Disabled);
            return;
        }

        ChatSettingsData chatSettings = chatSettingsManager.CurrentSettings;
        isChatEnabled = vivoxChatService != null &&
                        vivoxChatService.ConnectionsEnabled &&
                        (chatSettings == null || chatSettings.chatEnabled);
        SetChatConnectionState(isChatEnabled ? ChatConnectionState.Connecting : ChatConnectionState.Disabled);
    }

    private void OnChatSettingsChanged(ChatSettingsData chatSettings)
    {
        if (suppressChatSettingsChanged)
        {
            return;
        }

        bool requestedEnabled = vivoxChatService != null &&
                                vivoxChatService.ConnectionsEnabled &&
                                (chatSettings == null || chatSettings.chatEnabled);

        if (requestedEnabled == isChatEnabled)
        {
            if (requestedEnabled && connectionState == ChatConnectionState.Unavailable)
            {
                StartChatRetryFromSettings();
                return;
            }

            PublishChatAvailabilityIfChanged();
            return;
        }

        isChatEnabled = requestedEnabled;

        if (!isChatEnabled)
        {
            lastServiceError = string.Empty;
            ClearPendingSessionPreparation();
            SetChatConnectionState(ChatConnectionState.Disabled);
            StartShutdownChat();
            PublishChatAvailabilityIfChanged();
            return;
        }

        StartChatRetryFromSettings();
    }

    private void StartChatRetryFromSettings()
    {
        lastServiceError = string.Empty;
        SetChatConnectionState(ChatConnectionState.Connecting);
        PublishChatAvailabilityIfChanged();

        if (connectionManager == null || !connectionManager.IsOnline)
        {
            lastServiceError = "Chat is unavailable.";
            SetChatConnectionState(ChatConnectionState.Unavailable);
            PublishChatAvailabilityIfChanged();
            return;
        }

        lobbyManager ??= LobbyManager.instance;

        if (lobbyManager != null && lobbyManager.HasEnteredLobby && lobbyManager.RuntimeType == SessionRuntimeType.Network)
        {
            BeginSessionPreparation(lobbyManager.CurrentLobbyId);
            return;
        }

        gameSessionManager ??= GameSessionManager.instance;

        if (gameSessionManager != null &&
            gameSessionManager.HasEnteredGame &&
            gameSessionManager.RuntimeType == SessionRuntimeType.Network)
        {
            BeginSessionPreparation(gameSessionManager.CurrentLobbyId);
            RefreshSessionParticipants(gameSessionManager.CurrentGameSession);
            return;
        }

        StartEnsureChatReady();
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
            case OnlineConnectionState.Connecting:
                if (isChatEnabled)
                {
                    SetChatConnectionState(ChatConnectionState.Connecting);
                }
                break;

            case OnlineConnectionState.Online:
                if (isChatEnabled)
                {
                    StartEnsureChatReady();
                }
                break;

            case OnlineConnectionState.Offline:
                ClearPendingSessionPreparation();

                if (isChatEnabled)
                {
                    SetChatConnectionState(ChatConnectionState.Unavailable);
                }

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
        if (!isChatEnabled)
        {
            if (connectionState == ChatConnectionState.Unavailable)
            {
                if (string.IsNullOrWhiteSpace(lastServiceError))
                {
                    lastServiceError = string.IsNullOrWhiteSpace(reason) ? "Chat is unavailable." : reason;
                }

                PublishChatAvailabilityIfChanged();
                return;
            }

            lastServiceError = string.Empty;
            SetChatConnectionState(ChatConnectionState.Disabled);
            PublishChatAvailabilityIfChanged();
            return;
        }

        HandleChatConnectionFailure(reason);
    }

    #endregion

    #region Shutdown

    private async void StartShutdownChat()
    {
        await ShutdownChatAsync();
    }

    public async Task ShutdownChatAsync()
    {
        RequestHelpClose();

        if (chatService != null)
        {
            await chatService.ShutdownAsync();
        }

        ClearChatConversationState();

        if (!isChatEnabled)
        {
            SetChatConnectionState(connectionState == ChatConnectionState.Unavailable
                ? ChatConnectionState.Unavailable
                : ChatConnectionState.Disabled);
        }

        PublishChatAvailabilityIfChanged();
    }

    private void ClearChatConversationState()
    {
        bool hadConversations = conversations.Count > 0;

        ClearConversationMessages();
        conversations.Clear();
        activeConversationKey = string.Empty;
        sessionConversationKey = string.Empty;
        lastPrivateMessageUserId = string.Empty;
        lastSuggestedUserId = string.Empty;
        lastSuggestedCommandName = string.Empty;
        ClearSessionBlocks();

        if (hadConversations)
        {
            ActiveConversationChanged?.Invoke(null);
            SessionParticipantsChanged?.Invoke(null);
        }
    }

    private void ClearConversationMessages()
    {
        foreach (ChatConversationData conversation in conversations.Values)
        {
            if (conversation?.messages == null)
            {
                continue;
            }

            conversation.messages.Clear();
            conversation.unreadCount = 0;
            ConversationMessagesChanged?.Invoke(conversation);
        }
    }

    #endregion
}
