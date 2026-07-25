using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LobbyBoardSectionController : MonoBehaviour
{
    #region Fields

    [SerializeField] private BingoBoardController boardController;

    [Header("Controls")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private Button readyButton;

    public event Action RerollRequested;
    public event Action ReadyRequested;

    #endregion

    #region Unity Methods

    private void OnEnable()
    {
        if (rerollButton != null)
        {
            rerollButton.onClick.AddListener(OnRerollClicked);
        }

        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyClicked);
        }
    }

    private void OnDisable()
    {
        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveListener(OnRerollClicked);
        }

        if (readyButton != null)
        {
            readyButton.onClick.RemoveListener(OnReadyClicked);
        }
    }

    #endregion

    #region Board

    public void DisplayBoard(LobbyBoardData boardData)
    {
        boardController?.DisplayBoard(boardData);
    }

    public void SetBoardInteractable(bool interactable)
    {
        boardController?.SetInteractable(interactable);
    }

    #endregion

    #region Controls

    public void SetRerollInteractable(bool interactable)
    {
        if (rerollButton != null)
        {
            rerollButton.interactable = interactable;
        }
    }

    public void SetReadyInteractable(bool interactable)
    {
        if (readyButton != null)
        {
            readyButton.interactable = interactable;
        }
    }

    #endregion

    #region UI Events

    private void OnRerollClicked()
    {
        RerollRequested?.Invoke();
    }

    private void OnReadyClicked()
    {
        ReadyRequested?.Invoke();
    }

    #endregion
}