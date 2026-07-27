using System;
using System.Collections.Generic;

[Serializable]
public class LobbyBoardCollectionData
{
    #region Fields

    public string lobbyId;
    public List<LobbyPlayerBoardViewData> boards;

    #endregion

    #region Constructors

    public LobbyBoardCollectionData()
    {
        lobbyId = string.Empty;
        boards = new List<LobbyPlayerBoardViewData>();
    }

    public LobbyBoardCollectionData(string lobbyId, IEnumerable<LobbyPlayerBoardViewData> boards) : this()
    {
        this.lobbyId = lobbyId ?? string.Empty;

        if (boards == null)
        {
            return;
        }

        foreach (LobbyPlayerBoardViewData boardData in boards)
        {
            if (boardData != null)
            {
                this.boards.Add(new LobbyPlayerBoardViewData(boardData.userId, boardData.boardData));
            }
        }
    }

    #endregion
}
