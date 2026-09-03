using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GamePlayController
{
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

    [SerializeField] private int ballCallRequestCount;
    [SerializeField] private string matchEndingCheckPlayerId = string.Empty;
    [SerializeField] private List<string> pendingCheckAnimationPlayerIds = new List<string>();
    [SerializeField] private bool isBallPoolExhaustedAwaitingChecks;

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
    public bool HasPendingCheckAnimations => pendingCheckAnimationPlayerIds != null &&
                                             pendingCheckAnimationPlayerIds.Count > 0;
    public bool IsBallPoolExhaustedAwaitingChecks => isBallPoolExhaustedAwaitingChecks;
    public bool IsRunning =>
        phase == GamePlayPhase.FirstBallCountdown ||
        phase == GamePlayPhase.NextBallCountdown;
    public bool CanAcceptBingoChecks =>
        ruleController != null &&
        ruleController.IsSupported &&
        IsRunning &&
        !isBallPoolExhaustedAwaitingChecks;

    public GamePlayController()
    {
        Initialize(
            BingoGameModeType.Traditional,
            BingoBallCountType.Ball75,
            true,
            GameSettings.DefaultFirstBallCountdownSeconds,
            GameSettings.DefaultNextBallCountdownSeconds,
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
        ballCallRequestCount = controller.ballCallRequestCount;
        matchEndingCheckPlayerId = controller.matchEndingCheckPlayerId ?? string.Empty;
        pendingCheckAnimationPlayerIds = controller.pendingCheckAnimationPlayerIds != null
            ? new List<string>(controller.pendingCheckAnimationPlayerIds)
            : new List<string>();
        isBallPoolExhaustedAwaitingChecks =
            controller.isBallPoolExhaustedAwaitingChecks;

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
        phase = GamePlayPhase.WaitingForFirstPlayer;
        endReason = GameEndReason.None;
        ballCallRequestCount = 0;
        matchEndingCheckPlayerId = string.Empty;
        pendingCheckAnimationPlayerIds ??= new List<string>();
        pendingCheckAnimationPlayerIds.Clear();
        isBallPoolExhaustedAwaitingChecks = false;

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
        if (phase != GamePlayPhase.WaitingForFirstPlayer)
        {
            return false;
        }

        phase = GamePlayPhase.FirstBallCountdown;
        ballTimer.Start(firstBallCountdownSeconds);
        return true;
    }

    public bool UpdateBallCallLoop()
    {
        if (!IsRunning ||
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
        if (!IsRunning)
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
            boardData == null)
        {
            return false;
        }

        bingoChecker ??= new BingoChecker();

        if (!bingoChecker.TryCheck(
                playerId,
                boardData,
                pressedCellIndices,
                ballController?.CalledNumbers,
                configuredPatternTypes))
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
            string.Equals(
                playerId,
                matchEndingCheckPlayerId,
                StringComparison.Ordinal))
        {
            EndGame(GameEndReason.RuleCompleted);
            return true;
        }

        if (isBallPoolExhaustedAwaitingChecks && !HasPendingCheckAnimations)
        {
            EndGame(GameEndReason.BallPoolExhausted);
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
        isBallPoolExhaustedAwaitingChecks = false;
        ballTimer?.Stop();
        riskTimer?.Stop();
        Debug.Log("[GamePlayController] Game is over.");
        return true;
    }
}
