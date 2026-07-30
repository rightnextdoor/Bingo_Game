using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardSearchController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Search")]
    [SerializeField] private TMP_InputField searchInputField;
    [SerializeField] private Button searchButton;

    [Header("Find Me")]
    [SerializeField] private Button findMeButton;

    [Header("Clear Search")]
    [SerializeField] private Button clearSearchButton;

    [Header("Error")]
    [SerializeField] private TMP_Text errorText;

    #endregion

    #region Events

    public event Action<string> SearchButtonRequested;
    public event Action<string> SearchInputSubmitted;
    public event Action ClearSearchRequested;
    public event Action FindMeRequested;

    #endregion

    #region Layout Fields

    private const float ActiveButtonWidthScale = 0.75f;
    private const float MinimumActiveButtonWidth = 70f;

    private LayoutElement searchButtonLayoutElement;
    private LayoutElement findMeButtonLayoutElement;
    private LayoutElement clearSearchButtonLayoutElement;

    private float searchButtonDefaultWidth;
    private float findMeButtonDefaultWidth;
    private float clearSearchButtonDefaultWidth;

    #endregion

    #region Error Fields

    private const float ErrorDisplayTime = 1.4f;
    private const float ErrorFadeTime = 0.35f;
    private const float ErrorFontSize = 20f;

    private CanvasGroup errorCanvasGroup;
    private Coroutine errorRoutine;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        SetupLayoutElements();
        SetupErrorText();

        SetSearchActive(false);
        HideError();
    }

    private void OnEnable()
    {
        if (searchButton != null)
        {
            searchButton.onClick.AddListener(RequestSearchFromButton);
        }

        if (findMeButton != null)
        {
            findMeButton.onClick.AddListener(RequestFindMe);
        }

        if (clearSearchButton != null)
        {
            clearSearchButton.onClick.AddListener(RequestClearSearch);
        }

        if (searchInputField != null)
        {
            searchInputField.onSubmit.AddListener(RequestSearchFromInputSubmit);
        }
    }

    private void OnDisable()
    {
        if (searchButton != null)
        {
            searchButton.onClick.RemoveListener(RequestSearchFromButton);
        }

        if (findMeButton != null)
        {
            findMeButton.onClick.RemoveListener(RequestFindMe);
        }

        if (clearSearchButton != null)
        {
            clearSearchButton.onClick.RemoveListener(RequestClearSearch);
        }

        if (searchInputField != null)
        {
            searchInputField.onSubmit.RemoveListener(RequestSearchFromInputSubmit);
        }

        StopErrorRoutine();
    }

    #endregion

    #region Input

    public void ClearSearchInput()
    {
        if (searchInputField == null)
        {
            return;
        }

        searchInputField.SetTextWithoutNotify(string.Empty);
    }

    public void SetSearchInputText(string searchText)
    {
        if (searchInputField == null)
        {
            return;
        }

        searchInputField.SetTextWithoutNotify(searchText ?? string.Empty);
    }

    public string GetSearchInputText()
    {
        if (searchInputField == null)
        {
            return string.Empty;
        }

        return searchInputField.text.Trim();
    }

    public void SetSearchInteractable(bool isInteractable)
    {
        if (searchInputField != null)
        {
            searchInputField.interactable = isInteractable;
        }

        if (searchButton != null)
        {
            searchButton.interactable = isInteractable;
        }
    }

    public void SetFindMeInteractable(bool isInteractable)
    {
        if (findMeButton != null)
        {
            findMeButton.interactable = isInteractable;
        }
    }

    private void RequestSearchFromButton()
    {
        string searchText = GetSearchInputText();

        ClearSearchInput();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            ClearSearchRequested?.Invoke();
            return;
        }

        SearchButtonRequested?.Invoke(searchText);
    }

    private void RequestSearchFromInputSubmit(string searchText)
    {
        searchText = string.IsNullOrWhiteSpace(searchText) ? string.Empty : searchText.Trim();

        ClearSearchInput();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return;
        }

        SearchInputSubmitted?.Invoke(searchText);
    }

    private void RequestClearSearch()
    {
        ClearSearchInput();
        HideError();
        SetSearchActive(false);

        ClearSearchRequested?.Invoke();
    }

    private void RequestFindMe()
    {
        FindMeRequested?.Invoke();
    }

    #endregion

    #region Search State

    public void SetSearchActive(bool isActive)
    {
        if (clearSearchButton != null)
        {
            clearSearchButton.gameObject.SetActive(isActive);
        }

        ApplyButtonLayout(isActive);
    }

    private void ApplyButtonLayout(bool searchActive)
    {
        if (!searchActive)
        {
            SetPreferredWidth(searchButtonLayoutElement, searchButtonDefaultWidth);
            SetPreferredWidth(findMeButtonLayoutElement, findMeButtonDefaultWidth);
            SetPreferredWidth(clearSearchButtonLayoutElement, clearSearchButtonDefaultWidth);
            return;
        }

        SetPreferredWidth(searchButtonLayoutElement, GetActiveWidth(searchButtonDefaultWidth));
        SetPreferredWidth(findMeButtonLayoutElement, GetActiveWidth(findMeButtonDefaultWidth));
        SetPreferredWidth(clearSearchButtonLayoutElement, GetActiveWidth(clearSearchButtonDefaultWidth));
    }

    #endregion

    #region Error

    public void ShowError(string message)
    {
        if (errorText == null)
        {
            return;
        }

        StopErrorRoutine();

        errorText.text = message ?? string.Empty;

        if (errorCanvasGroup != null)
        {
            errorCanvasGroup.alpha = 1f;
        }

        errorRoutine = StartCoroutine(ErrorFadeRoutine());
    }

    public void HideError()
    {
        StopErrorRoutine();

        if (errorCanvasGroup != null)
        {
            errorCanvasGroup.alpha = 0f;
        }

        if (errorText != null)
        {
            errorText.text = string.Empty;
        }
    }

    private IEnumerator ErrorFadeRoutine()
    {
        yield return new WaitForSeconds(ErrorDisplayTime);

        float timer = 0f;

        while (timer < ErrorFadeTime)
        {
            timer += Time.unscaledDeltaTime;

            if (errorCanvasGroup != null)
            {
                errorCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / ErrorFadeTime);
            }

            yield return null;
        }

        HideError();
    }

    private void StopErrorRoutine()
    {
        if (errorRoutine == null)
        {
            return;
        }

        StopCoroutine(errorRoutine);
        errorRoutine = null;
    }

    #endregion

    #region Setup Helpers

    private void SetupLayoutElements()
    {
        searchButtonLayoutElement = searchButton != null ? searchButton.GetComponent<LayoutElement>() : null;
        findMeButtonLayoutElement = findMeButton != null ? findMeButton.GetComponent<LayoutElement>() : null;
        clearSearchButtonLayoutElement = clearSearchButton != null ? clearSearchButton.GetComponent<LayoutElement>() : null;

        searchButtonDefaultWidth = GetCurrentPreferredWidth(searchButtonLayoutElement, searchButton);
        findMeButtonDefaultWidth = GetCurrentPreferredWidth(findMeButtonLayoutElement, findMeButton);
        clearSearchButtonDefaultWidth = GetCurrentPreferredWidth(clearSearchButtonLayoutElement, clearSearchButton);

        if (clearSearchButtonDefaultWidth <= 0f)
        {
            clearSearchButtonDefaultWidth = searchButtonDefaultWidth;
        }
    }

    private void SetupErrorText()
    {
        if (errorText == null)
        {
            return;
        }

        errorText.fontSize = ErrorFontSize;
        errorText.text = string.Empty;

        errorCanvasGroup = errorText.GetComponent<CanvasGroup>();

        if (errorCanvasGroup == null)
        {
            errorCanvasGroup = errorText.gameObject.AddComponent<CanvasGroup>();
        }

        errorCanvasGroup.alpha = 0f;
    }

    private float GetCurrentPreferredWidth(LayoutElement layoutElement, Button button)
    {
        if (layoutElement != null && layoutElement.preferredWidth > 0f)
        {
            return layoutElement.preferredWidth;
        }

        if (button == null)
        {
            return 0f;
        }

        RectTransform rectTransform = button.GetComponent<RectTransform>();

        if (rectTransform == null)
        {
            return 0f;
        }

        return rectTransform.rect.width;
    }

    private float GetActiveWidth(float defaultWidth)
    {
        if (defaultWidth <= 0f)
        {
            return 0f;
        }

        return Mathf.Max(MinimumActiveButtonWidth, defaultWidth * ActiveButtonWidthScale);
    }

    private void SetPreferredWidth(LayoutElement layoutElement, float width)
    {
        if (layoutElement == null || width <= 0f)
        {
            return;
        }

        layoutElement.preferredWidth = width;
    }

    #endregion
}