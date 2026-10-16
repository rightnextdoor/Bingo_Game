using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;
using BingoGame.Chat;

[DefaultExecutionOrder(-1060)]
[DisallowMultipleComponent]
public class VivoxChatService : MonoBehaviour, IChatService
{
    #region Metadata

    [Serializable]
    private class BingoChatMessageMetadata
    {
        public string type = MetadataType;
        public string conversationId = string.Empty;
        public ChatConversationType conversationType = ChatConversationType.Session;
        public string userId = string.Empty;
        public string playerName = string.Empty;
        public string iconId = string.Empty;
        public bool isPrivate;
        public string recipientUserId = string.Empty;
    }

    private const string MetadataType = "BingoChatMessage";

    #endregion

    #region Fields

    private OnlineServicesRoot onlineServicesRoot;

    private readonly Dictionary<string, ChatConversationReference> conversationByChannelName = new Dictionary<string, ChatConversationReference>();
    private readonly Dictionary<string, string> channelNameByConversationKey = new Dictionary<string, string>();
    private readonly Dictionary<string, Dictionary<string, string>> providerPlayerIdByUserIdByConversationKey = new Dictionary<string, Dictionary<string, string>>();

    private ChatParticipantData localParticipant;

    private bool isAdapterReady;
    private bool eventsSubscribed;
    private bool isShuttingDown;
    private bool isApplicationStopping;
    private CancellationTokenSource operationCancellation = new CancellationTokenSource();
    private Task shutdownTask;
    private static VivoxChatService activeService;
    private bool serviceUnavailable;
    private bool recoveryInProgress;
    private bool recoveringEstablishedConnection;
    private bool sdkRecovering;
    private bool awaitingRecoveryLogout;
    private TaskCompletionSource<bool> sdkRecoveryCompletion;
    private const int MaximumConnectionAttempts = 3;
    private const int ConnectionAttemptMilliseconds = 10000;

    private Task<bool> readinessTask;
    private string lastError = string.Empty;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool developmentConnectionAvailable = true;
    private float developmentRetryFailureDelaySeconds = 2f;
    public bool DevelopmentConnectionAvailable => developmentConnectionAvailable;
#endif

    public bool IsReady =>
        isAdapterReady && !isShuttingDown && !isApplicationStopping &&
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        developmentConnectionAvailable &&
#endif
        !serviceUnavailable && !sdkRecovering &&
        VivoxService.Instance != null &&
        VivoxService.Instance.InitializationState == VivoxInitializationState.Initialized &&
        VivoxService.Instance.IsLoggedIn;

    public string LastError => lastError;
    public bool IsRecoveringConnection => recoveryInProgress && recoveringEstablishedConnection;

    public event Action<ChatMessageData> MessageReceived;
    public event Action<string> ServiceUnavailable;
    public event Action<bool> ConnectionRecoveryChanged;

    #endregion

    #region Unity Methods

    private void OnApplicationQuit()
    {
        BeginApplicationShutdown();
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterEditorShutdown()
    {
        UnityEditor.EditorApplication.playModeStateChanged -= OnEditorPlayModeChanged;
        UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
    }

    private static void OnEditorPlayModeChanged(UnityEditor.PlayModeStateChange _state)
    {
        if (_state == UnityEditor.PlayModeStateChange.ExitingPlayMode && activeService != null)
            activeService.BeginApplicationShutdown();
    }
#endif

    private void BeginApplicationShutdown()
    {
        if (isApplicationStopping) return;
        isApplicationStopping = true;
        ChatManager.instance?.PrepareForApplicationShutdown();
        _ = ShutdownAsync();
    }

    private void OnDestroy()
    {
        BeginApplicationShutdown();
        UnsubscribeFromVivoxEvents();
        operationCancellation.Dispose();
        if (activeService == this) activeService = null;
    }

    #endregion

    #region Initialization

    public bool Initialize()
    {
        if (isAdapterReady)
        {
            return true;
        }

        onlineServicesRoot = OnlineServicesRoot.instance;

        if (onlineServicesRoot == null || onlineServicesRoot != GetComponent<OnlineServicesRoot>() || !onlineServicesRoot.IsPrimaryInstance)
        {
            return false;
        }

        isAdapterReady = true;
        activeService = this;
        VivoxConnectionLogHandler.Install();
        return true;
    }

    public async Task<bool> EnsureReadyAsync(ChatParticipantData participant)
    {
        if (!isAdapterReady || isShuttingDown || isApplicationStopping || !Application.isPlaying ||
            participant == null || !participant.IsValid)
        {
            return false;
        }

        UpdateLocalParticipant(participant);

        if (readinessTask != null && !readinessTask.IsCompleted)
            return await readinessTask;

        if (IsReady)
        {
            lastError = string.Empty;
            return true;
        }

        Task<bool> activeTask = RecoverConnectionAsync(false, false);
        readinessTask = activeTask;

        try
        {
            return await activeTask;
        }
        finally
        {
            if (readinessTask == activeTask)
            {
                readinessTask = null;
            }
        }
    }

    private async Task<bool> EnsureReadyInternalAsync()
    {
        CancellationToken cancellation = operationCancellation.Token;
        lastError = string.Empty;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!developmentConnectionAvailable)
        {
            SetUnavailable("Vivox connection is disabled by the development simulation.");
            return false;
        }
#endif

        serviceUnavailable = false;

        using VivoxConnectionLogHandler.ConnectionAttempt connectionAttempt = VivoxConnectionLogHandler.BeginAttempt();
        try
        {
            if (VivoxService.Instance == null)
            {
                SetUnavailable("VivoxService was not available.");
                return false;
            }

            SubscribeToVivoxEvents();

            if (VivoxService.Instance.InitializationState != VivoxInitializationState.Initialized)
            {
                if (!await AwaitOperationAsync(VivoxService.Instance.InitializeAsync(), cancellation)) return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!developmentConnectionAvailable)
            {
                SetUnavailable("Vivox connection is disabled by the development simulation.");
                return false;
            }
#endif

            if (this == null || cancellation.IsCancellationRequested || isApplicationStopping) return false;
            if (!VivoxService.Instance.IsLoggedIn)
            {
                LoginOptions loginOptions = new LoginOptions
                {
                    DisplayName = localParticipant.userId
                };

                if (!await AwaitOperationAsync(VivoxService.Instance.LoginAsync(loginOptions), cancellation)) return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!developmentConnectionAvailable)
            {
                if (VivoxService.Instance.IsLoggedIn)
                {
                    await VivoxService.Instance.LogoutAsync();
                }

                SetUnavailable("Vivox connection is disabled by the development simulation.");
                return false;
            }
#endif

            if (this == null || cancellation.IsCancellationRequested || isApplicationStopping) return false;
            if (!VivoxService.Instance.IsLoggedIn)
            {
                SetUnavailable("Vivox did not create a logged-in session.");
                return false;
            }

            lastError = string.Empty;
            serviceUnavailable = false;
            return true;
        }
        catch (Exception exception)
        {
            if (this == null || cancellation.IsCancellationRequested || isApplicationStopping) return false;
            connectionAttempt.ReportFailure(exception);
            SetUnavailable(exception.Message);
            return false;
        }
    }

    public void UpdateLocalParticipant(ChatParticipantData participant)
    {
        localParticipant = participant?.Clone();
    }

    #endregion

    #region Conversations

    public async Task<bool> JoinConversationAsync(ChatConversationReference conversation)
    {
        if (!IsReady || conversation == null || !conversation.IsValid)
        {
            return false;
        }

        CancellationToken cancellation = operationCancellation.Token;
        string conversationKey = conversation.Key;

        if (channelNameByConversationKey.ContainsKey(conversationKey))
        {
            RefreshParticipantMappings(conversation);
            return true;
        }

        string channelName = BuildChannelName(conversation);

        try
        {
            if (!await AwaitOperationAsync(
                    VivoxService.Instance.JoinGroupChannelAsync(channelName, ChatCapability.TextOnly), cancellation)) return false;
            if (this == null || cancellation.IsCancellationRequested || isApplicationStopping) return false;

            channelNameByConversationKey[conversationKey] = channelName;
            conversationByChannelName[channelName] = new ChatConversationReference(conversation.conversationId, conversation.conversationType);
            providerPlayerIdByUserIdByConversationKey[conversationKey] = new Dictionary<string, string>(StringComparer.Ordinal);

            RefreshParticipantMappings(conversation);
            return true;
        }
        catch (Exception exception)
        {
            if (this != null && !cancellation.IsCancellationRequested && !isApplicationStopping)
                lastError = exception.Message;
            return false;
        }
    }

    public async Task<bool> LeaveConversationAsync(ChatConversationReference conversation)
    {
        if (conversation == null || !conversation.IsValid)
        {
            return false;
        }

        string conversationKey = conversation.Key;

        if (!channelNameByConversationKey.TryGetValue(conversationKey, out string channelName))
        {
            providerPlayerIdByUserIdByConversationKey.Remove(conversationKey);
            return true;
        }

        channelNameByConversationKey.Remove(conversationKey);
        conversationByChannelName.Remove(channelName);
        providerPlayerIdByUserIdByConversationKey.Remove(conversationKey);
        if (isApplicationStopping || isShuttingDown) return true;
        CancellationToken cancellation = operationCancellation.Token;
        try
        {
            if (VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn)
            {
                if (!await AwaitOperationAsync(VivoxService.Instance.LeaveChannelAsync(channelName), cancellation)) return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            lastError = exception.Message;
            return false;
        }
    }

    #endregion

    #region Messages

    public async Task<ChatSendResult> SendMessageAsync(ChatConversationReference conversation, string message)
    {
        if (!IsReady)
        {
            return ChatSendResult.Failed(string.IsNullOrWhiteSpace(lastError) ? "Chat is not available." : lastError);
        }

        if (conversation == null || !conversation.IsValid)
        {
            return ChatSendResult.Failed("The chat conversation is not valid.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return ChatSendResult.Failed("The chat message is empty.");
        }

        if (!channelNameByConversationKey.TryGetValue(conversation.Key, out string channelName))
        {
            return ChatSendResult.Failed("The chat conversation has not been joined.");
        }

        try
        {
            MessageOptions messageOptions = new MessageOptions
            {
                Metadata = BuildMessageMetadata(conversation, false, string.Empty)
            };

            await VivoxService.Instance.SendChannelTextMessageAsync(channelName, message, messageOptions);
            return ChatSendResult.Succeeded();
        }
        catch (Exception exception)
        {
            lastError = exception.Message;
            return ChatSendResult.Failed(lastError);
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public async Task<ChatSendResult> SendStressChannelMessageAsync(ChatConversationReference conversation, ChatParticipantData simulatedSender, string message)
    {
        if (!IsReady)
        {
            return ChatSendResult.Failed(string.IsNullOrWhiteSpace(lastError) ? "Chat is not available." : lastError);
        }

        if (conversation == null || !conversation.IsValid || conversation.conversationType != ChatConversationType.Session)
        {
            return ChatSendResult.Failed("The Session conversation is not valid.");
        }

        if (simulatedSender == null || !simulatedSender.IsValid || string.IsNullOrWhiteSpace(message))
        {
            return ChatSendResult.Failed("The synthetic sender or message is invalid.");
        }

        if (!channelNameByConversationKey.TryGetValue(conversation.Key, out string channelName))
        {
            return ChatSendResult.Failed("The chat conversation has not been joined.");
        }

        try
        {
            MessageOptions messageOptions = new MessageOptions
            {
                Metadata = BuildMessageMetadata(conversation, false, string.Empty, simulatedSender)
            };

            await VivoxService.Instance.SendChannelTextMessageAsync(channelName, message.Trim(), messageOptions);
            return ChatSendResult.Succeeded();
        }
        catch (Exception exception)
        {
            lastError = exception.Message;
            return ChatSendResult.Failed(lastError);
        }
    }
#endif

    public async Task<ChatSendResult> SendDirectMessageAsync(ChatConversationReference conversation, string recipientUserId, string message)
    {
        if (!IsReady)
        {
            return ChatSendResult.Failed(string.IsNullOrWhiteSpace(lastError) ? "Chat is not available." : lastError);
        }

        if (conversation == null || !conversation.IsValid || conversation.conversationType != ChatConversationType.Session)
        {
            return ChatSendResult.Failed("The Session conversation is not valid.");
        }

        if (string.IsNullOrWhiteSpace(recipientUserId) || string.IsNullOrWhiteSpace(message))
        {
            return ChatSendResult.Failed("The private message target or message is missing.");
        }

        RefreshParticipantMappings(conversation);

        if (!TryGetProviderPlayerId(conversation, recipientUserId, out string providerPlayerId))
        {
            return ChatSendResult.Failed("That player is not available in the current Session chat.");
        }

        try
        {
            MessageOptions messageOptions = new MessageOptions
            {
                Metadata = BuildMessageMetadata(conversation, true, recipientUserId)
            };

            await VivoxService.Instance.SendDirectTextMessageAsync(providerPlayerId, message, messageOptions);
            return ChatSendResult.Succeeded();
        }
        catch (Exception exception)
        {
            lastError = exception.Message;
            return ChatSendResult.Failed(lastError);
        }
    }

    public async Task<IReadOnlyList<ChatMessageData>> GetHistoryAsync(ChatHistoryRequest request)
    {
        List<ChatMessageData> history = new List<ChatMessageData>();

        if (!IsReady || request?.conversation == null || !request.conversation.IsValid || request.maximumMessages <= 0)
        {
            return history;
        }

        if (!channelNameByConversationKey.TryGetValue(request.conversation.Key, out string channelName))
        {
            return history;
        }

        try
        {
            ReadOnlyCollection<VivoxMessage> messages = await VivoxService.Instance.GetChannelTextMessageHistoryAsync(channelName, request.maximumMessages);

            if (messages == null)
            {
                return history;
            }

            for (int i = messages.Count - 1; i >= 0; i--)
            {
                ChatMessageData chatMessage = ConvertChannelMessage(messages[i], request.conversation);

                if (chatMessage != null)
                {
                    history.Add(chatMessage);
                }
            }
        }
        catch (Exception exception)
        {
            lastError = exception.Message;
        }

        return history;
    }

    private void OnChannelMessageReceived(VivoxMessage vivoxMessage)
    {
        if (vivoxMessage == null || string.IsNullOrWhiteSpace(vivoxMessage.ChannelName) ||
            !conversationByChannelName.TryGetValue(vivoxMessage.ChannelName, out ChatConversationReference conversation))
        {
            return;
        }

        ChatMessageData message = ConvertChannelMessage(vivoxMessage, conversation);

        if (message != null)
        {
            MessageReceived?.Invoke(message);
        }
    }

    private void OnDirectedMessageReceived(VivoxMessage vivoxMessage)
    {
        ChatMessageData message = ConvertDirectedMessage(vivoxMessage);

        if (message != null)
        {
            MessageReceived?.Invoke(message);
        }
    }

    private ChatMessageData ConvertChannelMessage(VivoxMessage vivoxMessage, ChatConversationReference conversation)
    {
        if (vivoxMessage == null || conversation == null)
        {
            return null;
        }

        BingoChatMessageMetadata metadata = ParseMessageMetadata(vivoxMessage.Metadata);
        return BuildChatMessage(vivoxMessage, conversation, metadata, false);
    }

    private ChatMessageData ConvertDirectedMessage(VivoxMessage vivoxMessage)
    {
        if (vivoxMessage == null)
        {
            return null;
        }

        BingoChatMessageMetadata metadata = ParseMessageMetadata(vivoxMessage.Metadata);

        if (metadata == null || !metadata.isPrivate || string.IsNullOrWhiteSpace(metadata.conversationId))
        {
            return null;
        }

        ChatConversationReference conversation = new ChatConversationReference(metadata.conversationId, metadata.conversationType);

        if (!channelNameByConversationKey.ContainsKey(conversation.Key))
        {
            return null;
        }

        return BuildChatMessage(vivoxMessage, conversation, metadata, true);
    }

    private ChatMessageData BuildChatMessage(VivoxMessage vivoxMessage, ChatConversationReference conversation, BingoChatMessageMetadata metadata, bool isPrivate)
    {
        string senderUserId = metadata != null ? metadata.userId : string.Empty;
        string senderPlayerName = metadata != null && !string.IsNullOrWhiteSpace(metadata.playerName) ? metadata.playerName : vivoxMessage.SenderDisplayName;
        string senderIconId = metadata != null ? metadata.iconId : string.Empty;

        DateTime receivedTime = vivoxMessage.ReceivedTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(vivoxMessage.ReceivedTime, DateTimeKind.Utc)
            : vivoxMessage.ReceivedTime.ToUniversalTime();

        long timestampUnixMilliseconds = new DateTimeOffset(receivedTime).ToUnixTimeMilliseconds();

        bool isFromCurrentUser = metadata != null && localParticipant != null && !string.IsNullOrWhiteSpace(metadata.userId)
            ? string.Equals(metadata.userId, localParticipant.userId, StringComparison.Ordinal)
            : vivoxMessage.FromSelf;

        return new ChatMessageData(
            vivoxMessage.MessageId,
            vivoxMessage.MessageId,
            conversation.conversationId,
            conversation.conversationType,
            senderUserId,
            senderPlayerName,
            senderIconId,
            vivoxMessage.SenderPlayerId,
            vivoxMessage.MessageText,
            timestampUnixMilliseconds,
            isFromCurrentUser,
            isPrivate,
            metadata?.recipientUserId);
    }

    private string BuildMessageMetadata(ChatConversationReference conversation, bool isPrivate, string recipientUserId)
    {
        return BuildMessageMetadata(conversation, isPrivate, recipientUserId, localParticipant);
    }

    private string BuildMessageMetadata(ChatConversationReference conversation, bool isPrivate, string recipientUserId, ChatParticipantData participant)
    {
        if (participant == null || !participant.IsValid)
        {
            return string.Empty;
        }

        BingoChatMessageMetadata metadata = new BingoChatMessageMetadata
        {
            conversationId = conversation?.conversationId ?? string.Empty,
            conversationType = conversation?.conversationType ?? ChatConversationType.Session,
            userId = participant.userId,
            playerName = participant.playerName,
            iconId = participant.iconId,
            isPrivate = isPrivate,
            recipientUserId = recipientUserId ?? string.Empty
        };

        return JsonUtility.ToJson(metadata);
    }

    private BingoChatMessageMetadata ParseMessageMetadata(string metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            BingoChatMessageMetadata metadata = JsonUtility.FromJson<BingoChatMessageMetadata>(metadataJson);
            return metadata != null && metadata.type == MetadataType ? metadata : null;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Participant Mapping

    private void OnParticipantAddedToChannel(VivoxParticipant participant)
    {
        UpdateParticipantMapping(participant, true);
    }

    private void OnParticipantRemovedFromChannel(VivoxParticipant participant)
    {
        UpdateParticipantMapping(participant, false);
    }

    private void UpdateParticipantMapping(VivoxParticipant participant, bool add)
    {
        if (participant == null || string.IsNullOrWhiteSpace(participant.ChannelName) ||
            !conversationByChannelName.TryGetValue(participant.ChannelName, out ChatConversationReference conversation))
        {
            return;
        }

        string userId = participant.DisplayName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        if (!providerPlayerIdByUserIdByConversationKey.TryGetValue(conversation.Key, out Dictionary<string, string> participantMap))
        {
            participantMap = new Dictionary<string, string>(StringComparer.Ordinal);
            providerPlayerIdByUserIdByConversationKey[conversation.Key] = participantMap;
        }

        if (add)
        {
            participantMap[userId] = participant.PlayerId;
        }
        else
        {
            participantMap.Remove(userId);
        }
    }

    private void RefreshParticipantMappings(ChatConversationReference conversation)
    {
        if (conversation == null || !conversation.IsValid || VivoxService.Instance == null ||
            !channelNameByConversationKey.TryGetValue(conversation.Key, out string channelName))
        {
            return;
        }

        if (!providerPlayerIdByUserIdByConversationKey.TryGetValue(conversation.Key, out Dictionary<string, string> participantMap))
        {
            participantMap = new Dictionary<string, string>(StringComparer.Ordinal);
            providerPlayerIdByUserIdByConversationKey[conversation.Key] = participantMap;
        }

        participantMap.Clear();

        if (!VivoxService.Instance.ActiveChannels.TryGetValue(channelName, out ReadOnlyCollection<VivoxParticipant> participants) || participants == null)
        {
            return;
        }

        for (int i = 0; i < participants.Count; i++)
        {
            VivoxParticipant participant = participants[i];
            string userId = participant?.DisplayName?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(participant.PlayerId))
            {
                participantMap[userId] = participant.PlayerId;
            }
        }
    }

    private bool TryGetProviderPlayerId(ChatConversationReference conversation, string userId, out string providerPlayerId)
    {
        providerPlayerId = string.Empty;

        return conversation != null &&
               !string.IsNullOrWhiteSpace(userId) &&
               providerPlayerIdByUserIdByConversationKey.TryGetValue(conversation.Key, out Dictionary<string, string> participantMap) &&
               participantMap.TryGetValue(userId.Trim(), out providerPlayerId) &&
               !string.IsNullOrWhiteSpace(providerPlayerId);
    }

    #endregion

    #region Vivox Events

    private void BeginConnectionRecovery(bool _waitForSdk)
    {
        if (isApplicationStopping || isShuttingDown || recoveryInProgress) return;
        readinessTask = RecoverConnectionAsync(_waitForSdk, true);
    }

    internal void RequestConnectionRecovery()
    {
        BeginConnectionRecovery(false);
    }

    private async Task<bool> RecoverConnectionAsync(bool _waitForSdk, bool _restoreChannels)
    {
        recoveryInProgress = true;
        recoveringEstablishedConnection = _restoreChannels;
        CancellationToken cancellation = operationCancellation.Token;
        List<ChatConversationReference> channels = _restoreChannels
            ? new List<ChatConversationReference>(conversationByChannelName.Values)
            : new List<ChatConversationReference>();
        ConnectionRecoveryChanged?.Invoke(true);
        await Task.Yield();
        bool recovered = false;
        try
        {
            if (_waitForSdk && sdkRecoveryCompletion != null)
            {
                Task completed = await Task.WhenAny(sdkRecoveryCompletion.Task, Task.Delay(31000, cancellation));
                cancellation.ThrowIfCancellationRequested();
                recovered = completed == sdkRecoveryCompletion.Task && await sdkRecoveryCompletion.Task;
                sdkRecovering = false;
                if (recovered) serviceUnavailable = false;
            }

            Task<bool> pendingAttempt = null;
            for (int attempt = 0; !recovered && attempt < MaximumConnectionAttempts; attempt++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (pendingAttempt == null || pendingAttempt.IsCompleted)
                    pendingAttempt = TryRestoreConnectionAsync(channels, cancellation);
                Task completed = await Task.WhenAny(pendingAttempt, Task.Delay(ConnectionAttemptMilliseconds, cancellation));
                cancellation.ThrowIfCancellationRequested();
                if (completed == pendingAttempt) recovered = await pendingAttempt;
                else lastError = "Vivox connection attempt timed out.";
                if (!recovered && attempt + 1 < MaximumConnectionAttempts)
                    await Task.Delay(1000, cancellation);
            }
            if (!recovered && pendingAttempt != null && !pendingAttempt.IsCompleted)
            {
                operationCancellation.Cancel();
                _ = ObserveStoppedOperationAsync(pendingAttempt);
            }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception exception) { lastError = exception.Message; }
        finally
        {
            recoveryInProgress = false;
            recoveringEstablishedConnection = false;
            sdkRecovering = false;
            sdkRecoveryCompletion = null;
        }
        if (this == null || isApplicationStopping || isShuttingDown || cancellation.IsCancellationRequested && recovered)
            return false;
        if (recovered)
        {
            lastError = string.Empty;
            ConnectionRecoveryChanged?.Invoke(false);
            return true;
        }
        SetUnavailable(lastError);
        return false;
    }

    private async Task<bool> TryRestoreConnectionAsync(List<ChatConversationReference> _channels, CancellationToken _cancellation)
    {
        if (sdkRecovering && sdkRecoveryCompletion != null)
        {
            if (!await AwaitOperationAsync(sdkRecoveryCompletion.Task, _cancellation)) return false;
            sdkRecovering = false;
        }
        while (awaitingRecoveryLogout && VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn)
            await Task.Delay(100, _cancellation);
        _cancellation.ThrowIfCancellationRequested();
        if (OnlineConnectionManager.instance == null || !OnlineConnectionManager.instance.IsOnline)
        {
            lastError = "Online services are unavailable.";
            await Task.Delay(ConnectionAttemptMilliseconds, _cancellation);
            return false;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!developmentConnectionAvailable && !await WaitForDevelopmentConnectionAttemptAsync()) return false;
#endif
        if (!await EnsureReadyInternalAsync() || _cancellation.IsCancellationRequested) return false;
        foreach (ChatConversationReference channel in _channels)
        {
            if (!await JoinConversationAsync(channel) || _cancellation.IsCancellationRequested) return false;
        }
        return true;
    }

    private void OnConnectionRecovering()
    {
        if (isShuttingDown || isApplicationStopping) return;
        sdkRecovering = true;
        sdkRecoveryCompletion ??= new TaskCompletionSource<bool>();
        BeginConnectionRecovery(true);
    }

    private void OnConnectionRecovered()
    {
        sdkRecovering = false;
        sdkRecoveryCompletion?.TrySetResult(true);
    }

    private void SubscribeToVivoxEvents()
    {
        if (eventsSubscribed || VivoxService.Instance == null)
        {
            return;
        }

        VivoxService.Instance.ChannelMessageReceived += OnChannelMessageReceived;
        VivoxService.Instance.DirectedMessageReceived += OnDirectedMessageReceived;
        VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAddedToChannel;
        VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemovedFromChannel;
        VivoxService.Instance.ConnectionFailedToRecover += OnConnectionFailedToRecover;
        VivoxService.Instance.ConnectionRecovering += OnConnectionRecovering;
        VivoxService.Instance.ConnectionRecovered += OnConnectionRecovered;
        VivoxService.Instance.LoggedOut += OnVivoxLoggedOut;

        eventsSubscribed = true;
    }

    private void UnsubscribeFromVivoxEvents()
    {
        if (!eventsSubscribed || VivoxService.Instance == null)
        {
            eventsSubscribed = false;
            return;
        }

        VivoxService.Instance.ChannelMessageReceived -= OnChannelMessageReceived;
        VivoxService.Instance.DirectedMessageReceived -= OnDirectedMessageReceived;
        VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAddedToChannel;
        VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemovedFromChannel;
        VivoxService.Instance.ConnectionFailedToRecover -= OnConnectionFailedToRecover;
        VivoxService.Instance.ConnectionRecovering -= OnConnectionRecovering;
        VivoxService.Instance.ConnectionRecovered -= OnConnectionRecovered;
        VivoxService.Instance.LoggedOut -= OnVivoxLoggedOut;

        eventsSubscribed = false;
    }

    private void OnConnectionFailedToRecover()
    {
        awaitingRecoveryLogout = true;
        lastError = "Vivox could not recover the chat connection.";
        if (sdkRecoveryCompletion != null) sdkRecoveryCompletion.TrySetResult(false);
        else BeginConnectionRecovery(false);
    }

    private void OnVivoxLoggedOut()
    {
        if (!isShuttingDown && !isApplicationStopping)
        {
            awaitingRecoveryLogout = false;
            if (sdkRecoveryCompletion != null) sdkRecoveryCompletion.TrySetResult(false);
            else BeginConnectionRecovery(false);
            channelNameByConversationKey.Clear();
            conversationByChannelName.Clear();
            providerPlayerIdByUserIdByConversationKey.Clear();
        }
    }

    private void SetUnavailable(string reason)
    {
        if (isApplicationStopping || isShuttingDown) return;
        if (recoveryInProgress)
        {
            lastError = string.IsNullOrWhiteSpace(reason) ? "Chat is unavailable." : reason;
            return;
        }
        serviceUnavailable = true;
        lastError = string.IsNullOrWhiteSpace(reason) ? "Chat is unavailable." : reason;

        channelNameByConversationKey.Clear();
        conversationByChannelName.Clear();
        providerPlayerIdByUserIdByConversationKey.Clear();

        ServiceUnavailable?.Invoke(lastError);
    }

    #endregion

    #region Development Simulation

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    public Task<bool> SetDevelopmentConnectionAvailableAsync(bool available, float retryFailureDelaySeconds = 2f)
    {
        developmentRetryFailureDelaySeconds = Mathf.Max(0f, retryFailureDelaySeconds);
        developmentConnectionAvailable = available;
        if (!available)
        {
            lastError = "Vivox connection is disabled by the development simulation.";
            BeginConnectionRecovery(false);
        }
        return Task.FromResult(true);
    }
    public async Task<bool> SimulateUnexpectedConnectionLossAsync()
    {
        return await SetDevelopmentConnectionAvailableAsync(false);
    }

    private async Task<bool> WaitForDevelopmentConnectionAttemptAsync()
    {
        float delaySeconds = Mathf.Max(0f, developmentRetryFailureDelaySeconds);

        if (delaySeconds > 0f)
        {
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), operationCancellation.Token);
        }

        if (developmentConnectionAvailable)
        {
            return true;
        }

        SetUnavailable("Vivox connection is disabled by the development simulation.");
        return false;
    }

#endif

    #endregion

    #region Shutdown

    public Task ShutdownAsync()
    {
        if (isApplicationStopping && shutdownTask != null) return shutdownTask;
        if (shutdownTask != null && !shutdownTask.IsCompleted) return shutdownTask;
        if (!isAdapterReady) return Task.CompletedTask;
        isShuttingDown = true;
        awaitingRecoveryLogout = false;
        operationCancellation.Cancel();
        UnsubscribeFromVivoxEvents();
        channelNameByConversationKey.Clear();
        conversationByChannelName.Clear();
        providerPlayerIdByUserIdByConversationKey.Clear();
        shutdownTask = LogoutInternalAsync();
        return shutdownTask;
    }

    private async Task LogoutInternalAsync()
    {
        try
        {
            if (VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn)
                await VivoxService.Instance.LogoutAsync();
        }
        catch (Exception exception)
        {
            if (this != null && !isApplicationStopping) lastError = exception.Message;
        }
        finally
        {
            if (this != null && !isApplicationStopping)
            {
                operationCancellation.Dispose();
                operationCancellation = new CancellationTokenSource();
                readinessTask = null;
                isShuttingDown = false;
            }
        }
    }

    private static async Task<bool> AwaitOperationAsync(Task _operation, CancellationToken _cancellation)
    {
        TaskCompletionSource<bool> stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (_cancellation.Register(() => stopped.TrySetResult(true)))
        {
            if (await Task.WhenAny(_operation, stopped.Task) != _operation)
            {
                _ = ObserveStoppedOperationAsync(_operation);
                return false;
            }
            await _operation;
            return !_cancellation.IsCancellationRequested;
        }
    }

    private static async Task ObserveStoppedOperationAsync(Task _operation)
    {
        try { await _operation; }
        catch (Exception) { }
    }

    #endregion

    #region Channel Names

    private string BuildChannelName(ChatConversationReference conversation)
    {
        string prefix;

        switch (conversation.conversationType)
        {
            case ChatConversationType.FriendDirect:
                prefix = "bingo-direct-";
                break;

            case ChatConversationType.FriendGroup:
                prefix = "bingo-group-";
                break;

            case ChatConversationType.Session:
            default:
                prefix = "bingo-session-";
                break;
        }

        string source = $"{(int)conversation.conversationType}:{conversation.conversationId}";
        return prefix + GetSha256Hex(source);
    }

    private string GetSha256Hex(string value)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            byte[] hash = sha256.ComputeHash(bytes);
            StringBuilder builder = new StringBuilder(hash.Length * 2);

            for (int i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }

    #endregion
}
