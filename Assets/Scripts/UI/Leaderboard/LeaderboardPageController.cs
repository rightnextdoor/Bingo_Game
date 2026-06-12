using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardPageController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Header("Input")]
    [SerializeField] private TMP_InputField pageInputField;

    [Header("Text")]
    [SerializeField] private TMP_Text pageCountText;

    public event Action PreviousPageRequested;
    public event Action NextPageRequested;
    public event Action<int> PageRequested;

    private int currentPage = 1;
    private int totalPages = 1;
    private bool filteringInput;

    private void Awake()
    {
        SetupInputField();
    }

    private void OnEnable()
    {
        if (previousButton != null)
        {
            previousButton.onClick.AddListener(RequestPreviousPage);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(RequestNextPage);
        }

        if (pageInputField != null)
        {
            pageInputField.onValueChanged.AddListener(FilterPageInput);
            pageInputField.onEndEdit.AddListener(RequestPageFromInput);
        }
    }

    private void OnDisable()
    {
        if (previousButton != null)
        {
            previousButton.onClick.RemoveListener(RequestPreviousPage);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(RequestNextPage);
        }

        if (pageInputField != null)
        {
            pageInputField.onValueChanged.RemoveListener(FilterPageInput);
            pageInputField.onEndEdit.RemoveListener(RequestPageFromInput);
        }
    }

    public void SetPageDisplay(int newCurrentPage, int newTotalPages)
    {
        totalPages = Mathf.Max(1, newTotalPages);
        currentPage = Mathf.Clamp(newCurrentPage, 1, totalPages);

        if (pageCountText != null)
        {
            pageCountText.text = $"Page {currentPage} / {totalPages}";
        }

        if (previousButton != null)
        {
            previousButton.interactable = currentPage > 1;
        }

        if (nextButton != null)
        {
            nextButton.interactable = currentPage < totalPages;
        }

        if (pageInputField != null)
        {
            pageInputField.characterLimit = GetDigitCount(totalPages);
            pageInputField.SetTextWithoutNotify(string.Empty);
        }
    }

    public void SetPageControlsInteractable(bool isInteractable)
    {
        if (previousButton != null)
        {
            previousButton.interactable = isInteractable && currentPage > 1;
        }

        if (nextButton != null)
        {
            nextButton.interactable = isInteractable && currentPage < totalPages;
        }

        if (pageInputField != null)
        {
            pageInputField.interactable = isInteractable;
        }
    }

    private void SetupInputField()
    {
        if (pageInputField == null)
        {
            return;
        }

        pageInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        pageInputField.lineType = TMP_InputField.LineType.SingleLine;
        pageInputField.characterLimit = GetDigitCount(totalPages);
    }

    private void RequestPreviousPage()
    {
        PreviousPageRequested?.Invoke();
    }

    private void RequestNextPage()
    {
        NextPageRequested?.Invoke();
    }

    private void RequestPageFromInput(string inputText)
    {
        if (string.IsNullOrWhiteSpace(inputText))
        {
            return;
        }

        if (!int.TryParse(inputText, out int requestedPage))
        {
            pageInputField.SetTextWithoutNotify(string.Empty);
            return;
        }

        requestedPage = Mathf.Clamp(requestedPage, 1, totalPages);

        pageInputField.SetTextWithoutNotify(string.Empty);

        PageRequested?.Invoke(requestedPage);
    }

    private void FilterPageInput(string inputText)
    {
        if (filteringInput)
        {
            return;
        }

        string filteredText = GetDigitsOnly(inputText);

        if (filteredText == inputText)
        {
            return;
        }

        filteringInput = true;
        pageInputField.SetTextWithoutNotify(filteredText);
        filteringInput = false;
    }

    private string GetDigitsOnly(string inputText)
    {
        if (string.IsNullOrEmpty(inputText))
        {
            return string.Empty;
        }

        string digitsOnly = string.Empty;

        for (int i = 0; i < inputText.Length; i++)
        {
            if (char.IsDigit(inputText[i]))
            {
                digitsOnly += inputText[i];
            }
        }

        return digitsOnly;
    }

    private int GetDigitCount(int number)
    {
        number = Mathf.Max(1, number);
        return number.ToString().Length;
    }
}