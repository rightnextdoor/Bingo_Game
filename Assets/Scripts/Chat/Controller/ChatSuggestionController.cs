using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatSuggestionController : MonoBehaviour
{
    #region Fields

    [Header("Input")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TMP_Text suggestionText;

    [Header("Optional Panel")]
    [SerializeField] private GameObject suggestionPanel;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private ChatSuggestionRowUI rowPrefab;

    private readonly List<ChatSuggestionData> suggestions = new List<ChatSuggestionData>();
    private readonly List<ChatSuggestionRowUI> spawnedRows = new List<ChatSuggestionRowUI>();

    private Action<ChatSuggestionData> suggestionAccepted;
    private Func<ChatSuggestionData, string> completionTextProvider;
    private int selectedIndex = -1;

    public bool HasSuggestions => suggestions.Count > 0;
    public bool HasPanel => suggestionPanel != null && scrollRect != null && content != null && rowPrefab != null;
    public int SelectedIndex => selectedIndex;
    public ChatSuggestionData SelectedSuggestion =>
        selectedIndex >= 0 && selectedIndex < suggestions.Count ? suggestions[selectedIndex] : null;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        ApplyPlaceholderStyle();
        ClearSuggestions();
    }

    #endregion

    #region Suggestions

    public void SetSuggestions(
        IReadOnlyList<ChatSuggestionData> newSuggestions,
        Action<ChatSuggestionData> onAccepted,
        Func<ChatSuggestionData, string> completionProvider)
    {
        ClearSuggestions();

        suggestionAccepted = onAccepted;
        completionTextProvider = completionProvider;

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

        if (HasPanel)
        {
            BuildSuggestionRows();
            suggestionPanel.SetActive(true);
            ScrollSelectedRowIntoView();
        }

        RefreshSuggestionText();
    }

    public void ClearSuggestions()
    {
        suggestions.Clear();
        selectedIndex = -1;
        suggestionAccepted = null;
        completionTextProvider = null;

        SetSuggestionText(string.Empty);
        ClearSuggestionRows();

        if (suggestionPanel != null)
        {
            suggestionPanel.SetActive(false);
        }
    }

    public void RefreshSuggestionText()
    {
        ApplyPlaceholderStyle();

        ChatSuggestionData selected = SelectedSuggestion;

        if (inputField == null || suggestionText == null || selected == null || completionTextProvider == null)
        {
            SetSuggestionText(string.Empty);
            return;
        }

        string typedText = inputField.text ?? string.Empty;
        string completedText = completionTextProvider(selected) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(completedText) ||
            completedText.Length <= typedText.Length ||
            !completedText.StartsWith(typedText, StringComparison.OrdinalIgnoreCase))
        {
            SetSuggestionText(string.Empty);
            return;
        }

        string remainingText = completedText.Substring(typedText.Length);
        suggestionText.richText = true;
        suggestionText.text = $"<color=#FFFFFF00><noparse>{typedText}</noparse></color><noparse>{remainingText}</noparse>";
    }

    private void SetSuggestionText(string text)
    {
        if (suggestionText != null)
        {
            suggestionText.text = text ?? string.Empty;
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
        RefreshRowHighlights();
        RefreshSuggestionText();
        ScrollSelectedRowIntoView();
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
        RefreshRowHighlights();
        RefreshSuggestionText();
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

    private void RefreshRowHighlights()
    {
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            ChatSuggestionRowUI row = spawnedRows[i];

            if (row != null)
            {
                row.SetHighlighted(i == selectedIndex);
            }
        }
    }

    #endregion

    #region Panel Rows

    private void BuildSuggestionRows()
    {
        ClearSuggestionRows();

        if (!HasPanel)
        {
            return;
        }

        for (int i = 0; i < suggestions.Count; i++)
        {
            ChatSuggestionRowUI row = Instantiate(rowPrefab, content);
            row.gameObject.SetActive(true);
            row.Setup(suggestions[i], i, OnRowHovered, OnRowSelected, i == selectedIndex);
            spawnedRows.Add(row);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private void ClearSuggestionRows()
    {
        for (int i = spawnedRows.Count - 1; i >= 0; i--)
        {
            ChatSuggestionRowUI row = spawnedRows[i];

            if (row == null)
            {
                continue;
            }

            row.Clear();
            row.gameObject.SetActive(false);
            DestroyObject(row.gameObject);
        }

        spawnedRows.Clear();
    }

    private void ScrollSelectedRowIntoView()
    {
        if (!HasPanel || selectedIndex < 0 || selectedIndex >= spawnedRows.Count)
        {
            return;
        }

        ChatSuggestionRowUI selectedRow = spawnedRows[selectedIndex];

        if (selectedRow == null)
        {
            return;
        }

        RectTransform rowRect = selectedRow.GetComponent<RectTransform>();
        RectTransform viewportRect = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();

        if (rowRect == null || viewportRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float contentHeight = content.rect.height;
        float viewportHeight = viewportRect.rect.height;

        if (contentHeight <= viewportHeight)
        {
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
            return;
        }

        float rowTop = GetRowTopFromContentTop(rowRect);
        float rowBottom = rowTop + rowRect.rect.height;
        float viewTop = content.anchoredPosition.y;
        float viewBottom = viewTop + viewportHeight;
        float targetY = content.anchoredPosition.y;

        if (rowTop < viewTop)
        {
            targetY = rowTop;
        }
        else if (rowBottom > viewBottom)
        {
            targetY = rowBottom - viewportHeight;
        }

        float maxY = Mathf.Max(0f, contentHeight - viewportHeight);
        targetY = Mathf.Clamp(targetY, 0f, maxY);

        if (selectedIndex == 0)
        {
            targetY = 0f;
        }
        else if (selectedIndex == spawnedRows.Count - 1)
        {
            targetY = maxY;
        }

        content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetY);
        scrollRect.StopMovement();
    }

    private float GetRowTopFromContentTop(RectTransform rowRect)
    {
        return -rowRect.anchoredPosition.y - ((1f - rowRect.pivot.y) * rowRect.rect.height);
    }

    #endregion

    #region Style

    private void ApplyPlaceholderStyle()
    {
        if (inputField == null || suggestionText == null || inputField.placeholder is not TMP_Text placeholderText)
        {
            return;
        }

        suggestionText.font = placeholderText.font;
        suggestionText.fontSharedMaterial = placeholderText.fontSharedMaterial;
        suggestionText.fontSize = placeholderText.fontSize;
        suggestionText.fontStyle = placeholderText.fontStyle;
        suggestionText.color = placeholderText.color;
        suggestionText.alignment = placeholderText.alignment;
        suggestionText.margin = placeholderText.margin;
        suggestionText.characterSpacing = placeholderText.characterSpacing;
        suggestionText.wordSpacing = placeholderText.wordSpacing;
        suggestionText.lineSpacing = placeholderText.lineSpacing;
        suggestionText.paragraphSpacing = placeholderText.paragraphSpacing;
        suggestionText.richText = true;
    }

    #endregion

    #region Helpers

    private void DestroyObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    #endregion
}
