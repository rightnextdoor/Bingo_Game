using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LobbyCustomPanelController : MonoBehaviour
{
    #region Fields

    private const string HiddenValue = "******";
    private const string ShowText = "Show";
    private const string HideText = "Hide";

    [Header("Panel")]
    [SerializeField] private GameObject customLobbyPanel;

    [Header("Lobby Name")]
    [SerializeField] private TMP_Text lobbyNameValueText;

    [Header("Lobby Code")]
    [SerializeField] private TMP_Text lobbyCodeValueText;
    [SerializeField] private Button lobbyCodeCopyButton;

    [Header("Lobby Password")]
    [SerializeField] private TMP_Text lobbyPasswordValueText;
    [SerializeField] private Button lobbyPasswordCopyButton;

    [Header("Visibility")]
    [SerializeField] private Button visibilityButton;
    [SerializeField] private TMP_Text visibilityButtonText;

    private string lobbyCode = string.Empty;
    private string lobbyPassword = string.Empty;

    private bool hasPassword;
    private bool isPrivateInfoVisible;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        isPrivateInfoVisible = false;

        SetPanelVisible(false);
        RefreshPrivateInfoDisplay();
    }

    private void OnEnable()
    {
        RegisterListeners();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    #endregion

    #region Lobby Display

    public void DisplayLobbyInfo(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData == null || lobbyViewData.playMode != MainMenuPlayMode.Custom)
        {
            SetPanelVisible(false);
            return;
        }

        SetPanelVisible(true);

        lobbyCode = lobbyViewData.roomCode ?? string.Empty;
        lobbyPassword = lobbyViewData.lobbyPassword ?? string.Empty;
        hasPassword = lobbyViewData.hasPassword && !string.IsNullOrWhiteSpace(lobbyPassword);

        if (lobbyNameValueText != null)
        {
            lobbyNameValueText.text = lobbyViewData.lobbyName ?? string.Empty;
        }

        if (lobbyCodeCopyButton != null)
        {
            lobbyCodeCopyButton.interactable = true;
        }

        if (lobbyPasswordCopyButton != null)
        {
            lobbyPasswordCopyButton.interactable = true;
        }

        RefreshPrivateInfoDisplay();
    }

    #endregion

    #region Visibility

    private void TogglePrivateInfoVisibility()
    {
        isPrivateInfoVisible = !isPrivateInfoVisible;
        RefreshPrivateInfoDisplay();
    }

    private void RefreshPrivateInfoDisplay()
    {
        if (lobbyCodeValueText != null)
        {
            lobbyCodeValueText.text = isPrivateInfoVisible
                ? lobbyCode
                : HiddenValue;
        }

        if (lobbyPasswordValueText != null)
        {
            lobbyPasswordValueText.text = isPrivateInfoVisible
                ? lobbyPassword
                : HiddenValue;
        }

        if (visibilityButtonText != null)
        {
            visibilityButtonText.text = isPrivateInfoVisible ? HideText : ShowText;
        }
    }

    private void SetPanelVisible(bool isVisible)
    {
        if (customLobbyPanel != null && customLobbyPanel.activeSelf != isVisible)
        {
            customLobbyPanel.SetActive(isVisible);
        }
    }

    #endregion

    #region Copy

    private void CopyLobbyCode()
    {
        GUIUtility.systemCopyBuffer = lobbyCode ?? string.Empty;
    }

    private void CopyLobbyPassword()
    {
        GUIUtility.systemCopyBuffer = lobbyPassword ?? string.Empty;
    }

    #endregion

    #region Listeners

    private void RegisterListeners()
    {
        if (visibilityButton != null)
        {
            visibilityButton.onClick.RemoveListener(TogglePrivateInfoVisibility);
            visibilityButton.onClick.AddListener(TogglePrivateInfoVisibility);
        }

        if (lobbyCodeCopyButton != null)
        {
            lobbyCodeCopyButton.onClick.RemoveListener(CopyLobbyCode);
            lobbyCodeCopyButton.onClick.AddListener(CopyLobbyCode);
        }

        if (lobbyPasswordCopyButton != null)
        {
            lobbyPasswordCopyButton.onClick.RemoveListener(CopyLobbyPassword);
            lobbyPasswordCopyButton.onClick.AddListener(CopyLobbyPassword);
        }
    }

    private void UnregisterListeners()
    {
        if (visibilityButton != null)
        {
            visibilityButton.onClick.RemoveListener(TogglePrivateInfoVisibility);
        }

        if (lobbyCodeCopyButton != null)
        {
            lobbyCodeCopyButton.onClick.RemoveListener(CopyLobbyCode);
        }

        if (lobbyPasswordCopyButton != null)
        {
            lobbyPasswordCopyButton.onClick.RemoveListener(CopyLobbyPassword);
        }
    }

    #endregion
}