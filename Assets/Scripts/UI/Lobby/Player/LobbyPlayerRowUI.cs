using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LobbyPlayerRowUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Selection")]
    [SerializeField] private GameObject selectionHighlight;

    [Header("Player")]
    [SerializeField] private Image playerIconImage;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField, Min(1)] private int maxPlayerNameCharacters = 16;

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

    public void Setup(
        LobbyPlayerViewData playerData,
        bool canKick,
        bool showBotIcon,
        Action<string> onRowClicked,
        Action<string> onKickRequested,
        bool isHighlighted)
    {
        if (playerData == null)
        {
            return;
        }

        userId = playerData.userId ?? string.Empty;
        rowClicked = onRowClicked;
        kickRequested = onKickRequested;

        SetPlayerIcon(playerData.iconId);

        if (playerNameText != null)
        {
            playerNameText.text = BuildPlayerDisplayName(playerData.playerName, playerData.userId);
        }

        boardPreviewController?.DisplayBoard(playerData.boardData);

        if (botIconImage != null)
        {
            botIconImage.gameObject.SetActive(showBotIcon && playerData.userTag == UserTag.Bot);
        }

        if (readyCheckmarkImage != null)
        {
            readyCheckmarkImage.gameObject.SetActive(playerData.isReady);
        }

        if (kickButton != null)
        {
            kickButton.gameObject.SetActive(canKick);
            kickButton.interactable = canKick;
        }

        SetHighlighted(isHighlighted);
    }

    public void UpdateBoard(LobbyBoardData boardData)
    {
        boardPreviewController?.DisplayBoard(boardData);
    }

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

    private void OnKickClicked()
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            kickRequested?.Invoke(userId);
        }
    }

    private void SetPlayerIcon(string iconId)
    {
        if (playerIconImage == null)
        {
            return;
        }

        Sprite sprite = UIIconManager.instance != null
            ? UIIconManager.instance.GetPlayerIconSpriteById(iconId)
            : null;

        playerIconImage.sprite = sprite;
        playerIconImage.enabled = sprite != null;
        playerIconImage.preserveAspect = true;
    }

    private string BuildPlayerDisplayName(string playerName, string playerUserId)
    {
        string displayName = string.IsNullOrWhiteSpace(playerName)
            ? "Player"
            : playerName.Trim();

        if (displayName.Length > maxPlayerNameCharacters)
        {
            displayName = displayName.Substring(0, maxPlayerNameCharacters);
        }

        string shortId = GetShortUserId(playerUserId);

        return string.IsNullOrWhiteSpace(shortId)
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
}