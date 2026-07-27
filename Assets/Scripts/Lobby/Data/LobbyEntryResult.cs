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
    public LobbyBoardCollectionData lobbyBoardData;

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
        lobbyBoardData = null;
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
        LobbyBoardCollectionData lobbyBoardData = lobby.Controller.BuildPlayerBoardCollectionData();

        if (lobbyViewData == null || lobbyBoardData == null)
        {
            return Failed(LobbyEntryFailureType.LobbyJoinFailed, "The lobby state was not available.");
        }

        return new LobbyEntryResult
        {
            success = true,
            failureType = LobbyEntryFailureType.None,
            failureMessage = string.Empty,
            lobbyId = lobby.GetLobbyId(),
            lobbyViewData = lobbyViewData,
            lobbyBoardData = lobbyBoardData,
            localLobby = lobby
        };
    }

    public static LobbyEntryResult Succeeded(string lobbyId, LobbyViewData lobbyViewData, LobbyBoardCollectionData lobbyBoardData)
    {
        if (string.IsNullOrWhiteSpace(lobbyId) || lobbyViewData == null || lobbyBoardData == null)
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
            lobbyBoardData = lobbyBoardData,
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
            lobbyBoardData = null,
            localLobby = null
        };
    }

    #endregion
}
