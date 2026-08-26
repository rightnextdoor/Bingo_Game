public class ChatSendResult
{
    public bool success;
    public string failureMessage;

    public static ChatSendResult Succeeded()
    {
        return new ChatSendResult
        {
            success = true,
            failureMessage = string.Empty
        };
    }

    public static ChatSendResult Failed(string message)
    {
        return new ChatSendResult
        {
            success = false,
            failureMessage = message ?? string.Empty
        };
    }
}
