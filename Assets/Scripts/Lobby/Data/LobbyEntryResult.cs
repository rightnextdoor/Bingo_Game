using System;

[Serializable]
public class LobbyEntryResult
{
    #region Fields

    public bool success;
    public LobbyEntryFailureType failureType;
    public string failureMessage;
    public string lobbyId;
    public LobbyViewData lobbyViewData;

    [NonSerialized] public Lobby localLobby;

    #endregion

    #region Constructors

    public LobbyEntryResult()
    {
        success = false;
        failureType = LobbyEntryFailureType.Unknown;
        failureMessage = string.Empty;
        lobbyId = string.Empty;
        lobbyViewData = null;
        localLobby = null;
    }

    #endregion

    #region Results

    public static LobbyEntryResult SucceededLocal(Lobby lobby)
    {
        if (lobby?.Controller == null)
        {
            return Failed(LobbyEntryFailureType.LobbyJoinFailed, "The lobby was not available.");
        }

        LobbyViewData lobbyViewData = lobby.Controller.BuildViewData();

        if (lobbyViewData == null)
        {
            return Failed(LobbyEntryFailureType.LobbyJoinFailed, "The lobby view was not available.");
        }

        return new LobbyEntryResult
        {
            success = true,
            failureType = LobbyEntryFailureType.None,
            failureMessage = string.Empty,
            lobbyId = lobby.GetLobbyId(),
            lobbyViewData = lobbyViewData,
            localLobby = lobby
        };
    }

    public static LobbyEntryResult Succeeded(string lobbyId, LobbyViewData lobbyViewData)
    {
        if (string.IsNullOrWhiteSpace(lobbyId) || lobbyViewData == null)
        {
            return Failed(LobbyEntryFailureType.LobbyJoinFailed, "The lobby data was not available.");
        }

        return new LobbyEntryResult
        {
            success = true,
            failureType = LobbyEntryFailureType.None,
            failureMessage = string.Empty,
            lobbyId = lobbyId,
            lobbyViewData = lobbyViewData,
            localLobby = null
        };
    }

    public static LobbyEntryResult Failed(LobbyEntryFailureType failureType, string failureMessage)
    {
        return new LobbyEntryResult
        {
            success = false,
            failureType = failureType,
            failureMessage = string.IsNullOrWhiteSpace(failureMessage) ? "The lobby could not be entered." : failureMessage,
            lobbyId = string.Empty,
            lobbyViewData = null,
            localLobby = null
        };
    }

    #endregion
}
