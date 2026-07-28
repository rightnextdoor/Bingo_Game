using System;

[Serializable]
public class LobbyExitNotification
{
    public string lobbyId;

    public LobbyPlayerExitReason exitReason;
    public LobbyCloseReason closeReason;

    public LobbyEntryFailureType failureType;
    public string message;

    public LobbyExitNotification()
    {
        lobbyId = string.Empty;

        exitReason = LobbyPlayerExitReason.LobbyClosed;
        closeReason = LobbyCloseReason.None;

        failureType = LobbyEntryFailureType.Unknown;
        message = string.Empty;
    }

    public static LobbyExitNotification Kicked(string lobbyId)
    {
        return new LobbyExitNotification
        {
            lobbyId = lobbyId ?? string.Empty,
            exitReason = LobbyPlayerExitReason.Kicked,
            closeReason = LobbyCloseReason.None,
            failureType = LobbyEntryFailureType.KickedFromLobby,
            message = "You were removed from the lobby by the host."
        };
    }

    public static LobbyExitNotification LobbyClosed(
        string lobbyId,
        LobbyCloseReason closeReason)
    {
        string message = closeReason == LobbyCloseReason.HostLeft
            ? "The lobby was closed because the host left."
            : "The lobby was closed.";

        return new LobbyExitNotification
        {
            lobbyId = lobbyId ?? string.Empty,
            exitReason = LobbyPlayerExitReason.LobbyClosed,
            closeReason = closeReason,
            failureType = LobbyEntryFailureType.LobbyClosed,
            message = message
        };
    }

    public static LobbyExitNotification LobbyStarted(string lobbyId)
    {
        return new LobbyExitNotification
        {
            lobbyId = lobbyId ?? string.Empty,
            exitReason = LobbyPlayerExitReason.LobbyStarted,
            closeReason = LobbyCloseReason.None,
            failureType = LobbyEntryFailureType.LobbyStarted,
            message = "The lobby has already started."
        };
    }

    public static LobbyExitNotification JoinTimedOut(string lobbyId)
    {
        return new LobbyExitNotification
        {
            lobbyId = lobbyId ?? string.Empty,
            exitReason = LobbyPlayerExitReason.JoinTimedOut,
            closeReason = LobbyCloseReason.None,
            failureType = LobbyEntryFailureType.JoinTimedOut,
            message = "The lobby took too long to finish loading."
        };
    }

    public static LobbyExitNotification ConnectionLost(string lobbyId)
    {
        return new LobbyExitNotification
        {
            lobbyId = lobbyId ?? string.Empty,
            exitReason = LobbyPlayerExitReason.Disconnected,
            closeReason = LobbyCloseReason.None,
            failureType = LobbyEntryFailureType.ConnectionLost,
            message = "The connection to the lobby was lost."
        };
    }
}
