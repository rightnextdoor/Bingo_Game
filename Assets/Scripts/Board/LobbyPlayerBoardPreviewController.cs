using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbyPlayerBoardPreviewController : MonoBehaviour
{
    #region Fields

    private const int CellCount = 25;
    private const int FreeCellIndex = 12;

    [SerializeField] private List<PlayerListBoardPreviewCellController> cells = new List<PlayerListBoardPreviewCellController>();

    #endregion

    #region Board Display

    public void DisplayBoard(LobbyBoardData boardData)
    {
        DisplayBoard(boardData, null);
    }

    public void DisplayBoard(LobbyBoardData boardData, IReadOnlyList<int> markedCellIndices)
    {
        if (boardData == null || boardData.cellNumbers == null || boardData.cellNumbers.Count != CellCount || cells.Count != CellCount)
        {
            ClearBoard();
            return;
        }

        for (int i = 0; i < CellCount; i++)
        {
            PlayerListBoardPreviewCellController cell = cells[i];

            if (cell == null)
            {
                continue;
            }

            bool isFreeCell = boardData.usesFreeCell && i == FreeCellIndex;
            bool isMarked = isFreeCell || IsCellMarked(markedCellIndices, i);

            cell.DisplayValue(i, boardData.cellNumbers[i], isFreeCell, isMarked);
        }
    }

    public void UpdateMarkedCells(IReadOnlyList<int> markedCellIndices)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            PlayerListBoardPreviewCellController cell = cells[i];

            if (cell != null)
            {
                cell.SetMarked(cell.IsFree || IsCellMarked(markedCellIndices, i));
            }
        }
    }

    public void SetMarkedCell(int cellIndex, bool isMarked)
    {
        if (cellIndex < 0 || cellIndex >= cells.Count || cells[cellIndex] == null)
        {
            return;
        }

        PlayerListBoardPreviewCellController cell = cells[cellIndex];
        cell.SetMarked(cell.IsFree || isMarked);
    }

    public void ClearBoard()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            cells[i]?.Clear();
        }
    }

    #endregion

    #region Helpers

    private bool IsCellMarked(IReadOnlyList<int> markedCellIndices, int cellIndex)
    {
        if (markedCellIndices == null)
        {
            return false;
        }

        for (int i = 0; i < markedCellIndices.Count; i++)
        {
            if (markedCellIndices[i] == cellIndex)
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}