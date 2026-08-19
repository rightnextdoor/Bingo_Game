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

    private readonly Dictionary<string, ChatConversationData> conversations =
        new Dictionary<string, ChatConversationData>();

    private bool isReady;
    private bool isSubscribedToConnectionManager;
    private bool isSubscribedToUserManager;

    private Task<bool> serviceReadyTask;

    private string activeConversationKey = string.Empty;
    private string lastServiceError = string.Empty;

    public bool IsReady => isReady;
    public bool IsChatAvailable => isReady && chatService != null && chatService.IsReady;
    public string LastServiceError => lastServiceError;
    public IReadOnlyDictionary<string, ChatConversationData> Conversations => conversations;

    public ChatConversationData ActiveConversation =>
        conversations.TryGetValue(activeConversationKey, out ChatConversationData conversation)
            ? conversation
            : null;

    public event Action<bool> ChatAvailabilityChanged;
    public event Action<ChatConversationData> ConversationJoined;
    public event Action<ChatConversationReference> ConversationLeft;
    public event Action<ChatMessageData> MessageReceived;
    public event Action<ChatConversationData> ActiveConversationChanged;

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

        if (onlineServicesRoot == null ||
            !onlineServicesRoot.IsPrimaryInstance)
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

        if (onlineServicesRoot == null ||
            onlineServicesRoot != GetComponent<OnlineServicesRoot>() ||
            !onlineServicesRoot.IsPrimaryInstance)
        {
            return false;
        }

        connectionManager = OnlineConnectionManager.instance;
        vivoxChatService = GetComponent<VivoxChatService>();

        if (connectionManager == null ||
            !connectionManager.IsReady ||
            vivoxChatService == null)
        {
            return false;
        }

        chatService = vivoxChatService;

        SubscribeToConnectionManager();
        SubscribeToUserManager();
        SubscribeToChatService();

        isReady = true;

        if (connectionManager.IsOnline)
        {
            StartEnsureChatReady();
        }

        return true;
    }

    #endregion

    #region Availability

    public async Task<bool> EnsureChatReadyAsync()
    {
        if (!isReady ||
            connectionManager == null ||
            !connectionManager.IsOnline ||
            chatService == null)
        {
            return false;
        }

        ChatParticipantData localParticipant =
            GetCurrentLocalParticipant();

        if (localParticipant == null)
        {
            lastServiceError =
                "A valid player is required before chat can connect.";

            SetChatAvailability(false);
            return false;
        }

        chatService.UpdateLocalParticipant(
            localParticipant);

        if (chatService.IsReady)
        {
            lastServiceError = string.Empty;
            SetChatAvailability(true);
            return true;
        }

        if (serviceReadyTask != null &&
            !serviceReadyTask.IsCompleted)
        {
            return await serviceReadyTask;
        }

        Task<bool> activeTask =
            EnsureChatServiceReadyInternalAsync(
                localParticipant);

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

    private async Task<bool> EnsureChatServiceReadyInternalAsync(
        ChatParticipantData localParticipant)
    {
        bool wasAvailable = IsChatAvailable;

        bool ready =
            await chatService.EnsureReadyAsync(
                localParticipant);

        lastServiceError =
            ready
                ? string.Empty
                : chatService.LastError;

        bool isAvailable = IsChatAvailable;

        if (wasAvailable != isAvailable)
        {
            ChatAvailabilityChanged?.Invoke(
                isAvailable);
        }

        return ready;
    }

    private async void StartEnsureChatReady()
    {
        await EnsureChatReadyAsync();
    }

    private void SetChatAvailability(bool available)
    {
        if (available == IsChatAvailable)
        {
            return;
        }

        ChatAvailabilityChanged?.Invoke(
            available);
    }

    #endregion

    #region Conversations

    public async Task<bool> JoinConversationAsync(
        ChatConversationReference conversation)
    {
        if (conversation == null ||
            !conversation.IsValid)
        {
            return false;
        }

        if (!await EnsureChatReadyAsync())
        {
            return false;
        }

        string conversationKey =
            conversation.Key;

        if (conversations.ContainsKey(
                conversationKey))
        {
            return true;
        }

        bool joined =
            await chatService.JoinConversationAsync(
                conversation);

        if (!joined)
        {
            lastServiceError =
                chatService.LastError;

            return false;
        }

        ChatConversationData conversationData =
            new ChatConversationData(
                new ChatConversationReference(
                    conversation.conversationId,
                    conversation.conversationType));

        conversations[conversationKey] =
            conversationData;

        if (string.IsNullOrWhiteSpace(
                activeConversationKey))
        {
            SetActiveConversation(
                conversation);
        }

        ConversationJoined?.Invoke(
            conversationData);

        return true;
    }

    public async Task<bool> LeaveConversationAsync(
        ChatConversationReference conversation)
    {
        if (conversation == null ||
            !conversation.IsValid)
        {
            return false;
        }

        string conversationKey =
            conversation.Key;

        if (!conversations.ContainsKey(
                conversationKey))
        {
            return true;
        }

        bool left =
            await chatService.LeaveConversationAsync(
                conversation);

        if (!left)
        {
            lastServiceError =
                chatService.LastError;

            return false;
        }

        conversations.Remove(
            conversationKey);

        if (activeConversationKey ==
            conversationKey)
        {
            activeConversationKey =
                string.Empty;

            ActiveConversationChanged?.Invoke(
                null);
        }

        ConversationLeft?.Invoke(
            new ChatConversationReference(
                conversation.conversationId,
                conversation.conversationType));

        return true;
    }

    public bool SetActiveConversation(
        ChatConversationReference conversation)
    {
        if (conversation == null ||
            !conversation.IsValid ||
            !conversations.TryGetValue(
                conversation.Key,
                out ChatConversationData conversationData))
        {
            return false;
        }

        activeConversationKey =
            conversation.Key;

        conversationData.unreadCount = 0;

        ActiveConversationChanged?.Invoke(
            conversationData);

        return true;
    }

    public bool TryGetConversation(
        ChatConversationReference conversation,
        out ChatConversationData conversationData)
    {
        conversationData = null;

        return conversation != null &&
               conversation.IsValid &&
               conversations.TryGetValue(
                   conversation.Key,
                   out conversationData);
    }

    #endregion

    #region Messages

    public async Task<ChatSendResult> SendMessageAsync(
        ChatConversationReference conversation,
        string message)
    {
        if (conversation == null ||
            !conversation.IsValid)
        {
            return ChatSendResult.Failed(
                "The chat conversation is not valid.");
        }

        if (!conversations.ContainsKey(
                conversation.Key))
        {
            return ChatSendResult.Failed(
                "The chat conversation has not been joined.");
        }

        if (!await EnsureChatReadyAsync())
        {
            return ChatSendResult.Failed(
                string.IsNullOrWhiteSpace(lastServiceError)
                    ? "Chat is not available."
                    : lastServiceError);
        }

        ChatParticipantData localParticipant =
            GetCurrentLocalParticipant();

        if (localParticipant != null)
        {
            chatService.UpdateLocalParticipant(
                localParticipant);
        }

        ChatSendResult result =
            await chatService.SendMessageAsync(
                conversation,
                message);

        if (!result.success)
        {
            lastServiceError =
                result.failureMessage;
        }

        return result;
    }

    public async Task<bool> LoadHistoryAsync(
        ChatConversationReference conversation,
        int maximumMessages)
    {
        if (conversation == null ||
            !conversation.IsValid ||
            maximumMessages <= 0 ||
            !conversations.TryGetValue(
                conversation.Key,
                out ChatConversationData conversationData))
        {
            return false;
        }

        if (!await EnsureChatReadyAsync())
        {
            return false;
        }

        IReadOnlyList<ChatMessageData> history =
            await chatService.GetHistoryAsync(
                new ChatHistoryRequest(
                    conversation,
                    maximumMessages));

        MergeHistory(
            conversationData,
            history);

        return true;
    }

    private void OnChatMessageReceived(
        ChatMessageData message)
    {
        if (message == null)
        {
            return;
        }

        string conversationKey =
            ChatConversationReference.GetKey(
                message.conversationId,
                message.conversationType);

        if (!conversations.TryGetValue(
                conversationKey,
                out ChatConversationData conversation))
        {
            return;
        }

        if (ContainsMessage(
                conversation,
                message.messageId))
        {
            return;
        }

        conversation.messages.Add(
            message);

        if (activeConversationKey !=
            conversationKey)
        {
            conversation.unreadCount++;
        }

        MessageReceived?.Invoke(
            message);
    }

    private void MergeHistory(
        ChatConversationData conversation,
        IReadOnlyList<ChatMessageData> history)
    {
        if (conversation == null ||
            history == null ||
            history.Count == 0)
        {
            return;
        }

        for (int i = 0; i < history.Count; i++)
        {
            ChatMessageData message =
                history[i];

            if (message == null ||
                ContainsMessage(
                    conversation,
                    message.messageId))
            {
                continue;
            }

            conversation.messages.Add(
                message);
        }

        conversation.messages.Sort(
            CompareMessagesByTimestamp);
    }

    private bool ContainsMessage(
        ChatConversationData conversation,
        string messageId)
    {
        if (conversation == null ||
            conversation.messages == null ||
            string.IsNullOrWhiteSpace(messageId))
        {
            return false;
        }

        for (int i = 0;
             i < conversation.messages.Count;
             i++)
        {
            if (conversation.messages[i] != null &&
                conversation.messages[i].messageId ==
                messageId)
            {
                return true;
            }
        }

        return false;
    }

    private int CompareMessagesByTimestamp(
        ChatMessageData left,
        ChatMessageData right)
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

        return left.timestampUnixMilliseconds
            .CompareTo(
                right.timestampUnixMilliseconds);
    }

    #endregion

    #region Identity

    private ChatParticipantData GetCurrentLocalParticipant()
    {
        UserManager userManager =
            UserManager.instance;

        if (userManager == null ||
            !userManager.HasUser)
        {
            return null;
        }

        UserData userData =
            userManager.CurrentUser;

        if (userData == null ||
            !userData.HasUser ||
            userData.userTag == UserTag.Bot)
        {
            return null;
        }

        return new ChatParticipantData(
            userData.userId,
            userData.playerName,
            userData.iconId);
    }

    private void SubscribeToUserManager()
    {
        if (isSubscribedToUserManager)
        {
            return;
        }

        UserManager.UserChanged +=
            OnUserChanged;

        isSubscribedToUserManager = true;
    }

    private void UnsubscribeFromUserManager()
    {
        if (!isSubscribedToUserManager)
        {
            return;
        }

        UserManager.UserChanged -=
            OnUserChanged;

        isSubscribedToUserManager = false;
    }

    private void OnUserChanged()
    {
        ChatParticipantData localParticipant =
            GetCurrentLocalParticipant();

        chatService?.UpdateLocalParticipant(
            localParticipant);

        if (connectionManager != null &&
            connectionManager.IsOnline &&
            localParticipant != null)
        {
            StartEnsureChatReady();
        }
    }

    #endregion

    #region Connection

    private void SubscribeToConnectionManager()
    {
        if (isSubscribedToConnectionManager ||
            connectionManager == null)
        {
            return;
        }

        connectionManager.ConnectionStateChanged +=
            OnOnlineConnectionStateChanged;

        isSubscribedToConnectionManager = true;
    }

    private void UnsubscribeFromConnectionManager()
    {
        if (isSubscribedToConnectionManager &&
            connectionManager != null)
        {
            connectionManager.ConnectionStateChanged -=
                OnOnlineConnectionStateChanged;
        }

        isSubscribedToConnectionManager = false;
    }

    private void OnOnlineConnectionStateChanged(
        OnlineConnectionState state)
    {
        switch (state)
        {
            case OnlineConnectionState.Online:
                StartEnsureChatReady();
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
        if (chatService == null)
        {
            return;
        }

        chatService.MessageReceived +=
            OnChatMessageReceived;

        chatService.ServiceUnavailable +=
            OnChatServiceUnavailable;
    }

    private void UnsubscribeFromChatService()
    {
        if (chatService == null)
        {
            return;
        }

        chatService.MessageReceived -=
            OnChatMessageReceived;

        chatService.ServiceUnavailable -=
            OnChatServiceUnavailable;
    }

    private void OnChatServiceUnavailable(
        string reason)
    {
        lastServiceError =
            reason ?? string.Empty;

        ChatAvailabilityChanged?.Invoke(
            false);
    }

    #endregion

    #region Shutdown

    private async void StartShutdownChat()
    {
        await ShutdownChatAsync();
    }

    public async Task ShutdownChatAsync()
    {
        if (chatService != null)
        {
            await chatService.ShutdownAsync();
        }

        bool hadConversations =
            conversations.Count > 0;

        conversations.Clear();
        activeConversationKey =
            string.Empty;

        if (hadConversations)
        {
            ActiveConversationChanged?.Invoke(
                null);
        }

        ChatAvailabilityChanged?.Invoke(
            false);
    }

    #endregion
}
