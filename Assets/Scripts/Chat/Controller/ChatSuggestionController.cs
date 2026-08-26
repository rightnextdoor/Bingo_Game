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
    private Action textSuggestionAccepted;
    private string textSuggestion = string.Empty;
    private int selectedIndex = -1;
    private bool inputTextHidden;
    private float inputTextCanvasAlpha = 1f;
    private bool hasInputTextBaseMargin;
    private Vector4 inputTextBaseMargin;

    public bool HasSuggestions => suggestions.Count > 0 || !string.IsNullOrWhiteSpace(textSuggestion);
    public bool HasPanel => suggestionPanel != null && scrollRect != null && content != null && rowPrefab != null;
    public int SelectedIndex => selectedIndex;
    public ChatSuggestionData SelectedSuggestion =>
        selectedIndex >= 0 && selectedIndex < suggestions.Count ? suggestions[selectedIndex] : null;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        CaptureInputTextBaseMargin();
        ApplyPlaceholderStyle();
        ClearSuggestions();
    }

    private void OnDisable()
    {
        RestoreInputTextLayout();
        ShowInputText();
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
            suggestionPanel.SetActive(true);
            BuildSuggestionRows();
            ScrollSelectedRowIntoView();
        }

        RefreshSuggestionText();
    }

    public void SetTextSuggestion(string completedText, Action onAccepted)
    {
        ClearSuggestions();

        if (string.IsNullOrWhiteSpace(completedText))
        {
            return;
        }

        textSuggestion = completedText;
        textSuggestionAccepted = onAccepted;
        RefreshSuggestionText();
    }

    public void ClearSuggestions()
    {
        suggestions.Clear();
        selectedIndex = -1;
        suggestionAccepted = null;
        completionTextProvider = null;
        textSuggestionAccepted = null;
        textSuggestion = string.Empty;

        SetSuggestionText(string.Empty);
        RestoreInputTextLayout();
        ShowInputText();
        ClearSuggestionRows();

        if (suggestionPanel != null)
        {
            suggestionPanel.SetActive(false);
        }
    }

    public void RefreshSuggestionText()
    {
        ApplyPlaceholderStyle();
        RestoreInputTextLayout();

        if (inputField == null || suggestionText == null)
        {
            ClearSuggestionText();
            return;
        }

        string typedText = inputField.text ?? string.Empty;
        string completedText = textSuggestion;

        if (string.IsNullOrWhiteSpace(completedText))
        {
            ChatSuggestionData selected = SelectedSuggestion;

            if (selected == null || completionTextProvider == null)
            {
                ClearSuggestionText();
                return;
            }

            completedText = completionTextProvider(selected) ?? string.Empty;
        }

        if (!TryBuildSuggestionText(typedText, completedText, out string renderedText, out int visualCaretCharacterIndex))
        {
            ClearSuggestionText();
            return;
        }

        SetSuggestionText(renderedText);
        MoveInputCaretToSuggestionPosition(visualCaretCharacterIndex);
        HideInputText();
    }

    private bool TryBuildSuggestionText(
        string typedText,
        string completedText,
        out string renderedText,
        out int visualCaretCharacterIndex)
    {
        renderedText = string.Empty;
        visualCaretCharacterIndex = 0;

        if (string.IsNullOrWhiteSpace(typedText) || string.IsNullOrWhiteSpace(completedText))
        {
            return false;
        }

        int commonPrefixLength = GetCommonPrefixLength(typedText, completedText);

        if (commonPrefixLength == typedText.Length)
        {
            if (completedText.Length <= typedText.Length)
            {
                return false;
            }

            renderedText = BuildNormalText(typedText) + BuildSuggestionText(completedText.Substring(typedText.Length));
            visualCaretCharacterIndex = typedText.Length;
            return true;
        }

        string typedQuery = typedText.Substring(commonPrefixLength);

        if (string.IsNullOrWhiteSpace(typedQuery))
        {
            return false;
        }

        int matchIndex = completedText.IndexOf(typedQuery, commonPrefixLength, StringComparison.OrdinalIgnoreCase);

        if (matchIndex < 0)
        {
            return false;
        }

        int matchEndIndex = matchIndex + typedQuery.Length;

        string typedPrefix = typedText.Substring(0, commonPrefixLength);
        string suggestionPrefix = completedText.Substring(commonPrefixLength, matchIndex - commonPrefixLength);
        string suggestionSuffix = matchEndIndex < completedText.Length ? completedText.Substring(matchEndIndex) : string.Empty;

        renderedText =
            BuildNormalText(typedPrefix) +
            BuildSuggestionText(suggestionPrefix) +
            BuildNormalText(typedQuery) +
            BuildSuggestionText(suggestionSuffix);

        visualCaretCharacterIndex = typedPrefix.Length + suggestionPrefix.Length + typedQuery.Length;
        return true;
    }

    private int GetCommonPrefixLength(string left, string right)
    {
        int length = Mathf.Min(left.Length, right.Length);
        int index = 0;

        while (index < length && char.ToUpperInvariant(left[index]) == char.ToUpperInvariant(right[index]))
        {
            index++;
        }

        return index;
    }

    private string BuildNormalText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        Color color = inputField != null && inputField.textComponent != null ? inputField.textComponent.color : Color.white;
        return $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}><noparse>{text}</noparse></color>";
    }

    private string BuildSuggestionText(string text)
    {
        return string.IsNullOrEmpty(text) ? string.Empty : $"<noparse>{text}</noparse>";
    }

    private void ClearSuggestionText()
    {
        SetSuggestionText(string.Empty);
        RestoreInputTextLayout();
        ShowInputText();
    }

    private void SetSuggestionText(string text)
    {
        if (suggestionText == null)
        {
            return;
        }

        suggestionText.richText = true;
        suggestionText.text = text ?? string.Empty;
        suggestionText.ForceMeshUpdate(true, true);
    }

    private void CaptureInputTextBaseMargin()
    {
        if (inputField == null || inputField.textComponent == null)
        {
            return;
        }

        inputTextBaseMargin = inputField.textComponent.margin;
        hasInputTextBaseMargin = true;
    }

    private void MoveInputCaretToSuggestionPosition(int visualCaretCharacterIndex)
    {
        if (inputField == null || inputField.textComponent == null || suggestionText == null)
        {
            return;
        }

        if (!hasInputTextBaseMargin)
        {
            CaptureInputTextBaseMargin();
        }

        TMP_Text inputText = inputField.textComponent;

        inputField.ForceLabelUpdate();
        inputText.ForceMeshUpdate(true, true);
        suggestionText.ForceMeshUpdate(true, true);

        float currentCaretX = GetCharacterEndX(inputText, inputField.text?.Length ?? 0);
        float visualCaretX = GetCharacterEndX(suggestionText, visualCaretCharacterIndex);
        float offsetX = visualCaretX - currentCaretX;

        Vector4 margin = hasInputTextBaseMargin ? inputTextBaseMargin : inputText.margin;
        margin.x += offsetX;
        inputText.margin = margin;

        inputText.ForceMeshUpdate(true, true);
        inputField.ForceLabelUpdate();
    }

    private float GetCharacterEndX(TMP_Text textComponent, int characterCount)
    {
        if (textComponent == null || characterCount <= 0 || textComponent.textInfo == null ||
            textComponent.textInfo.characterCount <= 0)
        {
            return 0f;
        }

        int characterIndex = Mathf.Clamp(characterCount - 1, 0, textComponent.textInfo.characterCount - 1);
        return textComponent.textInfo.characterInfo[characterIndex].xAdvance;
    }

    private void RestoreInputTextLayout()
    {
        if (!hasInputTextBaseMargin || inputField == null || inputField.textComponent == null)
        {
            return;
        }

        inputField.textComponent.margin = inputTextBaseMargin;
        inputField.textComponent.ForceMeshUpdate(true, true);
        inputField.ForceLabelUpdate();
    }

    private void HideInputText()
    {
        if (inputTextHidden || inputField == null || inputField.textComponent == null)
        {
            return;
        }

        CanvasRenderer canvasRenderer = inputField.textComponent.canvasRenderer;
        inputTextCanvasAlpha = canvasRenderer.GetAlpha();
        canvasRenderer.SetAlpha(0f);
        inputTextHidden = true;
    }

    private void ShowInputText()
    {
        if (!inputTextHidden || inputField == null || inputField.textComponent == null)
        {
            return;
        }

        inputField.textComponent.canvasRenderer.SetAlpha(inputTextCanvasAlpha);
        inputTextHidden = false;
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
        if (!string.IsNullOrWhiteSpace(textSuggestion))
        {
            Action callback = textSuggestionAccepted;
            ClearSuggestions();
            callback?.Invoke();
            return true;
        }

        ChatSuggestionData selected = SelectedSuggestion;

        if (selected == null)
        {
            return false;
        }

        Action<ChatSuggestionData> userCallback = suggestionAccepted;
        ChatSuggestionData accepted = selected.Clone();

        ClearSuggestions();
        userCallback?.Invoke(accepted);
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
