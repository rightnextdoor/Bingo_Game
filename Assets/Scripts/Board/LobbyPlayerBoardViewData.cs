using System;

[Serializable]
public class LobbyPlayerBoardViewData
{
    public string userId;
    public LobbyBoardData boardData;

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
}