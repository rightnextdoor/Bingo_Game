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
        bool isFromCurrentUser)
    {
        this.messageId = messageId ?? string.Empty;
        this.providerMessageId = providerMessageId ?? string.Empty;
        this.conversationId = conversationId ?? string.Empty;
        this.conversationType = conversationType;
        this.senderUserId = senderUserId ?? string.Empty;
        this.senderPlayerName = senderPlayerName ?? string.Empty;
        this.senderIconId = senderIconId ?? string.Empty;
        this.providerSenderId = providerSenderId ?? string.Empty;
        this.message = message ?? string.Empty;
        this.timestampUnixMilliseconds = timestampUnixMilliseconds;
        this.isFromCurrentUser = isFromCurrentUser;
    }
}
