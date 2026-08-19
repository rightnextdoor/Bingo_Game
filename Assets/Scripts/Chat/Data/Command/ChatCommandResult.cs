public class ChatCommandResult
{
    public bool handled;
    public bool success;
    public string responseMessage;

    public static ChatCommandResult NotHandled()
    {
        return new ChatCommandResult
        {
            handled = false,
            success = false,
            responseMessage = string.Empty
        };
    }

    public static ChatCommandResult Succeeded(string responseMessage = null)
    {
        return new ChatCommandResult
        {
            handled = true,
            success = true,
            responseMessage = responseMessage ?? string.Empty
        };
    }

    public static ChatCommandResult Failed(string responseMessage)
    {
        return new ChatCommandResult
        {
            handled = true,
            success = false,
            responseMessage = responseMessage ?? string.Empty
        };
    }
}
