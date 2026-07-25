using System;

[Serializable]
public class LobbyPlayerBoardUpdateData
{
    public string lobbyId;
    public string userId;
    public LobbyBoardData boardData;

    public LobbyPlayerBoardUpdateData()
    {
        lobbyId = string.Empty;
        userId = string.Empty;
        boardData = new LobbyBoardData();
    }

    public LobbyPlayerBoardUpdateData(string lobbyId, string userId, LobbyBoardData boardData)
    {
        this.lobbyId = lobbyId ?? string.Empty;
        this.userId = userId ?? string.Empty;
        this.boardData = new LobbyBoardData(boardData);
    }
}