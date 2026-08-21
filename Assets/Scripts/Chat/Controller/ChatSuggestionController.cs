using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatSuggestionController : MonoBehaviour
{
    #region Fields

    [Header("Panel")]
    [SerializeField] private GameObject suggestionPanel;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;

    [Header("Rows")]
    [SerializeField] private ChatSuggestionRowUI rowPrefab;
    [SerializeField, Min(1f)] private float rowHeight = 52f;
    [SerializeField, Min(0)] private int extraVisibleRows = 1;

    [Header("Ghost Text")]
    [SerializeField] private TMP_Text ghostSuggestionText;

    private readonly List<ChatSuggestionData> suggestions = new List<ChatSuggestionData>();

    private VirtualizedScrollList virtualizedList;
    private Action<ChatSuggestionData> suggestionAccepted;
    private int selectedIndex = -1;

    public bool HasSuggestions => suggestions.Count > 0;
    public int SelectedIndex => selectedIndex;
    public ChatSuggestionData SelectedSuggestion =>
        selectedIndex >= 0 && selectedIndex < suggestions.Count ? suggestions[selectedIndex] : null;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        ResolveReferences();
        InitializeVirtualizedList();
        ClearSuggestions();
    }

    private void OnDestroy()
    {
        UnsubscribeFromVirtualizedList();
    }

    #endregion

    #region Setup

    private void ResolveReferences()
    {
        if (suggestionPanel == null)
        {
            suggestionPanel = gameObject;
        }

        if (scrollRect == null)
        {
            scrollRect = GetComponentInChildren<ScrollRect>(true);
        }

        if (content == null && scrollRect != null)
        {
            content = scrollRect.content;
        }
    }

    private void InitializeVirtualizedList()
    {
        if (scrollRect == null || content == null || rowPrefab == null)
        {
            return;
        }

        virtualizedList = GetComponent<VirtualizedScrollList>();

        if (virtualizedList == null)
        {
            virtualizedList = gameObject.AddComponent<VirtualizedScrollList>();
        }

        UnsubscribeFromVirtualizedList();

        if (!virtualizedList.Initialize(scrollRect, content, rowPrefab.gameObject, null, null, rowHeight, extraVisibleRows))
        {
            virtualizedList = null;
            return;
        }

        virtualizedList.ItemBound += OnItemBound;
        virtualizedList.ItemReleased += OnItemReleased;
    }

    private void UnsubscribeFromVirtualizedList()
    {
        if (virtualizedList == null)
        {
            return;
        }

        virtualizedList.ItemBound -= OnItemBound;
        virtualizedList.ItemReleased -= OnItemReleased;
    }

    #endregion

    #region Suggestions

    public void SetSuggestions(IReadOnlyList<ChatSuggestionData> newSuggestions, Action<ChatSuggestionData> onAccepted)
    {
        ClearSuggestions();

        suggestionAccepted = onAccepted;

        if (newSuggestions == null)
        {
            return;
        }

        for (int i = 0; i < newSuggestions.Count; i++)
        {
            ChatSuggestionData suggestion = newSuggestions[i];

            if (suggestion != null && suggestion.IsValid)
            {
                suggestions.Add(suggestion.Clone());
            }
        }

        if (suggestions.Count == 0)
        {
            return;
        }

        selectedIndex = 0;

        if (suggestionPanel != null)
        {
            suggestionPanel.SetActive(true);
        }

        if (virtualizedList == null)
        {
            InitializeVirtualizedList();
        }

        virtualizedList?.SetItemCount(suggestions.Count);
        virtualizedList?.ScrollToIndex(0);
        RefreshVisibleHighlights();
    }

    public void ClearSuggestions()
    {
        suggestions.Clear();
        selectedIndex = -1;
        suggestionAccepted = null;

        SetGhostText(string.Empty);

        if (virtualizedList != null)
        {
            virtualizedList.SetItemCount(0);
        }

        if (suggestionPanel != null)
        {
            suggestionPanel.SetActive(false);
        }
    }

    public void SetGhostText(string text)
    {
        if (ghostSuggestionText != null)
        {
            ghostSuggestionText.text = text ?? string.Empty;
        }
    }

    #endregion

    #region Selection

    public bool MoveSelection(int direction)
    {
        if (suggestions.Count == 0)
        {
            return false;
        }

        int nextIndex = selectedIndex < 0 ? 0 : Mathf.Clamp(selectedIndex + direction, 0, suggestions.Count - 1);

        if (nextIndex == selectedIndex)
        {
            return false;
        }

        selectedIndex = nextIndex;
        virtualizedList?.ScrollToIndex(selectedIndex);
        RefreshVisibleHighlights();
        return true;
    }

    public bool AcceptSelected()
    {
        ChatSuggestionData selected = SelectedSuggestion;

        if (selected == null)
        {
            return false;
        }

        Action<ChatSuggestionData> callback = suggestionAccepted;
        ChatSuggestionData accepted = selected.Clone();

        ClearSuggestions();
        callback?.Invoke(accepted);
        return true;
    }

    private void OnRowHovered(int index)
    {
        if (index < 0 || index >= suggestions.Count)
        {
            return;
        }

        selectedIndex = index;
        RefreshVisibleHighlights();
    }

    private void OnRowSelected(int index)
    {
        if (index < 0 || index >= suggestions.Count)
        {
            return;
        }

        selectedIndex = index;
        AcceptSelected();
    }

    private void RefreshVisibleHighlights()
    {
        if (virtualizedList == null)
        {
            return;
        }

        for (int i = 0; i < suggestions.Count; i++)
        {
            if (!virtualizedList.TryGetVisibleItem(i, out GameObject itemObject) || itemObject == null)
            {
                continue;
            }

            ChatSuggestionRowUI row = itemObject.GetComponent<ChatSuggestionRowUI>();
            row?.SetHighlighted(i == selectedIndex);
        }
    }

    #endregion

    #region Virtualized Rows

    private void OnItemBound(GameObject itemObject, int index)
    {
        if (itemObject == null || index < 0 || index >= suggestions.Count)
        {
            return;
        }

        ChatSuggestionRowUI row = itemObject.GetComponent<ChatSuggestionRowUI>();
        row?.Setup(suggestions[index], index, OnRowHovered, OnRowSelected, index == selectedIndex);
    }

    private void OnItemReleased(GameObject itemObject, int _)
    {
        if (itemObject != null)
        {
            itemObject.GetComponent<ChatSuggestionRowUI>()?.Clear();
        }
    }

    #endregion
}
