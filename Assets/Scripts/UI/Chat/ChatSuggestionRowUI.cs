using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatSuggestionRowUI : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    #region Fields

    [Header("Row")]
    [SerializeField] private GameObject highlightObject;

    [Header("Player")]
    [SerializeField] private Image playerIconImage;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text userIdText;

    private int boundIndex = -1;
    private ChatSuggestionData suggestionData;
    private Action<int> hoverRequested;
    private Action<int> selectionRequested;

    public int BoundIndex => boundIndex;
    public ChatSuggestionData SuggestionData => suggestionData;

    #endregion

    private void Awake()
    {
        Clear();
    }

    public void Setup(ChatSuggestionData data, int index, Action<int> onHoverRequested, Action<int> onSelectionRequested, bool highlighted)
    {
        Clear();

        if (data == null)
        {
            return;
        }

        suggestionData = data;
        boundIndex = index;
        hoverRequested = onHoverRequested;
        selectionRequested = onSelectionRequested;

        if (playerNameText != null)
        {
            playerNameText.text = string.IsNullOrWhiteSpace(data.playerName) ? "Player" : data.playerName;
        }

        if (userIdText != null)
        {
            userIdText.text = string.IsNullOrWhiteSpace(data.userId) ? string.Empty : data.userId;
        }

        if (playerIconImage != null)
        {
            Sprite sprite = UIIconManager.instance != null ? UIIconManager.instance.GetPlayerIconSpriteById(data.iconId) : null;
            playerIconImage.sprite = sprite;
            playerIconImage.enabled = sprite != null;
            playerIconImage.preserveAspect = true;
        }

        SetHighlighted(highlighted);
    }

    public void Clear()
    {
        boundIndex = -1;
        suggestionData = null;
        hoverRequested = null;
        selectionRequested = null;

        if (playerNameText != null)
        {
            playerNameText.text = string.Empty;
        }

        if (userIdText != null)
        {
            userIdText.text = string.Empty;
        }

        if (playerIconImage != null)
        {
            playerIconImage.sprite = null;
            playerIconImage.enabled = false;
        }

        SetHighlighted(false);
    }

    public void SetHighlighted(bool highlighted)
    {
        if (highlightObject != null)
        {
            highlightObject.SetActive(highlighted);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (boundIndex >= 0)
        {
            hoverRequested?.Invoke(boundIndex);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (boundIndex >= 0 && eventData.button == PointerEventData.InputButton.Left)
        {
            selectionRequested?.Invoke(boundIndex);
        }
    }
}
