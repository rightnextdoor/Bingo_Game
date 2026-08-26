using System;

[Serializable]
public class ChatMessageData
{
    public string messageId;
    public string providerMessageId;

    public string conversationId;
    public ChatConversationType conversationType;

    public string senderUserId;
    public string senderPlayerName;
    public string senderIconId;
    public string providerSenderId;

    public bool isPrivate;
    public string recipientUserId;
    public bool isLocalSystemMessage;

    public string message;
    public long timestampUnixMilliseconds;
    public bool isFromCurrentUser;

    public ChatMessageData()
    {
        messageId = string.Empty;
        providerMessageId = string.Empty;
        conversationId = string.Empty;
        senderUserId = string.Empty;
        senderPlayerName = string.Empty;
        senderIconId = string.Empty;
        providerSenderId = string.Empty;
        isPrivate = false;
        recipientUserId = string.Empty;
        isLocalSystemMessage = false;
        message = string.Empty;
    }

    public ChatMessageData(
        string messageId,
        string providerMessageId,
        string conversationId,
        ChatConversationType conversationType,
        string senderUserId,
        string senderPlayerName,
        string senderIconId,
        string providerSenderId,
        string message,
        long timestampUnixMilliseconds,
        bool isFromCurrentUser,
        bool isPrivate = false,
        string recipientUserId = null,
        bool isLocalSystemMessage = false)
    {
        this.messageId = messageId ?? string.Empty;
        this.providerMessageId = providerMessageId ?? string.Empty;
        this.conversationId = conversationId ?? string.Empty;
        this.conversationType = conversationType;
        this.senderUserId = senderUserId ?? string.Empty;
        this.senderPlayerName = senderPlayerName ?? string.Empty;
        this.senderIconId = senderIconId ?? string.Empty;
        this.providerSenderId = providerSenderId ?? string.Empty;
        this.isPrivate = isPrivate;
        this.recipientUserId = recipientUserId ?? string.Empty;
        this.isLocalSystemMessage = isLocalSystemMessage;
        this.message = message ?? string.Empty;
        this.timestampUnixMilliseconds = timestampUnixMilliseconds;
        this.isFromCurrentUser = isFromCurrentUser;
    }
}
