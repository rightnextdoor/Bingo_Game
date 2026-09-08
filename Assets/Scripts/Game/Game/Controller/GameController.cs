using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GameController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Sections")]
    [SerializeField] private GameHeaderController headerController;
    [SerializeField] private GameBoardSectionController boardSectionController;
    [SerializeField] private BingoCheckAnimationController bingoCheckAnimationController;
    [SerializeField] private LobbyPlayerListController playerListController;
    [SerializeField] private GameInfoController gameInfoController;
    [SerializeField] private LobbyCustomPanelController customPanelController;
    [SerializeField] private RiskDecisionPopupController riskDecisionPopupController;

    #endregion

    #region Private Fields

    private readonly List<PlayerListPlayerData> visiblePlayers = new List<PlayerListPlayerData>();
    private readonly Dictionary<string, HashSet<int>> markedCellsByUserId =
        new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
    private readonly BingoBoardPatternTracker boardPatternTracker =
        new BingoBoardPatternTracker();
    private GameBallDisplayController ballDisplayController;
    private Coroutine bindRoutine;
    private string trackedPatternGameId = string.Empty;
    private bool isBingoCheckPending;
    private GameSessionData displayedGameSession;
    private string riskNotificationGameId = string.Empty;
    private int previousRiskRemainingSeconds = -1;
    private double lastRiskSubmitNotificationEndTime;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ballDisplayController = GetComponentInChildren<GameBallDisplayController>(true);

        if (ballDisplayController == null && transform.root != null)
        {
            ballDisplayController =
                transform.root.GetComponentInChildren<GameBallDisplayController>(true);
        }

        if (bingoCheckAnimationController == null)
        {
            bingoCheckAnimationController =
                GetComponentInChildren<BingoCheckAnimationController>(true);
        }

        if (bingoCheckAnimationController == null && transform.root != null)
        {
            bingoCheckAnimationController =
                transform.root.GetComponentInChildren<BingoCheckAnimationController>(true);
        }

        if (riskDecisionPopupController == null && transform.root != null)
        {
            riskDecisionPopupController =
                transform.root.GetComponentInChildren<RiskDecisionPopupController>(true);
        }
    }

    private void OnEnable()
    {
        SessionPauseManager.PauseChanged -= OnSessionPauseChanged;
        SessionPauseManager.PauseChanged += OnSessionPauseChanged;
        SubscribeToHeader();
        SubscribeToBoardSection();
        SubscribeToRiskDecisionPopup();
        bindRoutine = StartCoroutine(BindWhenGameIsReady());
    }

    private void OnDisable()
    {
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        UnsubscribeFromHeader();
        UnsubscribeFromBoardSection();
        UnsubscribeFromRiskDecisionPopup();
        UnsubscribeFromGameSessionManager();
        SessionPauseManager.PauseChanged -= OnSessionPauseChanged;
        bingoCheckAnimationController?.StopAndClear();
        isBingoCheckPending = false;
    }

    private void Update()
    {
        UpdateRiskTimeNotifications();
    }

    #endregion

    #region Game Display

    public void DisplayGameInfo(GameSessionData gameSessionData)
    {
        if (gameSessionData == null)
        {
            ClearDisplay();
            return;
        }

        bool isNewGame = !string.Equals(
            riskNotificationGameId,
            gameSessionData.gameId,
            StringComparison.Ordinal);

        displayedGameSession = new GameSessionData(gameSessionData);

        if (isNewGame)
        {
            ResetRiskNotificationTracking(gameSessionData);
        }

        GameModeManager gameModeManager = GameModeManager.instance;
        BingoGameModeData gameModeData = gameModeManager != null
            ? gameModeManager.GetGameModeData(gameSessionData.gameModeType)
            : null;

        string gameName = gameModeData != null && !string.IsNullOrWhiteSpace(gameModeData.GameName)
            ? gameModeData.GameName
            : gameSessionData.gameModeType.ToString();

        headerController?.DisplayGameInfo(gameSessionData, gameName);
        ballDisplayController?.DisplayGameInfo(gameSessionData);
        EnsureBoardPatternTracker(gameSessionData);
        DisplayPlayerBoard(gameSessionData);
        DisplayPlayerList(gameSessionData);
        DisplayGameModeInfo(gameSessionData, gameModeData, gameName, gameModeManager);
        DisplayCustomLobbyInfo(gameSessionData);
        ShowRiskSubmitNotificationIfNeeded(gameSessionData);
        CloseRiskDecisionPopupIfResolved(gameSessionData);
    }

    public void SetTimerSeconds(float remainingSeconds)
    {
        headerController?.SetTimerSeconds(remainingSeconds);
    }

    public void HideTimer()
    {
        headerController?.HideTimer();
    }

    private void DisplayPlayerList(GameSessionData gameSessionData)
    {
        visiblePlayers.Clear();
        bool localPlayerIsHost = gameSessionData?.GetPlayer(UserManager.instance?.UserId)?.isLobbyHost == true;

        if (gameSessionData?.players != null)
        {
            AddVisibleHost(gameSessionData.players, gameSessionData, localPlayerIsHost);

            for (int i = 0; i < gameSessionData.players.Count; i++)
            {
                GamePlayerData playerData = gameSessionData.players[i];

                if (playerData == null || playerData.isLobbyHost)
                {
                    continue;
                }

                AddVisiblePlayer(playerData, gameSessionData, localPlayerIsHost);
            }
        }

        playerListController?.DisplayPlayers(visiblePlayers, visiblePlayers.Count);
    }

    private void AddVisibleHost(
        IReadOnlyList<GamePlayerData> players,
        GameSessionData gameSessionData,
        bool localPlayerIsHost)
    {
        for (int i = 0; i < players.Count; i++)
        {
            GamePlayerData playerData = players[i];

            if (playerData != null && playerData.isLobbyHost)
            {
                AddVisiblePlayer(playerData, gameSessionData, localPlayerIsHost);
                return;
            }
        }
    }

    private void AddVisiblePlayer(
        GamePlayerData gamePlayerData,
        GameSessionData gameSessionData,
        bool localPlayerIsHost)
    {
        if (gamePlayerData == null || !gamePlayerData.HasValidPlayer || !gamePlayerData.isGameSceneReady)
        {
            return;
        }

        PlayerListPlayerData playerData = new PlayerListPlayerData
        {
            userId = gamePlayerData.userId ?? string.Empty,
            userTag = gamePlayerData.userTag,
            playerName = gamePlayerData.playerName ?? string.Empty,
            iconId = gamePlayerData.iconId ?? string.Empty,
            isHost = gamePlayerData.isLobbyHost,
            isReady = true,
            boardData = new LobbyBoardData(gamePlayerData.boardData),
            markedCellIndices = GetMarkedCellSnapshot(gamePlayerData.userId),
            gameplayStatusText = ResolveGameplayStatusText(
                gamePlayerData,
                gameSessionData),
            canKick = false,
            showBotIcon = localPlayerIsHost && gamePlayerData.userTag == UserTag.Bot,
            showReadyIcon = false
        };

        visiblePlayers.Add(playerData);
    }

    private void DisplayGameModeInfo(
        GameSessionData gameSessionData,
        BingoGameModeData gameModeData,
        string gameName,
        GameModeManager gameModeManager)
    {
        string gameDescription = gameModeData != null && !string.IsNullOrWhiteSpace(gameModeData.Description)
            ? gameModeData.Description
            : "No game information is available for this game mode.";

        string ruleDescription = string.Empty;

        if (gameSessionData.hasRule)
        {
            BingoGameRuleData ruleData = gameModeManager != null
                ? gameModeManager.GetGameRuleData(gameSessionData.ruleType)
                : null;

            ruleDescription = ruleData != null && !string.IsNullOrWhiteSpace(ruleData.Description)
                ? ruleData.Description
                : "No rule description is available for this game mode.";
        }

        gameInfoController?.ShowGameInfo(
            gameName,
            gameDescription,
            gameSessionData.ballCountType,
            gameSessionData.hasRule,
            ruleDescription,
            gameSessionData.patternTypes,
            true);
    }

    private void DisplayCustomLobbyInfo(GameSessionData gameSessionData)
    {
        LobbyViewData lobbyViewData = new LobbyViewData
        {
            lobbyId = gameSessionData.lobbyId ?? string.Empty,
            playMode = gameSessionData.playMode,
            lobbyName = gameSessionData.lobbyName ?? string.Empty,
            roomCode = gameSessionData.roomCode ?? string.Empty,
            hasPassword = gameSessionData.hasPassword,
            lobbyPassword = gameSessionData.lobbyPassword ?? string.Empty
        };

        customPanelController?.DisplayLobbyInfo(lobbyViewData);
    }

    private void ClearDisplay()
    {
        visiblePlayers.Clear();
        markedCellsByUserId.Clear();
        boardPatternTracker.Clear();
        trackedPatternGameId = string.Empty;
        isBingoCheckPending = false;
        displayedGameSession = null;
        riskNotificationGameId = string.Empty;
        previousRiskRemainingSeconds = -1;
        lastRiskSubmitNotificationEndTime = 0d;
        headerController?.ClearHeader();
        boardSectionController?.ClearBoard();
        boardSectionController?.SetBoardInteractable(false);
        boardSectionController?.SetBingoInteractable(false);
        playerListController?.DisplayPlayers(visiblePlayers, 0);
        gameInfoController?.ClearInfo();
        customPanelController?.DisplayLobbyInfo(null);
        ballDisplayController?.ClearDisplay();
    }

    #endregion

    #region Board

    private void DisplayPlayerBoard(GameSessionData gameSessionData)
    {
        if (boardSectionController == null)
        {
            return;
        }

        string localUserId = UserManager.instance?.UserId;
        GamePlayerData localPlayer = gameSessionData?.GetPlayer(localUserId);

        bool boardDisplayed =
            localPlayer?.boardData != null &&
            boardSectionController.DisplayBoard(localPlayer.boardData);

        if (!boardDisplayed)
        {
            boardSectionController.ClearBoard();
        }

        bool playerCanUseBoard =
            boardDisplayed &&
            !SessionPauseManager.IsPaused &&
            localPlayer.gameStatus == GamePlayerStatus.Eligible &&
            !localPlayer.isRiskDecisionPending &&
            gameSessionData.gameState != GameSessionState.Completed;
        bool playerCanSubmitBingo =
            playerCanUseBoard &&
            !isBingoCheckPending &&
            gameSessionData.gamePlayController?.CanAcceptBingoChecks == true;

        boardSectionController.SetBoardInteractable(playerCanUseBoard);
        boardSectionController.SetBingoInteractable(playerCanSubmitBingo);
    }

    private void SubscribeToBoardSection()
    {
        if (boardSectionController == null)
        {
            return;
        }

        boardSectionController.BingoRequested -= OnBingoRequested;
        boardSectionController.BingoRequested += OnBingoRequested;

        boardSectionController.MarkedCellChanged -= OnBoardMarkedCellChanged;
        boardSectionController.MarkedCellChanged += OnBoardMarkedCellChanged;
    }

    private void UnsubscribeFromBoardSection()
    {
        if (boardSectionController != null)
        {
            boardSectionController.BingoRequested -= OnBingoRequested;
            boardSectionController.MarkedCellChanged -= OnBoardMarkedCellChanged;
        }
    }

    private void OnBoardMarkedCellChanged(int cellIndex, bool isMarked)
    {
        if (SessionPauseManager.IsPaused)
        {
            DisplayPlayerBoard(GameSessionManager.instance?.CurrentGameSession);
            return;
        }

        if (GameSessionManager.instance == null ||
            !GameSessionManager.instance.SetCurrentPlayerMarkedCell(cellIndex, isMarked))
        {
            Debug.LogWarning(
                $"[GameController] Could not update marked cell {cellIndex} for the current player.");
        }
    }

    private void OnBingoRequested(
        LobbyBoardData boardData,
        IReadOnlyList<int> markedCellIndices)
    {
        if (SessionPauseManager.IsPaused)
        {
            return;
        }

        if (boardData?.cellNumbers == null)
        {
            Debug.LogWarning("[GameController] Bingo was pressed, but the board data was unavailable.");
            return;
        }

        if (isBingoCheckPending)
        {
            return;
        }

        List<int> sortedCellIndices = markedCellIndices != null
            ? new List<int>(markedCellIndices)
            : new List<int>();
        sortedCellIndices.Sort();

        isBingoCheckPending = true;
        boardSectionController?.SetBingoInteractable(false);

        if (GameSessionManager.instance == null ||
            !GameSessionManager.instance.SubmitCurrentPlayerBingoCheck(
                new LobbyBoardData(boardData),
                sortedCellIndices))
        {
            isBingoCheckPending = false;
            DisplayPlayerBoard(GameSessionManager.instance?.CurrentGameSession);
            Debug.LogWarning("[GameController] The Bingo check request could not be sent.");
        }
    }

    #endregion

    #region Session Binding

    private IEnumerator BindWhenGameIsReady()
    {
        while (GameSessionManager.instance == null ||
               !GameSessionManager.instance.HasEnteredGame ||
               GameSessionManager.instance.CurrentGameSession == null ||
               GameModeManager.instance == null ||
               !GameModeManager.instance.IsReady)
        {
            yield return null;
        }

        GameSessionManager gameSessionManager = GameSessionManager.instance;

        gameSessionManager.GameSessionUpdated -= OnGameSessionUpdated;
        gameSessionManager.GameSessionUpdated += OnGameSessionUpdated;

        gameSessionManager.GamePlayerMarkedCellChanged -= OnGamePlayerMarkedCellChanged;
        gameSessionManager.GamePlayerMarkedCellChanged += OnGamePlayerMarkedCellChanged;

        gameSessionManager.BingoCheckResolved -= OnBingoCheckResolved;
        gameSessionManager.BingoCheckResolved += OnBingoCheckResolved;

        DisplayGameInfo(gameSessionManager.CurrentGameSession);
        bindRoutine = null;
    }

    private void OnGameSessionUpdated(GameSessionData gameSessionData)
    {
        DisplayGameInfo(gameSessionData);
    }

    private void OnGamePlayerMarkedCellChanged(
        GamePlayerMarkedCellChangedData updateData)
    {
        if (updateData == null || string.IsNullOrWhiteSpace(updateData.userId))
        {
            return;
        }

        if (!markedCellsByUserId.TryGetValue(
                updateData.userId,
                out HashSet<int> markedCells))
        {
            markedCells = new HashSet<int>();
            markedCellsByUserId.Add(updateData.userId, markedCells);
        }

        if (updateData.isMarked)
        {
            markedCells.Add(updateData.cellIndex);
        }
        else
        {
            markedCells.Remove(updateData.cellIndex);
        }

        playerListController?.SetPlayerMarkedCell(
            updateData.userId,
            updateData.cellIndex,
            updateData.isMarked);
    }

    private List<int> GetMarkedCellSnapshot(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            !markedCellsByUserId.TryGetValue(userId, out HashSet<int> markedCells))
        {
            return new List<int>();
        }

        List<int> snapshot = new List<int>(markedCells);
        snapshot.Sort();
        return snapshot;
    }

    private void OnBingoCheckResolved(GameBingoCheckResolvedData resolvedData)
    {
        string localUserId = UserManager.instance?.UserId;

        if (resolvedData == null ||
            string.IsNullOrWhiteSpace(localUserId) ||
            (!string.IsNullOrWhiteSpace(resolvedData.userId) &&
             !string.Equals(resolvedData.userId, localUserId, StringComparison.Ordinal)))
        {
            return;
        }

        isBingoCheckPending = false;

        if (!resolvedData.wasAccepted)
        {
            DisplayPlayerBoard(GameSessionManager.instance?.CurrentGameSession);
            Debug.LogWarning(
                $"[GameController] Bingo check rejected: {resolvedData.failureMessage}");
            return;
        }

        boardPatternTracker.ApplyAvailablePatterns(
            resolvedData.availablePatternTypes);
        DisplayPlayerBoard(GameSessionManager.instance?.CurrentGameSession);

        if (resolvedData.playerStatus != GamePlayerStatus.Eligible)
        {
            boardSectionController?.SetBoardInteractable(false);
            boardSectionController?.SetBingoInteractable(false);
        }

        if (resolvedData.checkResult == null || bingoCheckAnimationController == null)
        {
            NotifyBingoCheckAnimationCompleted();
            return;
        }

        BingoCheckResult checkResult = resolvedData.checkResult;
        GamePlayerStatus playerStatus = resolvedData.playerStatus;
        bool requiresRiskDecision = resolvedData.requiresRiskDecision;
        int latePatternCount = resolvedData.latePatternCount;
        bingoCheckAnimationController.PlayCheckAnimation(
            checkResult,
            () => PlayFinalManualCheckAnimation(
                checkResult,
                playerStatus,
                requiresRiskDecision,
                latePatternCount));
    }

    private void PlayFinalManualCheckAnimation(
        BingoCheckResult checkResult,
        GamePlayerStatus playerStatus,
        bool requiresRiskDecision,
        int latePatternCount)
    {
        if (checkResult == null || bingoCheckAnimationController == null)
        {
            NotifyBingoCheckAnimationCompleted();
            return;
        }

        if (latePatternCount > 0 && !checkResult.HasFailedPattern)
        {
            NotificationService.instance?.SendLocal(
                UIMessageType.RiskPatternSubmittedLate,
                "One or more Bingo patterns were submitted too late. No points were awarded for those patterns.");
        }

        if (requiresRiskDecision)
        {
            bingoCheckAnimationController.PlayWinnerAnimation(
                checkResult.GetWinningPatterns());
            NotifyBingoCheckAnimationCompleted();

            GameSessionData currentSession =
                GameSessionManager.instance?.CurrentGameSession;
            GamePlayerData localPlayer =
                currentSession?.GetPlayer(UserManager.instance?.UserId);

            if (currentSession?.gameState == GameSessionState.InProgress &&
                localPlayer?.isRiskDecisionPending == true)
            {
                OpenRiskDecisionPopup();
            }

            return;
        }

        if (playerStatus == GamePlayerStatus.Eligible)
        {
            bingoCheckAnimationController.ContinuePlaying();
            NotifyBingoCheckAnimationCompleted();
            return;
        }

        switch (playerStatus)
        {
            case GamePlayerStatus.Won:
                bingoCheckAnimationController.PlayWinnerAnimation(
                    checkResult.GetWinningPatterns());
                break;

            case GamePlayerStatus.Lost:
                bingoCheckAnimationController.PlayLoserAnimation(
                    checkResult.GetFailedPatterns());
                break;
        }

        NotifyBingoCheckAnimationCompleted();
    }

    private void NotifyBingoCheckAnimationCompleted()
    {
        if (GameSessionManager.instance == null ||
            !GameSessionManager.instance.CompleteCurrentPlayerBingoCheckAnimation())
        {
            Debug.LogWarning(
                "[GameController] The completed Bingo check animation could not be sent to the Game authority.");
        }
    }

    private void EnsureBoardPatternTracker(GameSessionData gameSessionData)
    {
        if (gameSessionData == null || string.IsNullOrWhiteSpace(gameSessionData.gameId))
        {
            boardPatternTracker.Clear();
            trackedPatternGameId = string.Empty;
            return;
        }

        if (string.Equals(
                trackedPatternGameId,
                gameSessionData.gameId,
                StringComparison.Ordinal))
        {
            return;
        }

        trackedPatternGameId = gameSessionData.gameId;
        boardPatternTracker.Setup(gameSessionData.patternTypes);
    }

    private static string ResolveGameplayStatusText(
        GamePlayerData playerData,
        GameSessionData gameSessionData)
    {
        if (playerData == null)
        {
            return string.Empty;
        }

        bool isRisk =
            gameSessionData?.gameModeType == BingoGameModeType.Risk ||
            (gameSessionData?.hasRule == true &&
             gameSessionData.ruleType == BingoRuleType.Risk);

        if (isRisk)
        {
            return playerData.gameStatus switch
            {
                GamePlayerStatus.Won => $"WON - SCORE: {playerData.currentMatchScore}",
                GamePlayerStatus.Lost => $"LOST - SCORE: {playerData.currentMatchScore}",
                _ => $"SCORE: {playerData.currentMatchScore}"
            };
        }

        if (playerData.gameStatus == GamePlayerStatus.Eligible)
        {
            return string.Empty;
        }

        if (playerData.gameStatus == GamePlayerStatus.Won)
        {
            return "WON";
        }

        bool isDeath =
            gameSessionData?.gameModeType == BingoGameModeType.Death ||
            (gameSessionData?.hasRule == true &&
             gameSessionData.ruleType == BingoRuleType.Elimination);

        return isDeath ? "OUT" : "LOST";
    }

    private void UnsubscribeFromGameSessionManager()
    {
        if (GameSessionManager.instance != null)
        {
            GameSessionManager.instance.GameSessionUpdated -= OnGameSessionUpdated;
            GameSessionManager.instance.GamePlayerMarkedCellChanged -= OnGamePlayerMarkedCellChanged;
            GameSessionManager.instance.BingoCheckResolved -= OnBingoCheckResolved;
        }
    }

    #endregion

    #region Risk

    private void SubscribeToRiskDecisionPopup()
    {
        if (riskDecisionPopupController == null)
        {
            return;
        }

        riskDecisionPopupController.DecisionSubmitted -= OnRiskDecisionSubmitted;
        riskDecisionPopupController.DecisionSubmitted += OnRiskDecisionSubmitted;
    }

    private void UnsubscribeFromRiskDecisionPopup()
    {
        if (riskDecisionPopupController != null)
        {
            riskDecisionPopupController.DecisionSubmitted -= OnRiskDecisionSubmitted;
        }
    }

    private void OnRiskDecisionSubmitted(bool endPlayerGame)
    {
        if (!endPlayerGame)
        {
            bingoCheckAnimationController?.ContinuePlaying();
        }

        DisplayPlayerBoard(GameSessionManager.instance?.CurrentGameSession);
    }

    private void OpenRiskDecisionPopup()
    {
        if (PopupManager.instance == null)
        {
            Debug.LogWarning(
                "[GameController] Risk decision popup could not open because PopupManager was not found.");
            return;
        }

        PopupManager.instance.OpenRiskDecisionPopup();
    }

    private void CloseRiskDecisionPopupIfResolved(GameSessionData gameSessionData)
    {
        if (PopupManager.instance?.ActivePopupId != PopupId.RiskDecision)
        {
            return;
        }

        GamePlayerData localPlayer =
            gameSessionData?.GetPlayer(UserManager.instance?.UserId);

        if (localPlayer == null ||
            !localPlayer.isRiskDecisionPending ||
            gameSessionData.gameState == GameSessionState.Completed)
        {
            PopupManager.instance.CloseActivePopup();
        }
    }

    private void ResetRiskNotificationTracking(GameSessionData gameSessionData)
    {
        riskNotificationGameId = gameSessionData?.gameId ?? string.Empty;
        lastRiskSubmitNotificationEndTime = 0d;

        GamePlayTimer riskTimer = gameSessionData?.gamePlayController?.RiskTimer;
        previousRiskRemainingSeconds = riskTimer?.IsActive == true
            ? Mathf.Max(0, Mathf.CeilToInt(riskTimer.GetRemainingSeconds()))
            : -1;
    }

    private void ShowRiskSubmitNotificationIfNeeded(GameSessionData gameSessionData)
    {
        if (!RiskGameplayAuthority.IsRiskGame(gameSessionData))
        {
            return;
        }

        GamePlayerData localPlayer =
            gameSessionData.GetPlayer(UserManager.instance?.UserId);

        if (localPlayer == null ||
            !localPlayer.isSubmitTimerActive ||
            localPlayer.submitTimerEndTime <= GamePlayTimer.GetCurrentTime() ||
            Math.Abs(
                localPlayer.submitTimerEndTime -
                lastRiskSubmitNotificationEndTime) < 0.01d)
        {
            return;
        }

        lastRiskSubmitNotificationEndTime = localPlayer.submitTimerEndTime;
        NotificationService.instance?.SendLocal(
            UIMessageType.RiskPatternAvailable,
            "You have a Bingo pattern. Submit it before time runs out or it will not award points.");
    }

    private void UpdateRiskTimeNotifications()
    {
        if (SessionPauseManager.IsPaused)
        {
            return;
        }

        if (!RiskGameplayAuthority.IsRiskGame(displayedGameSession))
        {
            previousRiskRemainingSeconds = -1;
            return;
        }

        GamePlayTimer riskTimer = displayedGameSession.gamePlayController?.RiskTimer;

        if (riskTimer?.IsActive != true)
        {
            return;
        }

        int currentSeconds = Mathf.Max(
            0,
            Mathf.CeilToInt(riskTimer.GetRemainingSeconds()));

        if (previousRiskRemainingSeconds < 0)
        {
            previousRiskRemainingSeconds = currentSeconds;
            return;
        }

        for (int crossedSecond = previousRiskRemainingSeconds - 1;
             crossedSecond >= currentSeconds;
             crossedSecond--)
        {
            if (crossedSecond <= 0 ||
                (crossedSecond != 30 &&
                 crossedSecond != 10 &&
                 crossedSecond % 60 != 0))
            {
                continue;
            }

            string message = crossedSecond >= 60
                ? $"Risk game time: {crossedSecond / 60} minute{(crossedSecond == 60 ? string.Empty : "s")} remaining."
                : $"Risk game time: {crossedSecond} seconds remaining.";

            NotificationService.instance?.SendLocal(
                UIMessageType.RiskTimeRemaining,
                message);
        }

        previousRiskRemainingSeconds = currentSeconds;
    }

    private void OnSessionPauseChanged(bool isPaused)
    {
        DisplayPlayerBoard(GameSessionManager.instance?.CurrentGameSession);
    }

    #endregion

    #region Header

    private void SubscribeToHeader()
    {
        if (headerController == null)
        {
            return;
        }

        headerController.LeaveRequested -= LeaveGame;
        headerController.LeaveRequested += LeaveGame;
    }

    private void UnsubscribeFromHeader()
    {
        if (headerController != null)
        {
            headerController.LeaveRequested -= LeaveGame;
        }
    }

    private void LeaveGame()
    {
        headerController?.SetLeaveInteractable(false);

        if (GameSessionManager.instance != null)
        {
            GameSessionManager.instance.LeaveCurrentGame();
            return;
        }

        UserManager.instance?.ClearLastGameId();
        GameSceneManager.instance?.LoadMainScene();
    }

    #endregion
}
