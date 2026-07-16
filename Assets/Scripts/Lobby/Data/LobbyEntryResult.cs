using System;

[Serializable]
public class LobbyEntryResult
{
    public bool success;

    public LobbyEntryFailureType failureType;
    public string failureMessage;

    public Lobby lobby;


    public LobbyEntryResult()
    {
        success = false;

        failureType = LobbyEntryFailureType.Unknown;
        failureMessage = string.Empty;

        lobby = null;
    }

    public static LobbyEntryResult Succeeded(Lobby lobby)
    {
        return new LobbyEntryResult
        {
            success = lobby != null,
            failureType = lobby != null
                ? LobbyEntryFailureType.None
                : LobbyEntryFailureType.LobbyJoinFailed,
            failureMessage = lobby != null
                ? string.Empty
                : "The lobby was not available.",
            lobby = lobby
        };
    }

    public static LobbyEntryResult Failed(
        LobbyEntryFailureType failureType,
        string failureMessage)
    {
        return new LobbyEntryResult
        {
            success = false,
            failureType = failureType,
            failureMessage =
                string.IsNullOrWhiteSpace(failureMessage)
                    ? "The lobby could not be entered."
                    : failureMessage,
            lobby = null
        };
    }
}