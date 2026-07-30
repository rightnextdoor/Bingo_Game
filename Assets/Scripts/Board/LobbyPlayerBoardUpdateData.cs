using System;

[Serializable]
public class LobbyPlayerBoardUpdateData
{
    #region Fields

    public string lobbyId;
    public long revision;
    public string userId;
    public LobbyBoardData boardData;

    #endregion

    #region Constructors

    public LobbyPlayerBoardUpdateData()
    {
        lobbyId = string.Empty;
        revision = 0;
        userId = string.Empty;
        boardData = new LobbyBoardData();
    }

    public LobbyPlayerBoardUpdateData(string lobbyId, string userId, LobbyBoardData boardData) : this(lobbyId, 0, userId, boardData)
    {
    }

    public LobbyPlayerBoardUpdateData(string lobbyId, long revision, string userId, LobbyBoardData boardData)
    {
        this.lobbyId = lobbyId ?? string.Empty;
        this.revision = revision;
        this.userId = userId ?? string.Empty;
        this.boardData = new LobbyBoardData(boardData);
    }

    #endregion
}
