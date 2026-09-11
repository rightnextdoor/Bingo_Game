using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GamePlayController
{
    public const float RiskSubmitCutoffSeconds = 2f;

    [SerializeField] private GamePlayPhase phase;
    [SerializeField] private GameEndReason endReason;

    [Header("Setup")]
    [SerializeField] private BingoGameModeType gameModeType;
    [SerializeField] private BingoBallCountType ballCountType;
    [SerializeField] private bool useFreeCell;
    [SerializeField] private bool hasRule;
    [SerializeField] private BingoRuleType ruleType;
    [SerializeField] private GameBallController ballController = new GameBallController();
    [SerializeField] private GameRuleController ruleController = new GameRuleController();

    [Header("Timers")]
    [SerializeField] private GamePlayTimer ballTimer = new GamePlayTimer();
    [SerializeField] private GamePlayTimer riskTimer = new GamePlayTimer();
    [SerializeField] private float firstBallCountdownSeconds;
    [SerializeField] private float nextBallCountdownSeconds;
    [SerializeField] private float riskMatchDurationSeconds;

    [SerializeField] private int ballCallRequestCount;
    [SerializeField] private bool isFinalBallCountdown;
    [SerializeField] private string matchEndingCheckPlayerId = string.Empty;
    [SerializeField] private List<string> pendingCheckAnimationPlayerIds = new List<string>();
    [SerializeField] private bool isRuleCompletionAwaitingChecks;
    [SerializeField] private bool isBallPoolExhaustedAwaitingChecks;
    [SerializeField] private bool isRiskTimerExpiredAwaitingChecks;
    [SerializeField] private bool deathFinalChecksWereRequired;

    [NonSerialized] private BingoChecker bingoChecker = new BingoChecker();

    public GamePlayPhase Phase => phase;
    public GameEndReason EndReason => endReason;
    public BingoGameModeType GameModeType => gameModeType;
    public BingoBallCountType BallCountType => ballCountType;
    public bool UseFreeCell => useFreeCell;
    public GameBallController BallController => ballController;
    public GameRuleController RuleController => ruleController;
    public GamePlayTimer BallTimer => ballTimer;
    public GamePlayTimer RiskTimer => riskTimer;
    public int BallCallRequestCount => ballCallRequestCount;
    public bool IsFinalBallCountdown =>
        isFinalBallCountdown &&
        phase == GamePlayPhase.NextBallCountdown;
    public bool HasPendingCheckAnimations => pendingCheckAnimationPlayerIds != null &&
                                             pendingCheckAnimationPlayerIds.Count > 0;
    public bool IsMatchEndPendingChecks =>
        isBallPoolExhaustedAwaitingChecks ||
        isRiskTimerExpiredAwaitingChecks;
    public bool IsPlayerInputClosed =>
        phase == GamePlayPhase.Ended ||
        isRuleCompletionAwaitingChecks ||
        IsMatchEndPendingChecks;
    public bool IsBallPoolExhaustedAwaitingChecks => isBallPoolExhaustedAwaitingChecks;
    public bool IsRiskTimerExpiredAwaitingChecks => isRiskTimerExpiredAwaitingChecks;
    public bool IsRuleCompletionAwaitingChecks => isRuleCompletionAwaitingChecks;
    public bool DeathFinalChecksWereRequired => deathFinalChecksWereRequired;
    public float NextBallCountdownSeconds => nextBallCountdownSeconds;
    public float RiskMatchDurationSeconds => riskMatchDurationSeconds;
    public bool IsRiskRule => ruleController?.ActiveRuleType == BingoRuleType.Risk;
    public bool IsDeathRule => ruleController?.ActiveRuleType == BingoRuleType.Elimination;
    public bool IsRunning =>
        phase == GamePlayPhase.FirstBallCountdown ||
        phase == GamePlayPhase.NextBallCountdown;
    public bool CanAcceptBingoChecks =>
        !SessionPauseManager.IsPaused &&
        ruleController != null &&
        ruleController.IsSupported &&
        IsRunning &&
        !IsPlayerInputClosed;

    public GamePlayController()
    {
        Initialize(
            BingoGameModeType.Traditional,
            BingoBallCountType.Ball75,
            true,
            GameSettings.DefaultFirstBallCountdownSeconds,
            GameSettings.DefaultNextBallCountdownSeconds,
            GameSettings.DefaultRiskMatchDurationSeconds,
            false,
            BingoRuleType.Traditional);
    }

    public GamePlayController(GamePlayController controller)
    {
        if (controller == null)
        {
            Initialize(
                BingoGameModeType.Traditional,
                BingoBallCountType.Ball75,
                true,
                GameSettings.DefaultFirstBallCountdownSeconds,
                GameSettings.DefaultNextBallCountdownSeconds,
                GameSettings.DefaultRiskMatchDurationSeconds,
                false,
                BingoRuleType.Traditional);
            return;
        }

        phase = controller.phase;
        endReason = controller.endReason;
        gameModeType = controller.gameModeType;
        ballCountType = controller.ballCountType;
        useFreeCell = controller.useFreeCell;
        hasRule = controller.hasRule;
        ruleType = controller.ruleType;
        ballController = new GameBallController(controller.ballController);
        ballTimer = new GamePlayTimer(controller.ballTimer);
        riskTimer = new GamePlayTimer(controller.riskTimer);
        firstBallCountdownSeconds = controller.firstBallCountdownSeconds;
        nextBallCountdownSeconds = controller.nextBallCountdownSeconds;
        riskMatchDurationSeconds = controller.riskMatchDurationSeconds;
        ballCallRequestCount = controller.ballCallRequestCount;
        isFinalBallCountdown = controller.isFinalBallCountdown;
        matchEndingCheckPlayerId = controller.matchEndingCheckPlayerId ?? string.Empty;
        pendingCheckAnimationPlayerIds = controller.pendingCheckAnimationPlayerIds != null
            ? new List<string>(controller.pendingCheckAnimationPlayerIds)
            : new List<string>();
        isRuleCompletionAwaitingChecks =
            controller.isRuleCompletionAwaitingChecks;
        isBallPoolExhaustedAwaitingChecks =
            controller.isBallPoolExhaustedAwaitingChecks;
        isRiskTimerExpiredAwaitingChecks =
            controller.isRiskTimerExpiredAwaitingChecks;
        deathFinalChecksWereRequired = controller.deathFinalChecksWereRequired;

        ruleController = new GameRuleController();
        ruleController.Setup(gameModeType, hasRule, ruleType);
        bingoChecker = new BingoChecker();
    }

    public void Initialize(
        BingoGameModeType requestedGameModeType,
        BingoBallCountType requestedBallCountType,
        bool requestedUseFreeCell,
        float requestedFirstBallCountdownSeconds,
        float requestedNextBallCountdownSeconds,
        float requestedRiskMatchDurationSeconds,
        bool requestedHasRule,
        BingoRuleType requestedRuleType)
    {
        gameModeType = requestedGameModeType;
        ballCountType = requestedBallCountType;
        useFreeCell = requestedUseFreeCell;
        hasRule = requestedHasRule;
        ruleType = requestedRuleType;
        firstBallCountdownSeconds = Mathf.Max(0f, requestedFirstBallCountdownSeconds);
        nextBallCountdownSeconds = Mathf.Max(0f, requestedNextBallCountdownSeconds);
        riskMatchDurationSeconds = Mathf.Max(0f, requestedRiskMatchDurationSeconds);
        phase = GamePlayPhase.WaitingForFirstPlayer;
        endReason = GameEndReason.None;
        ballCallRequestCount = 0;
        isFinalBallCountdown = false;
        matchEndingCheckPlayerId = string.Empty;
        pendingCheckAnimationPlayerIds ??= new List<string>();
        pendingCheckAnimationPlayerIds.Clear();
        isRuleCompletionAwaitingChecks = false;
        isBallPoolExhaustedAwaitingChecks = false;
        isRiskTimerExpiredAwaitingChecks = false;
        deathFinalChecksWereRequired = false;

        ballController ??= new GameBallController();
        ballController.Setup(ballCountType);

        ruleController ??= new GameRuleController();
        ruleController.Setup(gameModeType, hasRule, ruleType);

        bingoChecker ??= new BingoChecker();
        bingoChecker.ClearAllCheckHistory();

        ballTimer ??= new GamePlayTimer();
        riskTimer ??= new GamePlayTimer();
        ballTimer.Stop();
        riskTimer.Stop();
    }

    public bool TryStartFirstBallCountdown()
    {
        if (SessionPauseManager.IsPaused ||
            phase != GamePlayPhase.WaitingForFirstPlayer)
        {
            return false;
        }

        phase = GamePlayPhase.FirstBallCountdown;
        ballTimer.Start(firstBallCountdownSeconds);

        if (IsRiskRule)
        {
            riskTimer.Start(riskMatchDurationSeconds);
        }

        return true;
    }

    public bool UpdateRiskTimer()
    {
        if (SessionPauseManager.IsPaused ||
            !IsRiskRule ||
            phase == GamePlayPhase.Ended ||
            riskTimer == null ||
            !riskTimer.HasExpired())
        {
            return false;
        }

        if (HasPendingCheckAnimations)
        {
            if (isRiskTimerExpiredAwaitingChecks)
            {
                return false;
            }

            isRiskTimerExpiredAwaitingChecks = true;
            return true;
        }

        EndGame(GameEndReason.TimerExpired);
        return true;
    }

    public bool UpdateBallCallLoop()
    {
        if (SessionPauseManager.IsPaused ||
            !IsRunning ||
            isBallPoolExhaustedAwaitingChecks ||
            ballTimer == null ||
            !ballTimer.HasExpired())
        {
            return false;
        }

        CallNextBall();

        if (phase == GamePlayPhase.Ended)
        {
            return true;
        }

        if (isBallPoolExhaustedAwaitingChecks)
        {
            return true;
        }

        phase = GamePlayPhase.NextBallCountdown;
        ballTimer.Start(nextBallCountdownSeconds);
        return true;
    }

    public bool CallNextBall()
    {
        if (SessionPauseManager.IsPaused || !IsRunning)
        {
            return false;
        }

        ballController ??= new GameBallController();

        if (!ballController.IsInitialized || ballController.BallCountType != ballCountType)
        {
            ballController.Setup(ballCountType);
        }

        if (!ballController.TryGetNextNumber(out int calledNumber))
        {
            if (HasPendingCheckAnimations)
            {
                isBallPoolExhaustedAwaitingChecks = true;
                return false;
            }

            EndGame(GameEndReason.BallPoolExhausted);
            return false;
        }

        ballCallRequestCount++;
        isFinalBallCountdown = !ballController.HasRemainingNumbers;
        return true;
    }

    public bool TryCheckBingo(
        string playerId,
        LobbyBoardData boardData,
        IReadOnlyCollection<int> pressedCellIndices,
        IReadOnlyCollection<BingoPatternType> configuredPatternTypes,
        out BingoCheckResult checkResult,
        out GameRuleCheckDecision ruleDecision)
    {
        checkResult = null;
        ruleDecision = null;

        if (!CanAcceptBingoChecks ||
            string.IsNullOrWhiteSpace(playerId) ||
            boardData == null ||
            HasPendingCheckAnimation(playerId))
        {
            return false;
        }

        bingoChecker ??= new BingoChecker();

        if (!bingoChecker.TryCheck(
                playerId,
                boardData,
                pressedCellIndices,
                ballController?.CalledNumbers,
                configuredPatternTypes,
                null))
        {
            return false;
        }

        checkResult = bingoChecker.CurrentCheckResult;

        if (!ruleController.TryResolveCheck(checkResult, out ruleDecision))
        {
            return false;
        }

        AddPendingCheckAnimation(playerId);

        if (ruleDecision.waitsForWinningCheckAnimation &&
            string.IsNullOrWhiteSpace(matchEndingCheckPlayerId))
        {
            matchEndingCheckPlayerId = playerId;
        }

        return true;
    }

    public bool TryCompleteBingoCheckAnimation(string playerId)
    {
        if (!RemovePendingCheckAnimation(playerId))
        {
            return false;
        }

        if (phase == GamePlayPhase.Ended)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(matchEndingCheckPlayerId) &&
            string.Equals(playerId, matchEndingCheckPlayerId, StringComparison.Ordinal))
        {
            isRuleCompletionAwaitingChecks = true;
            ballTimer?.Stop();
            riskTimer?.Stop();
        }

        if (isRuleCompletionAwaitingChecks && !HasPendingCheckAnimations)
        {
            EndGame(GameEndReason.RuleCompleted);
            return true;
        }

        if (isBallPoolExhaustedAwaitingChecks && !HasPendingCheckAnimations)
        {
            EndGame(GameEndReason.BallPoolExhausted);
            return true;
        }

        if (isRiskTimerExpiredAwaitingChecks && !HasPendingCheckAnimations)
        {
            EndGame(GameEndReason.TimerExpired);
        }

        return true;
    }

    private void AddPendingCheckAnimation(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        pendingCheckAnimationPlayerIds ??= new List<string>();

        if (!pendingCheckAnimationPlayerIds.Contains(playerId))
        {
            pendingCheckAnimationPlayerIds.Add(playerId);
        }
    }

    private bool RemovePendingCheckAnimation(string playerId)
    {
        return !string.IsNullOrWhiteSpace(playerId) &&
               pendingCheckAnimationPlayerIds != null &&
               pendingCheckAnimationPlayerIds.Remove(playerId);
    }

    public bool HasPendingCheckAnimation(string playerId)
    {
        return !string.IsNullOrWhiteSpace(playerId) &&
               pendingCheckAnimationPlayerIds != null &&
               pendingCheckAnimationPlayerIds.Contains(playerId);
    }

    public bool CancelPendingCheckAnimation(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId) ||
            pendingCheckAnimationPlayerIds == null ||
            !pendingCheckAnimationPlayerIds.Remove(playerId))
        {
            return false;
        }

        if (string.Equals(matchEndingCheckPlayerId, playerId, StringComparison.Ordinal))
        {
            matchEndingCheckPlayerId = string.Empty;
            isRuleCompletionAwaitingChecks = false;
        }

        return true;
    }

    public List<BingoPatternCheckResult> GetCompletedAvailablePatterns(
        string playerId,
        LobbyBoardData boardData,
        IReadOnlyCollection<BingoPatternType> configuredPatternTypes)
    {
        bingoChecker ??= new BingoChecker();
        return bingoChecker.GetCompletedAvailablePatterns(
            playerId,
            boardData,
            ballController?.CalledNumbers,
            configuredPatternTypes);
    }

    public List<BingoPatternCheckResult> GetCompletedAvailablePatterns(
        string playerId,
        LobbyBoardData boardData,
        IReadOnlyCollection<int> markedCellIndices,
        IReadOnlyCollection<int> calledNumbers,
        IReadOnlyCollection<BingoPatternType> configuredPatternTypes)
    {
        bingoChecker ??= new BingoChecker();
        return bingoChecker.GetCompletedAvailablePatterns(
            playerId,
            boardData,
            markedCellIndices,
            calledNumbers,
            configuredPatternTypes);
    }

    public bool TryCheckAutomaticBingo(
        string playerId,
        LobbyBoardData boardData,
        IReadOnlyCollection<int> markedCellIndices,
        IReadOnlyCollection<int> calledNumbers,
        IReadOnlyCollection<BingoPatternType> configuredPatternTypes,
        out BingoCheckResult checkResult,
        out GameRuleCheckDecision ruleDecision)
    {
        return TryCheckAutomaticBingo(
            playerId,
            boardData,
            markedCellIndices,
            calledNumbers,
            configuredPatternTypes,
            null,
            out checkResult,
            out ruleDecision);
    }

    public bool TryCheckAutomaticBingo(
        string playerId,
        LobbyBoardData boardData,
        IReadOnlyCollection<int> markedCellIndices,
        IReadOnlyCollection<int> calledNumbers,
        IReadOnlyCollection<BingoPatternType> configuredPatternTypes,
        IReadOnlyList<BingoPatternIdentity> latePatterns,
        out BingoCheckResult checkResult,
        out GameRuleCheckDecision ruleDecision)
    {
        checkResult = null;
        ruleDecision = null;

        if (!CanAcceptBingoChecks ||
            string.IsNullOrWhiteSpace(playerId) ||
            boardData == null)
        {
            return false;
        }

        bingoChecker ??= new BingoChecker();

        if (!bingoChecker.TryCheck(
                playerId,
                boardData,
                markedCellIndices,
                calledNumbers,
                configuredPatternTypes,
                latePatterns))
        {
            return false;
        }

        checkResult = bingoChecker.CurrentCheckResult;
        return ruleController.TryResolveCheck(checkResult, out ruleDecision);
    }

    public bool ShouldEndBeforeNextBall(int eligiblePlayerCount)
    {
        ruleController ??= new GameRuleController();
        ruleController.Setup(gameModeType, hasRule, ruleType);
        return ruleController.ShouldEndBeforeNextBall(eligiblePlayerCount);
    }

    public void MarkDeathFinalChecksRequired()
    {
        deathFinalChecksWereRequired = true;
    }

    public bool TryCheckRiskBingo(
        string playerId,
        LobbyBoardData boardData,
        IReadOnlyCollection<int> pressedCellIndices,
        IReadOnlyCollection<BingoPatternType> configuredPatternTypes,
        IReadOnlyList<BingoPatternIdentity> latePatterns,
        out BingoCheckResult checkResult,
        out GameRuleCheckDecision ruleDecision)
    {
        checkResult = null;
        ruleDecision = null;

        if (!CanAcceptBingoChecks ||
            !IsRiskRule ||
            string.IsNullOrWhiteSpace(playerId) ||
            boardData == null ||
            HasPendingCheckAnimation(playerId))
        {
            return false;
        }

        bingoChecker ??= new BingoChecker();

        if (!bingoChecker.TryCheck(
                playerId,
                boardData,
                pressedCellIndices,
                ballController?.CalledNumbers,
                configuredPatternTypes,
                latePatterns))
        {
            return false;
        }

        checkResult = bingoChecker.CurrentCheckResult;

        if (!ruleController.TryResolveCheck(checkResult, out ruleDecision))
        {
            return false;
        }

        AddPendingCheckAnimation(playerId);
        return true;
    }

    public List<BingoPatternType> GetAvailablePatternTypes(
        string playerId,
        IReadOnlyCollection<BingoPatternType> configuredPatternTypes)
    {
        bingoChecker ??= new BingoChecker();
        return bingoChecker.GetAvailablePatternTypes(playerId, configuredPatternTypes);
    }

    public List<BingoPatternType> GetCheckedPatternTypes(string playerId)
    {
        bingoChecker ??= new BingoChecker();
        return bingoChecker.GetCheckedPatternTypes(playerId);
    }

    public void ApplyNetworkState(GamePlayStateChangedData updateData)
    {
        if (updateData == null)
        {
            return;
        }

        phase = updateData.phase;
        endReason = updateData.endReason;
        ballCallRequestCount = updateData.ballCallRequestCount;
        isFinalBallCountdown = updateData.isFinalBallCountdown;
        isRuleCompletionAwaitingChecks = updateData.isRuleCompletionAwaitingChecks;
        isBallPoolExhaustedAwaitingChecks = updateData.isBallPoolExhaustedAwaitingChecks;
        isRiskTimerExpiredAwaitingChecks = updateData.isRiskTimerExpiredAwaitingChecks;
        deathFinalChecksWereRequired = updateData.deathFinalChecksWereRequired;

        ballController ??= new GameBallController();
        ballController.ApplyCalledNumbersSnapshot(updateData.calledNumbers);

        ballTimer ??= new GamePlayTimer();
        ballTimer.ApplyNetworkState(updateData.isBallTimerActive, updateData.ballTimerEndTime);

        riskTimer ??= new GamePlayTimer();
        riskTimer.ApplyNetworkState(updateData.isRiskTimerActive, updateData.riskTimerEndTime);
    }

    public void AddCheckedPatterns(
        string playerId,
        IReadOnlyCollection<BingoPatternCheckResult> patternResults)
    {
        bingoChecker ??= new BingoChecker();
        bingoChecker.AddCheckedPatterns(playerId, patternResults);
    }

    public GamePlayerStatus ResolveEligiblePlayerAtMatchEnd()
    {
        ruleController ??= new GameRuleController();
        ruleController.Setup(gameModeType, hasRule, ruleType);
        return ruleController.ResolveEligiblePlayerAtMatchEnd(endReason);
    }

    public bool EndGame(GameEndReason reason)
    {
        if (phase == GamePlayPhase.Ended)
        {
            return false;
        }

        phase = GamePlayPhase.Ended;
        endReason = reason;
        isFinalBallCountdown = false;
        isRuleCompletionAwaitingChecks = false;
        isBallPoolExhaustedAwaitingChecks = false;
        isRiskTimerExpiredAwaitingChecks = false;
        ballTimer?.Stop();
        riskTimer?.Stop();
        Debug.Log("[GamePlayController] Game is over.");
        return true;
    }
}
