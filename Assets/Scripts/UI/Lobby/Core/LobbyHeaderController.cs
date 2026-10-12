using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LobbyHeaderController : MonoBehaviour
{
    private const string TimerCaption = "Game Starts In:";

    [Header("Text")]
    [SerializeField] private TMP_Text lobbyTitleText;
    [SerializeField] private TMP_Text lobbyTimerText;
    [SerializeField] private TMP_Text lobbyIdText;
    [Tooltip("Maximum internal lobby ID characters shown in the header. Set to 0 to show the full ID.")]
    [Min(0)]
    [SerializeField] private int lobbyIdDisplayLength = 8;

    [Header("Buttons")]
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button hostSettingsButton;

    private LobbyViewData currentLobbyViewData;
    private RectTransform timerRect;
    private TMP_FontAsset timerLayoutFont;
    private Vector2 timerLayoutSize;
    private Vector4 timerLayoutMargins;
    private float timerMaximumFontSize;
    private float timerMinimumFontSize;
    private float timerCaptionPosition;
    private float timerCountdownPosition;
    private int timerMinuteDigits;
    private int displayedTimerSeconds = -1;

    public event Action LeaveRequested;
    public event Action StartRequested;
    public event Action HostSettingsRequested;

    private void Awake()
    {
        if (lobbyTimerText != null)
        {
            timerRect = lobbyTimerText.rectTransform;
            timerMaximumFontSize = lobbyTimerText.enableAutoSizing
                ? lobbyTimerText.fontSizeMax
                : lobbyTimerText.fontSize;
            timerMinimumFontSize = lobbyTimerText.enableAutoSizing
                ? lobbyTimerText.fontSizeMin
                : timerMaximumFontSize * 0.75f;
        }

        if (lobbyIdText == null)
        {
            return;
        }

        float defaultSize = lobbyIdText.enableAutoSizing ? lobbyIdText.fontSizeMax : lobbyIdText.fontSize;
        lobbyIdText.fontSizeMax = Mathf.Max(1f, defaultSize);
        lobbyIdText.fontSizeMin = Mathf.Max(1f, defaultSize * 0.75f);
        lobbyIdText.textWrappingMode = TextWrappingModes.Normal;
        lobbyIdText.maxVisibleLines = 2;
        lobbyIdText.overflowMode = TextOverflowModes.Ellipsis;
        lobbyIdText.enableAutoSizing = true;
        SetLobbyId(null);
    }

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

    public void DisplayLobbyInfo(
     LobbyViewData lobbyViewData,
     bool canOpenHostSettings,
     bool canStartLobby)
    {
        if (lobbyViewData == null)
        {
            SetLobbyId(null);
            return;
        }

        currentLobbyViewData = lobbyViewData;

        bool controlsInteractable =
            lobbyViewData.lobbyState == LobbyState.Open;

        SetLobbyTitle(lobbyViewData);
        SetLobbyId(lobbyViewData.lobbyId);
        SetLeaveInteractable(controlsInteractable);
        SetStartVisible(canStartLobby);
        SetHostSettingsVisible(canOpenHostSettings);
        UpdateTimerDisplay();
    }

    private void SetLeaveInteractable(bool isInteractable)
    {
        if (leaveButton != null)
        {
            leaveButton.interactable = isInteractable;
        }
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

        if (!lobbyTimerText.gameObject.activeSelf)
        {
            lobbyTimerText.gameObject.SetActive(true);
        }

        bool layoutChanged = ConfigureTimerLayout(seconds);
        if (timerMinuteDigits == 0 || (!layoutChanged && displayedTimerSeconds == seconds))
        {
            return;
        }

        displayedTimerSeconds = seconds;
        lobbyTimerText.text = string.Format(
            CultureInfo.InvariantCulture,
            "<pos={0:0.###}>{1}<pos={2:0.###}>{3}",
            timerCaptionPosition, TimerCaption, timerCountdownPosition, FormatTime(seconds));
    }

    private bool ConfigureTimerLayout(int _seconds)
    {
        if (timerRect == null || lobbyTimerText.font == null)
        {
            return false;
        }

        Vector2 size = timerRect.rect.size;
        Vector4 margins = lobbyTimerText.margin;
        float width = size.x - margins.x - margins.z;
        float height = size.y - margins.y - margins.w;
        if (width <= 0f || height <= 0f)
        {
            return false;
        }

        int minuteDigits = 1;
        for (int minutes = _seconds / 60; minutes >= 10; minutes /= 10)
        {
            minuteDigits++;
        }

        if (timerMinuteDigits >= minuteDigits && timerLayoutFont == lobbyTimerText.font &&
            timerLayoutSize == size && timerLayoutMargins == margins)
        {
            return false;
        }

        timerMinuteDigits = Mathf.Max(timerMinuteDigits, minuteDigits);
        timerLayoutFont = lobbyTimerText.font;
        timerLayoutSize = size;
        timerLayoutMargins = margins;

        lobbyTimerText.enableAutoSizing = false;
        lobbyTimerText.fontSize = Mathf.Max(1f, timerMaximumFontSize);
        lobbyTimerText.horizontalAlignment = HorizontalAlignmentOptions.Left;
        lobbyTimerText.textWrappingMode = TextWrappingModes.NoWrap;
        lobbyTimerText.maxVisibleLines = 1;
        lobbyTimerText.characterWidthAdjustment = 0f;
        lobbyTimerText.richText = true;

        char widestDigit = '0';
        float widestDigitWidth = 0f;
        for (char digit = '0'; digit <= '9'; digit++)
        {
            float digitWidth = MeasureTimerText(digit.ToString()).x;
            if (digitWidth > widestDigitWidth)
            {
                widestDigit = digit;
                widestDigitWidth = digitWidth;
            }
        }

        string referenceTime = new string(widestDigit, timerMinuteDigits) + ":" + new string(widestDigit, 2);
        string referenceText = TimerCaption + "  " + referenceTime;
        Vector2 referenceSize = MeasureTimerText(referenceText);
        float scale = Mathf.Min(1f, width / Mathf.Max(1f, referenceSize.x), height / Mathf.Max(1f, referenceSize.y));
        lobbyTimerText.fontSize = Mathf.Clamp(
            timerMaximumFontSize * scale,
            Mathf.Max(1f, timerMinimumFontSize),
            Mathf.Max(1f, timerMaximumFontSize));

        float referenceWidth = MeasureTimerText(referenceText).x;
        float captionWidth = MeasureTimerText(TimerCaption + "  " + widestDigit).x -
            MeasureTimerText(widestDigit.ToString()).x;
        timerCaptionPosition = Mathf.Max(0f, (width - referenceWidth) * 0.5f);
        timerCountdownPosition = timerCaptionPosition + captionWidth;
        return true;
    }

    private Vector2 MeasureTimerText(string _value)
    {
        Vector2 preferredSize = lobbyTimerText.GetPreferredValues(_value);
        Vector4 margins = lobbyTimerText.margin;
        preferredSize.x -= Mathf.Max(0f, margins.x) + Mathf.Max(0f, margins.z);
        preferredSize.y -= Mathf.Max(0f, margins.y) + Mathf.Max(0f, margins.w);
        return preferredSize;
    }

    public void HideTimer()
    {
        displayedTimerSeconds = -1;
        timerMinuteDigits = 0;
        if (lobbyTimerText != null)
        {
            lobbyTimerText.gameObject.SetActive(false);
        }
    }

    private void SetLobbyId(string lobbyId)
    {
        if (lobbyIdText == null)
        {
            return;
        }

        bool hasId = !string.IsNullOrWhiteSpace(lobbyId);
        string displayId = hasId ? lobbyId.Trim() : string.Empty;
        if (lobbyIdDisplayLength > 0 && displayId.Length > lobbyIdDisplayLength)
        {
            displayId = displayId.Substring(0, lobbyIdDisplayLength);
        }

        lobbyIdText.text = hasId ? $"Lobby {displayId}" : string.Empty;
        lobbyIdText.gameObject.SetActive(hasId);
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
