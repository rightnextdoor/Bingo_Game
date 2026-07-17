using System;

[Serializable]
public class LobbyExitResult
{
    public bool success;

    public string userId;
    public LobbyPlayerExitReason exitReason;

    public bool wasHost;
    public int remainingPlayerCount;

    public bool shouldCloseLobby;
    public LobbyCloseReason closeReason;

    public string failureMessage;

    public LobbyExitResult()
    {
        success = false;

        userId = string.Empty;
        exitReason = LobbyPlayerExitReason.VoluntaryLeave;

        wasHost = false;
        remainingPlayerCount = 0;

        shouldCloseLobby = false;
        closeReason = LobbyCloseReason.None;

        failureMessage = string.Empty;
    }

    public static LobbyExitResult Succeeded(
        string userId,
        LobbyPlayerExitReason exitReason,
        bool wasHost,
        int remainingPlayerCount,
        bool shouldCloseLobby,
        LobbyCloseReason closeReason)
    {
        return new LobbyExitResult
        {
            success = true,
            userId = userId ?? string.Empty,
            exitReason = exitReason,
            wasHost = wasHost,
            remainingPlayerCount = remainingPlayerCount,
            shouldCloseLobby = shouldCloseLobby,
            closeReason = closeReason,
            failureMessage = string.Empty
        };
    }

    public static LobbyExitResult Failed(
        string userId,
        LobbyPlayerExitReason exitReason,
        string failureMessage)
    {
        return new LobbyExitResult
        {
            success = false,
            userId = userId ?? string.Empty,
            exitReason = exitReason,
            wasHost = false,
            remainingPlayerCount = 0,
            shouldCloseLobby = false,
            closeReason = LobbyCloseReason.None,
            failureMessage = string.IsNullOrWhiteSpace(failureMessage)
                ? "The player could not leave the lobby."
                : failureMessage
        };
    }
}
