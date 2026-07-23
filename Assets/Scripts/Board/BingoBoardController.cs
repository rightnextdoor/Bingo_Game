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

    private readonly List<int> markedCellIndices = new List<int>();

    private LobbyBoardData currentBoardData;

    public LobbyBoardData CurrentBoardData => currentBoardData;
    public IReadOnlyList<int> MarkedCellIndices => markedCellIndices;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        SubscribeToCells();
    }

    private void OnDisable()
    {
        UnsubscribeFromCells();
    }

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

        currentBoardData = boardData;
        markedCellIndices.Clear();

        for (int i = 0; i < CellCount; i++)
        {
            BingoBoardCellController cell = cells[i];

            if (cell == null)
                continue;

            bool isFree = boardData.usesFreeCell && i == FreeCellIndex;

            cell.DisplayValue(i, boardData.cellNumbers[i], isFree);

            if (isFree)
                markedCellIndices.Add(i);
        }

        return true;
    }

    public void ClearBoard()
    {
        currentBoardData = null;
        markedCellIndices.Clear();

        for (int i = 0; i < cells.Count; i++)
            cells[i]?.Clear();
    }

    #endregion

    #region Marked Cells

    private void OnCellPressed(int cellIndex)
    {
        BingoBoardCellController cell = GetCell(cellIndex);

        if (cell == null || cell.IsFree)
            return;

        if (markedCellIndices.Contains(cellIndex))
        {
            markedCellIndices.Remove(cellIndex);
            cell.SetMarked(false);
            return;
        }

        markedCellIndices.Add(cellIndex);
        cell.SetMarked(true);
    }

    public List<int> GetMarkedCellIndicesSnapshot()
    {
        return new List<int>(markedCellIndices);
    }

    public void ClearMarks()
    {
        markedCellIndices.Clear();

        for (int i = 0; i < cells.Count; i++)
        {
            BingoBoardCellController cell = cells[i];

            if (cell == null)
                continue;

            cell.SetMarked(false);

            if (cell.IsFree)
                markedCellIndices.Add(cell.CellIndex);
        }
    }

    //for simulation
    public bool SetCellMarked(int cellIndex, bool marked)
    {
        BingoBoardCellController cell = GetCell(cellIndex);

        if (cell == null || cell.CellIndex < 0)
            return false;

        if (cell.IsFree)
        {
            if (!markedCellIndices.Contains(cellIndex))
                markedCellIndices.Add(cellIndex);

            cell.SetMarked(true);
            return true;
        }

        if (marked)
        {
            if (!markedCellIndices.Contains(cellIndex))
                markedCellIndices.Add(cellIndex);
        }
        else
        {
            markedCellIndices.Remove(cellIndex);
        }

        cell.SetMarked(marked);
        return true;
    }

    public void SetMarkedCells(IReadOnlyCollection<int> cellIndices)
    {
        ClearMarks();

        if (cellIndices == null)
            return;

        foreach (int cellIndex in cellIndices)
            SetCellMarked(cellIndex, true);
    }

    #endregion

    #region Check Highlight

    public void ShowCheckHighlight(int cellIndex, Color color)
    {
        BingoBoardCellController cell = GetCell(cellIndex);
        cell?.ShowCheckHighlight(color);
    }

    public void ClearCheckHighlight(int cellIndex)
    {
        BingoBoardCellController cell = GetCell(cellIndex);
        cell?.ClearCheckHighlight();
    }

    public void ClearCheckHighlights()
    {
        for (int i = 0; i < cells.Count; i++)
            cells[i]?.ClearCheckHighlight();
    }

    #endregion

    #region Interaction

    public void SetInteractable(bool interactable)
    {
        if (boardCanvasGroup == null)
            return;

        boardCanvasGroup.interactable = interactable;
        boardCanvasGroup.blocksRaycasts = interactable;
    }

    #endregion

    #region Cell Setup

    private void SubscribeToCells()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] != null)
                cells[i].CellPressed += OnCellPressed;
        }
    }

    private void UnsubscribeFromCells()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] != null)
                cells[i].CellPressed -= OnCellPressed;
        }
    }

    private BingoBoardCellController GetCell(int cellIndex)
    {
        if (cellIndex < 0 || cellIndex >= cells.Count)
            return null;

        return cells[cellIndex];
    }

    #endregion
}