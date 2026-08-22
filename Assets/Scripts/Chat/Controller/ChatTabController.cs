using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatTabController : MonoBehaviour
{
    #region Fields

    [Header("Tabs")]
    [SerializeField] private Button lobbyTabButton;
    [SerializeField] private Button friendsTabButton;

    [Header("Content")]
    [SerializeField] private GameObject lobbyChatContent;
    [SerializeField] private GameObject friendsContent;

    private bool lobbySelected = true;

    public bool IsLobbySelected => lobbySelected;
    public bool IsFriendsSelected => !lobbySelected;

    public event Action SelectionChanged;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        ApplySelection(false);
    }

    private void OnEnable()
    {
        RegisterListeners();
        ApplySelection(false);
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    #endregion

    #region Selection

    public void SelectLobby(bool notify = true)
    {
        SetLobbySelected(true, notify);
    }

    public void SelectFriends(bool notify = true)
    {
        SetLobbySelected(false, notify);
    }

    public void SetLobbySelected(bool selected, bool notify = true)
    {
        bool changed = lobbySelected != selected;
        lobbySelected = selected;
        ApplySelection(notify && changed);
    }

    private void ApplySelection(bool notify)
    {
        if (lobbyTabButton != null)
        {
            lobbyTabButton.interactable = !lobbySelected;
        }

        if (friendsTabButton != null)
        {
            friendsTabButton.interactable = lobbySelected;
        }

        if (lobbyChatContent != null)
        {
            lobbyChatContent.SetActive(lobbySelected);
        }

        if (friendsContent != null)
        {
            friendsContent.SetActive(!lobbySelected);
        }

        if (notify)
        {
            SelectionChanged?.Invoke();
        }
    }

    #endregion

    #region UI Events

    private void RegisterListeners()
    {
        if (lobbyTabButton != null)
        {
            lobbyTabButton.onClick.RemoveListener(OnLobbyClicked);
            lobbyTabButton.onClick.AddListener(OnLobbyClicked);
        }

        if (friendsTabButton != null)
        {
            friendsTabButton.onClick.RemoveListener(OnFriendsClicked);
            friendsTabButton.onClick.AddListener(OnFriendsClicked);
        }
    }

    private void UnregisterListeners()
    {
        if (lobbyTabButton != null)
        {
            lobbyTabButton.onClick.RemoveListener(OnLobbyClicked);
        }

        if (friendsTabButton != null)
        {
            friendsTabButton.onClick.RemoveListener(OnFriendsClicked);
        }
    }

    private void OnLobbyClicked()
    {
        SelectLobby();
    }

    private void OnFriendsClicked()
    {
        SelectFriends();
    }

    #endregion
}
