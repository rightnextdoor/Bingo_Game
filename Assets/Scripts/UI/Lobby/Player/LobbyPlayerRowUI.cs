using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LobbyPlayerRowUI : MonoBehaviour, IPointerClickHandler
{
    #region Fields

    [Header("Selection")]
    [SerializeField] private GameObject selectionHighlight;

    [Header("Player")]
    [SerializeField] private Image playerIconImage;
    [SerializeField] private TMP_Text playerNameText;

    [Header("Board")]
    [SerializeField] private LobbyPlayerBoardPreviewController boardPreviewController;

    [Header("Status")]
    [SerializeField] private Image botIconImage;
    [SerializeField] private Image readyCheckmarkImage;

    [Header("Controls")]
    [SerializeField] private Button kickButton;

    private string userId = string.Empty;

    private Action<string> rowClicked;
    private Action<string> kickRequested;

    public string UserId => userId;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        ResetVisualState();
    }

    private void OnEnable()
    {
        if (kickButton != null)
        {
            kickButton.onClick.RemoveListener(OnKickClicked);
            kickButton.onClick.AddListener(OnKickClicked);
        }
    }

    private void OnDisable()
    {
        if (kickButton != null)
        {
            kickButton.onClick.RemoveListener(OnKickClicked);
        }
    }

    #endregion

    #region Setup

    public void Setup(PlayerListPlayerData playerData, Action<string> onRowClicked, Action<string> onKickRequested, bool isHighlighted, bool refreshBoard = true)
    {
        if (playerData == null)
        {
            return;
        }

        userId = playerData.userId ?? string.Empty;
        rowClicked = onRowClicked;
        kickRequested = onKickRequested;

        SetPlayerIcon(playerData.iconId);
        SetPlayerName(playerData.playerName, playerData.userId);

        if (refreshBoard)
        {
            boardPreviewController?.DisplayBoard(playerData.boardData, playerData.markedCellIndices);
        }

        SetStatusIcon(botIconImage, UIIconType.Bot, playerData.showBotIcon);
        SetStatusIcon(readyCheckmarkImage, UIIconType.LobbyCheckmark, playerData.showReadyIcon && playerData.isReady);

        SetKickButtonState(playerData.canKick);
        SetHighlighted(isHighlighted);
    }

    private void ResetVisualState()
    {
        userId = string.Empty;
        rowClicked = null;
        kickRequested = null;

        if (playerIconImage != null)
        {
            playerIconImage.sprite = null;
            playerIconImage.enabled = false;
        }

        if (playerNameText != null)
        {
            playerNameText.text = string.Empty;
        }

        SetImageActive(botIconImage, false);
        SetImageActive(readyCheckmarkImage, false);

        SetKickButtonState(false);
        SetHighlighted(false);

        boardPreviewController?.ClearBoard();
    }

    #endregion

    #region Player

    private void SetPlayerIcon(string iconId)
    {
        if (playerIconImage == null)
        {
            return;
        }

        Sprite iconSprite = UIIconManager.instance != null
            ? UIIconManager.instance.GetPlayerIconSpriteById(iconId)
            : null;

        playerIconImage.sprite = iconSprite;
        playerIconImage.enabled = iconSprite != null;
        playerIconImage.preserveAspect = true;
    }

    private void SetPlayerName(string playerName, string playerUserId)
    {
        if (playerNameText == null)
        {
            return;
        }

        string displayName = string.IsNullOrWhiteSpace(playerName)
            ? "Player"
            : playerName.Trim();

        string shortId = GetShortUserId(playerUserId);

        playerNameText.text = string.IsNullOrWhiteSpace(shortId)
            ? displayName
            : $"{displayName} #{shortId}";
    }

    private string GetShortUserId(string playerUserId)
    {
        if (string.IsNullOrWhiteSpace(playerUserId))
        {
            return string.Empty;
        }

        playerUserId = playerUserId.Trim();

        return playerUserId.Length <= 4
            ? playerUserId
            : playerUserId.Substring(0, 4);
    }

    #endregion

    #region Status

    private void SetStatusIcon(Image iconImage, UIIconType iconType, bool isVisible)
    {
        if (iconImage == null)
        {
            return;
        }

        Sprite iconSprite = UIIconManager.instance != null
            ? UIIconManager.instance.GetNonPlayerIconSprite(iconType)
            : null;

        iconImage.sprite = iconSprite;
        iconImage.preserveAspect = true;

        SetImageActive(
            iconImage,
            isVisible && iconSprite != null);
    }

    private void SetImageActive(Image image, bool isActive)
    {
        if (image != null)
        {
            image.gameObject.SetActive(isActive);
        }
    }

    #endregion

    #region Controls

    private void SetKickButtonState(bool canKick)
    {
        if (kickButton == null)
        {
            return;
        }

        kickButton.gameObject.SetActive(canKick);
        kickButton.interactable = canKick;
    }

    private void OnKickClicked()
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            kickRequested?.Invoke(userId);
        }
    }

    #endregion

    #region Selection

    public void SetHighlighted(bool highlighted)
    {
        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(highlighted);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        if (kickButton != null &&
            eventData.pointerPress != null &&
            eventData.pointerPress.transform.IsChildOf(kickButton.transform))
        {
            return;
        }

        rowClicked?.Invoke(userId);
    }

    #endregion

    #region Board

    public void UpdateBoard(LobbyBoardData boardData)
    {
        boardPreviewController?.DisplayBoard(boardData);
    }

    public void UpdateBoard(LobbyBoardData boardData, IReadOnlyList<int> markedCellIndices)
    {
        boardPreviewController?.DisplayBoard(boardData, markedCellIndices);
    }

    public void UpdateMarkedCells(IReadOnlyList<int> markedCellIndices)
    {
        boardPreviewController?.UpdateMarkedCells(markedCellIndices);
    }

    public void SetMarkedCell(int cellIndex, bool isMarked)
    {
        boardPreviewController?.SetMarkedCell(cellIndex, isMarked);
    }

    #endregion
}