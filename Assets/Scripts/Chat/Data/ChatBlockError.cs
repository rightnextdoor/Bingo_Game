public enum ChatBlockErrorType
{
    None,
    PlayerAlreadyBlocked,
    PlayerNotFound,
    CannotBlockSelf
}

public static class ChatBlockError
{
    public static string GetMessage(ChatBlockErrorType errorType)
    {
        switch (errorType)
        {
            case ChatBlockErrorType.PlayerAlreadyBlocked:
                return "Player is already blocked.";

            case ChatBlockErrorType.PlayerNotFound:
                return "Player not found.";

            case ChatBlockErrorType.CannotBlockSelf:
                return "You cannot block yourself.";

            default:
                return string.Empty;
        }
    }
}
