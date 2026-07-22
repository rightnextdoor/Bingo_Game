using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BingoBoardController : MonoBehaviour
{
    #region Fields

    private const int CellCount = 25;
    private const int FreeCellIndex = 12;

    [SerializeField] private CanvasGroup boardCanvasGroup;
    [SerializeField] private List<BingoBoardCellController> cells = new List<BingoBoardCellController>();

    #endregion

    #region Board Display

    public bool DisplayBoard(LobbyBoardData boardData)
    {
        if (boardData == null ||
            boardData.cellNumbers == null ||
            boardData.cellNumbers.Count != CellCount ||
            cells.Count != CellCount)
        {
            ClearBoard();
            return false;
        }

        for (int i = 0; i < CellCount; i++)
        {
            BingoBoardCellController cell = cells[i];

            if (cell == null)
            {
                continue;
            }

            bool isFree = boardData.usesFreeCell && i == FreeCellIndex;
            cell.DisplayValue(i, boardData.cellNumbers[i], isFree);
        }

        return true;
    }

    public void ClearBoard()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            cells[i]?.Clear();
        }
    }

    #endregion

    #region Interaction

    public void SetInteractable(bool interactable)
    {
        if (boardCanvasGroup == null)
        {
            return;
        }

        boardCanvasGroup.interactable = interactable;
        boardCanvasGroup.blocksRaycasts = interactable;
    }

    #endregion

}