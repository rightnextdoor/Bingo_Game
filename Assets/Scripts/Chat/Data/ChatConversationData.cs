using System;
using System.Collections.Generic;

[Serializable]
public class ChatConversationData
{
    public string conversationId;
    public ChatConversationType conversationType;

    public List<ChatParticipantData> participants = new List<ChatParticipantData>();
    public List<ChatMessageData> messages = new List<ChatMessageData>();

    public int unreadCount;

    public ChatConversationReference Reference => new ChatConversationReference(conversationId, conversationType);
    public string Key => ChatConversationReference.GetKey(conversationId, conversationType);

    public ChatConversationData()
    {
        conversationId = string.Empty;
        conversationType = ChatConversationType.Session;
    }

    public ChatConversationData(ChatConversationReference conversation)
    {
        conversationId = conversation?.conversationId ?? string.Empty;
        conversationType = conversation?.conversationType ?? ChatConversationType.Session;
    }
}
