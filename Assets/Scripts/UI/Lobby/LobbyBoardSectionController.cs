using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LobbyBoardSectionController : MonoBehaviour
{
    #region Fields

    [SerializeField] private BingoBoardController boardController;
    [SerializeField] private Button rerollButton;

    public event Action RerollRequested;

    #endregion

    #region Unity Methods

    private void OnEnable()
    {
        if (rerollButton != null)
        {
            rerollButton.onClick.AddListener(OnRerollClicked);
        }
    }

    private void OnDisable()
    {
        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveListener(OnRerollClicked);
        }
    }

    #endregion

    #region Board

    public void DisplayBoard(LobbyBoardData boardData)
    {
        boardController?.DisplayBoard(boardData);
    }

    public void SetRerollInteractable(bool isInteractable)
    {
        if (rerollButton != null)
        {
            rerollButton.interactable = isInteractable;
        }
    }

    #endregion

    #region UI Events

    private void OnRerollClicked()
    {
        RerollRequested?.Invoke();
    }

    #endregion
}