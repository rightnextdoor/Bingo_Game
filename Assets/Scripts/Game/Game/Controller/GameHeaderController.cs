using System;
using BingoGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameHeaderController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Text")]
    [SerializeField] private TMP_Text gameTitleText;
    [SerializeField] private TMP_Text gameTimerText;
    [SerializeField] private TMP_Text submitTimerText;
    [SerializeField] private TMP_Text riskTimerText;
    [SerializeField] private TMP_Text localScoreStatusText;

    [Header("Text Fitting")]
    [Tooltip("Smallest allowed size relative to each label's original font size. Long text is truncated with an ellipsis if it still cannot fit.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float minimumFontSizeRatio = 0.75f;

    [Header("Buttons")]
    [SerializeField] private Button leaveButton;

    #endregion

    public event Action LeaveRequested;

    private GameSessionData currentGameSession;
    private FixedTimerText gameTimerDisplay;
    private FixedTimerText submitTimerDisplay;
    private FixedTimerText riskTimerDisplay;
    private int displayedPlayersLeft = -1;
    private int displayedPlayerTotal = -1;

    #region Unity Lifecycle

    private void Awake()
    {
        ConfigureTextFitting();
        gameTimerDisplay = new FixedTimerText(gameTimerText);
        submitTimerDisplay = new FixedTimerText(submitTimerText);
        riskTimerDisplay = new FixedTimerText(riskTimerText);
        ClearHeader();
    }

    private void OnEnable()
    {
        if (leaveButton != null)
        {
            leaveButton.onClick.AddListener(OnLeaveClicked);
        }
    }

    private void OnDisable()
    {
        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveListener(OnLeaveClicked);
        }
    }

    private void Update()
    {
        if (ConnectionRecoveryManager.instance?.IsRecoveringInScene == true)
        {
            return;
        }

        RefreshTimersAndStatus();
    }

    #endregion

    #region Display

    public void DisplayGameInfo(GameSessionData gameSessionData, string gameName)
    {
        if (gameSessionData == null)
        {
            ClearHeader();
            return;
        }

        currentGameSession = new GameSessionData(gameSessionData);

        if (gameTitleText != null)
        {
            string resolvedGameName = string.IsNullOrWhiteSpace(gameName)
                ? gameSessionData.gameModeType.ToString()
                : gameName;

            gameTitleText.text = $"{gameSessionData.playMode} - {resolvedGameName}";
        }

        SetLeaveInteractable(true);
        RefreshTimersAndStatus();
    }

    public void RefreshGameState(GameSessionData _gameSessionData)
    {
        if (_gameSessionData == null)
        {
            ClearHeader();
            return;
        }

        currentGameSession = new GameSessionData(_gameSessionData);
        SetLeaveInteractable(true);
        RefreshTimersAndStatus();
    }

    public void SetTimerSeconds(float remainingSeconds)
    {
        if (gameTimerText == null)
        {
            return;
        }

        int seconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        GamePlayController playController = currentGameSession?.gamePlayController;
        string timerLabel = playController?.Phase == GamePlayPhase.FirstBallCountdown
            ? "Start Timer:"
            : playController?.IsFinalBallCountdown == true
                ? "Game Ends In:"
                : "Next Ball:";

        gameTimerDisplay?.SetSeconds(timerLabel, seconds);
    }

    public void HideTimer()
    {
        gameTimerDisplay?.Hide();
    }

    public void SetLeaveInteractable(bool isInteractable)
    {
        if (leaveButton != null)
        {
            leaveButton.interactable = isInteractable;
        }
    }

    public void ClearHeader()
    {
        currentGameSession = null;
        displayedPlayersLeft = -1;
        displayedPlayerTotal = -1;

        if (gameTitleText != null)
        {
            gameTitleText.text = string.Empty;
        }

        HideTimer();
        submitTimerDisplay?.Hide();
        riskTimerDisplay?.Hide();
        SetTextVisible(localScoreStatusText, false);
        SetLeaveInteractable(false);
    }

    #endregion

    #region Button Events

    private void OnLeaveClicked()
    {
        LeaveRequested?.Invoke();
    }

    #endregion

    #region Helpers

    private void ConfigureTextFitting()
    {
        ConfigureTextFitting(gameTitleText);
        ConfigureTextFitting(gameTimerText);
        ConfigureTextFitting(submitTimerText, 2);
        ConfigureTextFitting(riskTimerText, 2);
        ConfigureTextFitting(localScoreStatusText, 2);

        if (leaveButton != null)
        {
            foreach (TMP_Text buttonText in leaveButton.GetComponentsInChildren<TMP_Text>(true))
            {
                ConfigureTextFitting(buttonText);
            }
        }
    }

    private void ConfigureTextFitting(TMP_Text text, int maximumLines = 1)
    {
        if (text == null)
        {
            return;
        }

        float defaultFontSize = Mathf.Max(1f, text.enableAutoSizing ? text.fontSizeMax : text.fontSize);
        text.fontSizeMax = defaultFontSize;
        text.fontSizeMin = Mathf.Max(1f, defaultFontSize * Mathf.Clamp(minimumFontSizeRatio, 0.5f, 1f));
        text.textWrappingMode = maximumLines > 1
            ? TextWrappingModes.Normal
            : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.maxVisibleLines = maximumLines;
        text.characterWidthAdjustment = 0f;
        text.enableAutoSizing = true;
    }

    private void RefreshTimersAndStatus()
    {
        GamePlayController playController = currentGameSession?.gamePlayController;
        GamePlayTimer ballTimer = playController?.BallTimer;

        if (ballTimer != null && ballTimer.IsActive && playController.Phase != GamePlayPhase.Ended)
        {
            SetTimerSeconds(ballTimer.GetRemainingSeconds());
        }
        else
        {
            HideTimer();
        }

        RefreshSubmitTimer();
        RefreshRiskTimerAndPlayerStatus();
    }

    private void RefreshSubmitTimer()
    {
        GamePlayController playController = currentGameSession?.gamePlayController;

        if (currentGameSession?.gameState == GameSessionState.Completed ||
            playController?.Phase == GamePlayPhase.Ended)
        {
            submitTimerDisplay?.ShowPlain("Game Is Over");
            return;
        }

        GamePlayerData localPlayer = currentGameSession?.GetPlayer(UserManager.instance?.UserId);

        if (localPlayer == null || !localPlayer.isSubmitTimerActive)
        {
            submitTimerDisplay?.Hide();
            return;
        }

        int seconds = Mathf.Max(
            0,
            Mathf.CeilToInt((float)(localPlayer.submitTimerEndTime - GamePlayTimer.GetCurrentTime())));

        submitTimerDisplay?.SetSeconds("Submit Bingo In:", seconds);
    }

    private void RefreshRiskTimerAndPlayerStatus()
    {
        if (currentGameSession == null)
        {
            riskTimerDisplay?.Hide();
            displayedPlayersLeft = -1;
            displayedPlayerTotal = -1;
            SetTextVisible(localScoreStatusText, false);
            return;
        }

        bool isRisk = currentGameSession.gameModeType == BingoGameModeType.Risk ||
                      (currentGameSession.hasRule && currentGameSession.ruleType == BingoRuleType.Risk);
        bool isDeath = currentGameSession.gameModeType == BingoGameModeType.Death ||
                       (currentGameSession.hasRule && currentGameSession.ruleType == BingoRuleType.Elimination);

        GamePlayTimer riskTimer = currentGameSession.gamePlayController?.RiskTimer;

        bool isRiskUiActive = isRisk && riskTimer != null && riskTimer.IsActive;

        if (isDeath)
        {
            int playerTotal = currentGameSession.players?.Count ?? 0;
            int playersLeft = currentGameSession.gameState == GameSessionState.Completed
                ? currentGameSession.GetPlayerCountWithStatus(GamePlayerStatus.Won)
                : currentGameSession.GetPlayerCountWithStatus(GamePlayerStatus.Eligible) +
                  currentGameSession.GetPlayerCountWithStatus(GamePlayerStatus.Checking);

            if (displayedPlayersLeft != playersLeft || displayedPlayerTotal != playerTotal)
            {
                displayedPlayersLeft = playersLeft;
                displayedPlayerTotal = playerTotal;
                riskTimerDisplay?.ShowPlain($"Players Left: {playersLeft} / {playerTotal}");
            }
        }
        else if (isRiskUiActive)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(riskTimer.GetRemainingSeconds()));
            displayedPlayersLeft = -1;
            displayedPlayerTotal = -1;
            riskTimerDisplay?.SetSeconds("Game Time Left:", seconds, true);
        }
        else
        {
            displayedPlayersLeft = -1;
            displayedPlayerTotal = -1;
            riskTimerDisplay?.Hide();
        }

        GamePlayerData localPlayer = currentGameSession.GetPlayer(UserManager.instance?.UserId);

        if (localPlayer == null)
        {
            SetTextVisible(localScoreStatusText, false);
            return;
        }

        if (isRisk)
        {
            string riskScoreText = localPlayer.gameStatus switch
            {
                GamePlayerStatus.Won => $"Won - Score: {localPlayer.currentMatchScore}",
                GamePlayerStatus.Lost => $"Lost - Score: {localPlayer.currentMatchScore}",
                _ => $"Score: {localPlayer.currentMatchScore}"
            };

            SetText(localScoreStatusText, riskScoreText);
            return;
        }

        if (isDeath)
        {
            string deathStatus = localPlayer.gameStatus switch
            {
                GamePlayerStatus.Won => "WON",
                GamePlayerStatus.Lost => "OUT",
                _ => "ALIVE"
            };

            SetText(localScoreStatusText, deathStatus);
            return;
        }

        if (localPlayer.gameStatus == GamePlayerStatus.Eligible)
        {
            SetTextVisible(localScoreStatusText, false);
            return;
        }

        SetText(
            localScoreStatusText,
            localPlayer.gameStatus.ToString().ToUpperInvariant());
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text == null)
        {
            return;
        }

        text.text = value ?? string.Empty;
        text.gameObject.SetActive(true);
    }

    private static void SetTextVisible(TMP_Text text, bool isVisible)
    {
        if (text != null)
        {
            text.gameObject.SetActive(isVisible);
        }
    }

    #endregion
}
