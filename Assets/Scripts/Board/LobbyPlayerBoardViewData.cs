using System;

[Serializable]
public class LobbyPlayerBoardViewData
{
    #region Fields

    public string userId;
    public LobbyBoardData boardData;

    #endregion

    #region Constructors

    public LobbyPlayerBoardViewData()
    {
        userId = string.Empty;
        boardData = new LobbyBoardData();
    }

    public LobbyPlayerBoardViewData(string userId, LobbyBoardData boardData)
    {
        this.userId = userId ?? string.Empty;
        this.boardData = new LobbyBoardData(boardData);
    }

    #endregion
}
