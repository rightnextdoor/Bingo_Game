public enum ChatErrorType
{
    None,
    EmptyMessage,
    ChatUnavailable,
    SendFailed
}

public static class ChatError
{
    public static ChatErrorType CheckInput(string input, ChatManager chatManager)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return ChatErrorType.EmptyMessage;
        }

        if (chatManager == null)
        {
            return ChatErrorType.ChatUnavailable;
        }

        return ChatErrorType.None;
    }

    public static string GetMessage(ChatErrorType errorType)
    {
        switch (errorType)
        {
            case ChatErrorType.EmptyMessage:
                return "The chat message is empty.";

            case ChatErrorType.ChatUnavailable:
                return "Chat is not available.";

            case ChatErrorType.SendFailed:
                return "The chat message could not be sent.";

            default:
                return string.Empty;
        }
    }
}
