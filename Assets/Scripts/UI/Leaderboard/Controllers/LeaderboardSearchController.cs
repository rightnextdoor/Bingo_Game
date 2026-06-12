using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardSearchController : MonoBehaviour
{
    [Header("Search")]
    [SerializeField] private TMP_InputField searchInputField;
    [SerializeField] private Button searchButton;

    [Header("Find Me")]
    [SerializeField] private Button findMeButton;

    public event Action<string> SearchRequested;
    public event Action FindMeRequested;

    private void OnEnable()
    {
        if (searchButton != null)
        {
            searchButton.onClick.AddListener(RequestSearch);
        }

        if (findMeButton != null)
        {
            findMeButton.onClick.AddListener(RequestFindMe);
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
            searchButton.onClick.RemoveListener(RequestSearch);
        }

        if (findMeButton != null)
        {
            findMeButton.onClick.RemoveListener(RequestFindMe);
        }

        if (searchInputField != null)
        {
            searchInputField.onSubmit.RemoveListener(RequestSearchFromInputSubmit);
        }
    }

    public void ClearSearchInput()
    {
        if (searchInputField == null)
        {
            return;
        }

        searchInputField.text = string.Empty;
    }

    public void SetSearchInputText(string searchText)
    {
        if (searchInputField == null)
        {
            return;
        }

        searchInputField.text = searchText ?? string.Empty;
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

    private void RequestSearch()
    {
        SearchRequested?.Invoke(GetSearchInputText());
    }

    private void RequestSearchFromInputSubmit(string searchText)
    {
        SearchRequested?.Invoke(string.IsNullOrWhiteSpace(searchText) ? string.Empty : searchText.Trim());
    }

    private void RequestFindMe()
    {
        FindMeRequested?.Invoke();
    }
}