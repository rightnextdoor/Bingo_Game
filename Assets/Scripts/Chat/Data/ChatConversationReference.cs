using System;

[Serializable]
public class ChatConversationReference
{
    public string conversationId;
    public ChatConversationType conversationType;

    public bool IsValid => !string.IsNullOrWhiteSpace(conversationId);
    public string Key => GetKey(conversationId, conversationType);

    public ChatConversationReference()
    {
        conversationId = string.Empty;
        conversationType = ChatConversationType.Session;
    }

    public ChatConversationReference(string conversationId, ChatConversationType conversationType)
    {
        this.conversationId = conversationId ?? string.Empty;
        this.conversationType = conversationType;
    }

    public static string GetKey(string conversationId, ChatConversationType conversationType)
    {
        return $"{(int)conversationType}:{conversationId ?? string.Empty}";
    }
}
