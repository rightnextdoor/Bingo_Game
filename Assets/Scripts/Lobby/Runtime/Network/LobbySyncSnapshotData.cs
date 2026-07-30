using System;

[Serializable]
public class LobbySyncSnapshotData
{
    #region Fields

    public string lobbyId;
    public long revision;
    public LobbyViewData lobbyViewData;
    public LobbyBoardCollectionData lobbyBoardData;

    #endregion

    #region Constructors

    public LobbySyncSnapshotData()
    {
        lobbyId = string.Empty;
        revision = 0;
        lobbyViewData = null;
        lobbyBoardData = null;
    }

    public LobbySyncSnapshotData(string lobbyId, long revision, LobbyViewData lobbyViewData, LobbyBoardCollectionData lobbyBoardData)
    {
        this.lobbyId = lobbyId ?? string.Empty;
        this.revision = revision;
        this.lobbyViewData = lobbyViewData;
        this.lobbyBoardData = lobbyBoardData;
    }

    #endregion
}
