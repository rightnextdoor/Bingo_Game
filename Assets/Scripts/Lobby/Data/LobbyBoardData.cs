using System;
using System.Collections.Generic;

[Serializable]
public class LobbyBoardData
{
    public BingoBallCountType ballCountType;
    public bool usesFreeCell;
    public List<int> cellNumbers;

    public LobbyBoardData()
    {
        ballCountType = BingoBallCountType.Ball75;
        usesFreeCell = true;
        cellNumbers = new List<int>();
    }

    public LobbyBoardData(BingoBallCountType ballCountType, bool usesFreeCell, List<int> cellNumbers)
    {
        this.ballCountType = ballCountType;
        this.usesFreeCell = usesFreeCell;
        this.cellNumbers = cellNumbers ?? new List<int>();
    }

    public LobbyBoardData(LobbyBoardData boardData) : this()
    {
        if (boardData == null)
        {
            return;
        }

        ballCountType = boardData.ballCountType;
        usesFreeCell = boardData.usesFreeCell;
        cellNumbers = boardData.cellNumbers != null
            ? new List<int>(boardData.cellNumbers)
            : new List<int>();
    }
}