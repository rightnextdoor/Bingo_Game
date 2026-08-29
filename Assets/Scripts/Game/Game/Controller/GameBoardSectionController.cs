using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameBoardSectionController : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private BingoBoardController boardController;

    [Header("Controls")]
    [SerializeField] private Button bingoButton;

    #endregion

    #region Events

    public event Action<LobbyBoardData, IReadOnlyList<int>> BingoRequested;
    public event Action<int, bool> MarkedCellChanged;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (boardController != null)
        {
            boardController.MarkedCellChanged -= OnMarkedCellChanged;
            boardController.MarkedCellChanged += OnMarkedCellChanged;
        }

        if (bingoButton != null)
        {
            bingoButton.onClick.AddListener(OnBingoClicked);
        }
    }

    private void OnDisable()
    {
        if (boardController != null)
        {
            boardController.MarkedCellChanged -= OnMarkedCellChanged;
        }

        if (bingoButton != null)
        {
            bingoButton.onClick.RemoveListener(OnBingoClicked);
        }
    }

    #endregion

    #region Board

    public bool DisplayBoard(LobbyBoardData boardData)
    {
        if (boardController == null)
        {
            return false;
        }

        if (boardData == null)
        {
            boardController.ClearBoard();
            return false;
        }

        if (AreBoardsEqual(boardController.CurrentBoardData, boardData))
        {
            return true;
        }

        return boardController.DisplayBoard(new LobbyBoardData(boardData));
    }

    public void ClearBoard()
    {
        boardController?.ClearBoard();
    }

    public void SetBoardInteractable(bool interactable)
    {
        boardController?.SetInteractable(interactable);
    }

    #endregion

    #region Controls

    public void SetBingoInteractable(bool interactable)
    {
        if (bingoButton != null)
        {
            bingoButton.interactable = interactable;
        }
    }

    #endregion

    #region UI Events

    private void OnMarkedCellChanged(int cellIndex, bool isMarked)
    {
        MarkedCellChanged?.Invoke(cellIndex, isMarked);
    }

    private void OnBingoClicked()
    {
        LobbyBoardData boardData = boardController?.CurrentBoardData;

        if (boardData == null)
        {
            return;
        }

        BingoRequested?.Invoke(
            new LobbyBoardData(boardData),
            boardController.GetMarkedCellIndicesSnapshot());
    }

    #endregion

    #region Helpers

    private bool AreBoardsEqual(LobbyBoardData currentBoard, LobbyBoardData nextBoard)
    {
        if (currentBoard == null || nextBoard == null)
        {
            return false;
        }

        if (currentBoard.ballCountType != nextBoard.ballCountType ||
            currentBoard.usesFreeCell != nextBoard.usesFreeCell ||
            currentBoard.cellNumbers == null ||
            nextBoard.cellNumbers == null ||
            currentBoard.cellNumbers.Count != nextBoard.cellNumbers.Count)
        {
            return false;
        }

        for (int i = 0; i < currentBoard.cellNumbers.Count; i++)
        {
            if (currentBoard.cellNumbers[i] != nextBoard.cellNumbers[i])
            {
                return false;
            }
        }

        return true;
    }

    #endregion
}
