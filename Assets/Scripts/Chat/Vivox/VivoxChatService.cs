using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;

[DefaultExecutionOrder(-1060)]
[DisallowMultipleComponent]
public class VivoxChatService : MonoBehaviour, IChatService
{
    #region Metadata

    [Serializable]
    private class BingoChatMessageMetadata
    {
        public string type = MetadataType;
        public string userId = string.Empty;
        public string playerName = string.Empty;
        public string iconId = string.Empty;
    }

    private const string MetadataType = "BingoChatMessage";

    #endregion

    #region Fields

    private OnlineServicesRoot onlineServicesRoot;

    private readonly Dictionary<string, ChatConversationReference> conversationByChannelName =
        new Dictionary<string, ChatConversationReference>();

    private readonly Dictionary<string, string> channelNameByConversationKey =
        new Dictionary<string, string>();

    private ChatParticipantData localParticipant;

    private bool isAdapterReady;
    private bool eventsSubscribed;
    private bool isShuttingDown;

    private string lastError = string.Empty;

    public bool IsReady =>
        isAdapterReady &&
        VivoxService.Instance != null &&
        VivoxService.Instance.InitializationState == VivoxInitializationState.Initialized &&
        VivoxService.Instance.IsLoggedIn;

    public string LastError => lastError;

    public event Action<ChatMessageData> MessageReceived;
    public event Action<string> ServiceUnavailable;

    #endregion

    #region Unity Methods

    private void OnDestroy()
    {
        UnsubscribeFromVivoxEvents();
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

        if (onlineServicesRoot == null ||
            onlineServicesRoot != GetComponent<OnlineServicesRoot>() ||
            !onlineServicesRoot.IsPrimaryInstance)
        {
            return false;
        }

        isAdapterReady = true;
        return true;
    }

    public async Task<bool> EnsureReadyAsync(ChatParticipantData participant)
    {
        if (!isAdapterReady || participant == null || !participant.IsValid)
        {
            return false;
        }

        UpdateLocalParticipant(participant);
        lastError = string.Empty;

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
                await VivoxService.Instance.InitializeAsync();
            }

            if (!VivoxService.Instance.IsLoggedIn)
            {
                LoginOptions loginOptions = new LoginOptions
                {
                    DisplayName = localParticipant.playerName
                };

                await VivoxService.Instance.LoginAsync(loginOptions);
            }

            if (!VivoxService.Instance.IsLoggedIn)
            {
                SetUnavailable("Vivox did not create a logged-in session.");
                return false;
            }

            lastError = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
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

        string conversationKey = conversation.Key;

        if (channelNameByConversationKey.ContainsKey(conversationKey))
        {
            return true;
        }

        string channelName = BuildChannelName(conversation);

        try
        {
            await VivoxService.Instance.JoinGroupChannelAsync(
                channelName,
                ChatCapability.TextOnly);

            channelNameByConversationKey[conversationKey] = channelName;
            conversationByChannelName[channelName] =
                new ChatConversationReference(conversation.conversationId, conversation.conversationType);

            return true;
        }
        catch (Exception exception)
        {
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
            return true;
        }

        try
        {
            if (VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn)
            {
                await VivoxService.Instance.LeaveChannelAsync(channelName);
            }

            channelNameByConversationKey.Remove(conversationKey);
            conversationByChannelName.Remove(channelName);

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

    public async Task<ChatSendResult> SendMessageAsync(
        ChatConversationReference conversation,
        string message)
    {
        if (!IsReady)
        {
            return ChatSendResult.Failed(
                string.IsNullOrWhiteSpace(lastError)
                    ? "Chat is not available."
                    : lastError);
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
                Metadata = BuildMessageMetadata()
            };

            await VivoxService.Instance.SendChannelTextMessageAsync(
                channelName,
                message,
                messageOptions);

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

        if (!IsReady ||
            request?.conversation == null ||
            !request.conversation.IsValid ||
            request.maximumMessages <= 0)
        {
            return history;
        }

        if (!channelNameByConversationKey.TryGetValue(
                request.conversation.Key,
                out string channelName))
        {
            return history;
        }

        try
        {
            ReadOnlyCollection<VivoxMessage> messages =
                await VivoxService.Instance.GetChannelTextMessageHistoryAsync(
                    channelName,
                    request.maximumMessages);

            if (messages == null)
            {
                return history;
            }

            for (int i = messages.Count - 1; i >= 0; i--)
            {
                ChatMessageData chatMessage =
                    ConvertMessage(messages[i], request.conversation);

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
        if (vivoxMessage == null ||
            string.IsNullOrWhiteSpace(vivoxMessage.ChannelName) ||
            !conversationByChannelName.TryGetValue(
                vivoxMessage.ChannelName,
                out ChatConversationReference conversation))
        {
            return;
        }

        ChatMessageData message =
            ConvertMessage(vivoxMessage, conversation);

        if (message != null)
        {
            MessageReceived?.Invoke(message);
        }
    }

    private ChatMessageData ConvertMessage(
        VivoxMessage vivoxMessage,
        ChatConversationReference conversation)
    {
        if (vivoxMessage == null || conversation == null)
        {
            return null;
        }

        BingoChatMessageMetadata metadata =
            ParseMessageMetadata(vivoxMessage.Metadata);

        string senderUserId =
            metadata != null
                ? metadata.userId
                : string.Empty;

        string senderPlayerName =
            metadata != null &&
            !string.IsNullOrWhiteSpace(metadata.playerName)
                ? metadata.playerName
                : vivoxMessage.SenderDisplayName;

        string senderIconId =
            metadata != null
                ? metadata.iconId
                : string.Empty;

        DateTime receivedTime =
            vivoxMessage.ReceivedTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(vivoxMessage.ReceivedTime, DateTimeKind.Utc)
                : vivoxMessage.ReceivedTime.ToUniversalTime();

        long timestampUnixMilliseconds =
            new DateTimeOffset(receivedTime)
                .ToUnixTimeMilliseconds();

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
            vivoxMessage.FromSelf);
    }

    private string BuildMessageMetadata()
    {
        if (localParticipant == null)
        {
            return string.Empty;
        }

        BingoChatMessageMetadata metadata =
            new BingoChatMessageMetadata
            {
                userId = localParticipant.userId,
                playerName = localParticipant.playerName,
                iconId = localParticipant.iconId
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
            BingoChatMessageMetadata metadata =
                JsonUtility.FromJson<BingoChatMessageMetadata>(
                    metadataJson);

            if (metadata == null || metadata.type != MetadataType)
            {
                return null;
            }

            return metadata;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Vivox Events

    private void SubscribeToVivoxEvents()
    {
        if (eventsSubscribed || VivoxService.Instance == null)
        {
            return;
        }

        VivoxService.Instance.ChannelMessageReceived += OnChannelMessageReceived;
        VivoxService.Instance.ConnectionFailedToRecover += OnConnectionFailedToRecover;
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
        VivoxService.Instance.ConnectionFailedToRecover -= OnConnectionFailedToRecover;
        VivoxService.Instance.LoggedOut -= OnVivoxLoggedOut;

        eventsSubscribed = false;
    }

    private void OnConnectionFailedToRecover()
    {
        SetUnavailable("Vivox could not recover the chat connection.");
    }

    private void OnVivoxLoggedOut()
    {
        if (isShuttingDown)
        {
            return;
        }

        SetUnavailable("Vivox chat was logged out.");
    }

    private void SetUnavailable(string reason)
    {
        lastError =
            string.IsNullOrWhiteSpace(reason)
                ? "Chat is unavailable."
                : reason;

        ServiceUnavailable?.Invoke(lastError);
    }

    #endregion

    #region Shutdown

    public async Task ShutdownAsync()
    {
        if (!isAdapterReady)
        {
            return;
        }

        isShuttingDown = true;

        try
        {
            if (VivoxService.Instance != null &&
                VivoxService.Instance.IsLoggedIn)
            {
                await VivoxService.Instance.LeaveAllChannelsAsync();
                await VivoxService.Instance.LogoutAsync();
            }
        }
        catch (Exception exception)
        {
            lastError = exception.Message;
        }
        finally
        {
            channelNameByConversationKey.Clear();
            conversationByChannelName.Clear();
            isShuttingDown = false;
        }
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

        string source =
            $"{(int)conversation.conversationType}:{conversation.conversationId}";

        return prefix + GetSha256Hex(source);
    }

    private string GetSha256Hex(string value)
    {
        using SHA256 sha256 = SHA256.Create();

        byte[] bytes =
            Encoding.UTF8.GetBytes(
                value ?? string.Empty);

        byte[] hash =
            sha256.ComputeHash(bytes);

        StringBuilder builder =
            new StringBuilder(hash.Length * 2);

        for (int i = 0; i < hash.Length; i++)
        {
            builder.Append(
                hash[i].ToString("x2"));
        }

        return builder.ToString();
    }

    #endregion
}
