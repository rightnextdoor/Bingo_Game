using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LobbyHeaderController : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text lobbyTitleText;
    [SerializeField] private TMP_Text lobbyTimerText;

    [Header("Buttons")]
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button hostSettingsButton;

    private LobbyViewData currentLobbyViewData;

    public event Action LeaveRequested;
    public event Action StartRequested;
    public event Action HostSettingsRequested;

    private void OnEnable()
    {
        if (leaveButton != null)
        {
            leaveButton.onClick.AddListener(OnLeaveClicked);
        }

        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartClicked);
        }

        if (hostSettingsButton != null)
        {
            hostSettingsButton.onClick.AddListener(OnHostSettingsClicked);
        }
    }

    private void OnDisable()
    {
        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveListener(OnLeaveClicked);
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartClicked);
        }

        if (hostSettingsButton != null)
        {
            hostSettingsButton.onClick.RemoveListener(OnHostSettingsClicked);
        }
    }

    private void Update()
    {
        UpdateTimerDisplay();
    }

    public void DisplayLobbyInfo(LobbyViewData lobbyViewData, bool canOpenHostSettings, bool canStartLobby)
    {
        if (lobbyViewData == null)
        {
            return;
        }

        currentLobbyViewData = lobbyViewData;

        SetLobbyTitle(lobbyViewData);
        SetStartVisible(canStartLobby);
        SetHostSettingsVisible(canOpenHostSettings);
        UpdateTimerDisplay();
    }

    private void SetStartVisible(bool isVisible)
    {
        if (startButton != null)
        {
            startButton.gameObject.SetActive(isVisible);
        }
    }

    public void SetTimerSeconds(float remainingSeconds)
    {
        if (lobbyTimerText == null)
        {
            return;
        }

        int seconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));

        lobbyTimerText.gameObject.SetActive(true);
        lobbyTimerText.text = FormatTime(seconds);
    }

    public void HideTimer()
    {
        if (lobbyTimerText != null)
        {
            lobbyTimerText.gameObject.SetActive(false);
        }
    }

    private void SetLobbyTitle(LobbyViewData lobbyViewData)
    {
        if (lobbyTitleText == null)
        {
            return;
        }

        string gameName = string.IsNullOrWhiteSpace(lobbyViewData.gameModeName)
            ? lobbyViewData.gameModeType.ToString()
            : lobbyViewData.gameModeName;

        lobbyTitleText.text = $"{lobbyViewData.playMode} - {gameName}";
    }

    private void UpdateTimerDisplay()
    {
        if (lobbyTimerText == null || currentLobbyViewData == null)
        {
            return;
        }

        if (!currentLobbyViewData.isTimerActive)
        {
            HideTimer();
            return;
        }

        float remainingSeconds = Mathf.Max(
            0f,
            (float)(currentLobbyViewData.timerEndTime - LobbyTimer.GetCurrentTime()));

        SetTimerSeconds(remainingSeconds);
    }

    private void SetHostSettingsVisible(bool isVisible)
    {
        if (hostSettingsButton != null)
        {
            hostSettingsButton.gameObject.SetActive(isVisible);
        }
    }

    private string FormatTime(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        return $"{minutes:0}:{seconds:00}";
    }

    private void OnLeaveClicked()
    {
        LeaveRequested?.Invoke();
    }

    private void OnHostSettingsClicked()
    {
        HostSettingsRequested?.Invoke();
    }

    private void OnStartClicked()
    {
        StartRequested?.Invoke();
    }
}