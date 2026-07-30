using System;
using System.Collections.Generic;

[Serializable]
public class LobbyInitialSyncBatchData
{
    #region Fields

    public string lobbyId;
    public long revision;
    public bool resetState;
    public bool isFinalBatch;
    public LobbyViewData lobbyViewData;
    public List<LobbyPlayerViewData> players;
    public List<LobbyPlayerBoardViewData> boards;

    #endregion

    #region Constructors

    public LobbyInitialSyncBatchData()
    {
        lobbyId = string.Empty;
        revision = 0;
        resetState = false;
        isFinalBatch = false;
        lobbyViewData = null;
        players = new List<LobbyPlayerViewData>();
        boards = new List<LobbyPlayerBoardViewData>();
    }

    public LobbyInitialSyncBatchData(string lobbyId, long revision, bool resetState, bool isFinalBatch, LobbyViewData lobbyViewData, IEnumerable<LobbyPlayerViewData> players, IEnumerable<LobbyPlayerBoardViewData> boards) : this()
    {
        this.lobbyId = lobbyId ?? string.Empty;
        this.revision = revision;
        this.resetState = resetState;
        this.isFinalBatch = isFinalBatch;
        this.lobbyViewData = lobbyViewData;

        if (players != null)
        {
            foreach (LobbyPlayerViewData playerData in players)
            {
                if (playerData != null)
                {
                    this.players.Add(playerData);
                }
            }
        }

        if (boards != null)
        {
            foreach (LobbyPlayerBoardViewData boardData in boards)
            {
                if (boardData != null)
                {
                    this.boards.Add(new LobbyPlayerBoardViewData(boardData.userId, boardData.boardData));
                }
            }
        }
    }

    #endregion
}
