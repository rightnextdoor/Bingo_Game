public class ChatHistoryRequest
{
    public ChatConversationReference conversation;
    public int maximumMessages;

    public ChatHistoryRequest(ChatConversationReference conversation, int maximumMessages)
    {
        this.conversation = conversation;
        this.maximumMessages = maximumMessages;
    }
}
