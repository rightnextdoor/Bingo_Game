using System;
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

    [Header("Buttons")]
    [SerializeField] private Button leaveButton;

    #endregion

    public event Action LeaveRequested;

    private GameSessionData currentGameSession;

    #region Unity Lifecycle

    private void Awake()
    {
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

    public void SetTimerSeconds(float remainingSeconds)
    {
        if (gameTimerText == null)
        {
            return;
        }

        int seconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        string timerLabel = currentGameSession?.gamePlayController?.Phase == GamePlayPhase.FirstBallCountdown
            ? "Start Timer"
            : "Next Ball";

        gameTimerText.gameObject.SetActive(true);
        gameTimerText.text = $"{timerLabel}: {seconds}";
    }

    public void HideTimer()
    {
        if (gameTimerText != null)
        {
            gameTimerText.gameObject.SetActive(false);
        }
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

        if (gameTitleText != null)
        {
            gameTitleText.text = string.Empty;
        }

        HideTimer();
        SetTextVisible(submitTimerText, false);
        SetTextVisible(riskTimerText, false);
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
        GamePlayerData localPlayer = currentGameSession?.GetPlayer(UserManager.instance?.UserId);

        if (localPlayer == null || !localPlayer.isSubmitTimerActive)
        {
            SetTextVisible(submitTimerText, false);
            return;
        }

        int seconds = Mathf.Max(
            0,
            Mathf.CeilToInt((float)(localPlayer.submitTimerEndTime - GamePlayTimer.GetCurrentTime())));

        SetText(submitTimerText, $"Submit Bingo In: {seconds}");
    }

    private void RefreshRiskTimerAndPlayerStatus()
    {
        if (currentGameSession == null)
        {
            SetTextVisible(riskTimerText, false);
            SetTextVisible(localScoreStatusText, false);
            return;
        }

        bool isRisk = currentGameSession.gameModeType == BingoGameModeType.Risk ||
                      (currentGameSession.hasRule && currentGameSession.ruleType == BingoRuleType.Risk);
        bool isDeath = currentGameSession.gameModeType == BingoGameModeType.Death ||
                       (currentGameSession.hasRule && currentGameSession.ruleType == BingoRuleType.Elimination);

        GamePlayTimer riskTimer = currentGameSession.gamePlayController?.RiskTimer;

        bool isRiskUiActive = isRisk && riskTimer != null && riskTimer.IsActive;

        if (isRiskUiActive)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(riskTimer.GetRemainingSeconds()));
            SetText(riskTimerText, $"Game Time Left: {FormatMinutesAndSeconds(seconds)}");
        }
        else
        {
            SetTextVisible(riskTimerText, false);
        }

        GamePlayerData localPlayer = currentGameSession.GetPlayer(UserManager.instance?.UserId);

        if (localPlayer == null)
        {
            SetTextVisible(localScoreStatusText, false);
            return;
        }

        if (isRiskUiActive)
        {
            string statusSuffix = localPlayer.gameStatus == GamePlayerStatus.Eligible
                ? string.Empty
                : $" - {localPlayer.gameStatus.ToString().ToUpperInvariant()}";
            SetText(localScoreStatusText, $"Score: {localPlayer.currentMatchScore}{statusSuffix}");
            return;
        }

        if (isDeath && localPlayer.gameStatus != GamePlayerStatus.Eligible)
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

        SetText(localScoreStatusText, localPlayer.gameStatus.ToString().ToUpperInvariant());
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

    private static string FormatMinutesAndSeconds(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        return $"{minutes:0}:{seconds:00}";
    }

    #endregion
}
