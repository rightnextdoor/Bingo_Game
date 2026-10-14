using System;

[Serializable]
public class GameRuleCheckDecision
{
    public GamePlayerStatus playerStatus = GamePlayerStatus.Eligible;
    public bool waitsForWinningCheckAnimation;
    public bool requiresRiskDecision;

    public bool IsFinalForPlayer => playerStatus != GamePlayerStatus.Eligible;
}

[Serializable]
public class GameRuleController
{
    private BingoRuleType activeRuleType = BingoRuleType.Traditional;
    private bool isSupported;

    public BingoRuleType ActiveRuleType => activeRuleType;
    public bool IsSupported => isSupported;
    public bool IsElimination => activeRuleType == BingoRuleType.Elimination;

    public void Setup(
        BingoGameModeType gameModeType,
        bool hasRule,
        BingoRuleType requestedRuleType)
    {
        activeRuleType = hasRule
            ? requestedRuleType
            : ResolveDefaultRule(gameModeType);

        isSupported = activeRuleType == BingoRuleType.Traditional ||
                      activeRuleType == BingoRuleType.Blackout ||
                      activeRuleType == BingoRuleType.Risk ||
                      activeRuleType == BingoRuleType.Elimination;
    }

    public bool TryResolveCheck(
        BingoCheckResult checkResult,
        out GameRuleCheckDecision decision)
    {
        decision = null;

        if (!isSupported || checkResult == null)
        {
            return false;
        }

        bool isValidWin =
            checkResult.HasCheckedPatterns &&
            checkResult.HasWinningPattern &&
            !checkResult.HasFailedPattern;

        bool isRisk = activeRuleType == BingoRuleType.Risk;
        bool isElimination = activeRuleType == BingoRuleType.Elimination;
        bool awardedRiskPoints =
            isRisk && checkResult.currentCheckPatternPoints > 0;

        decision = new GameRuleCheckDecision
        {
            playerStatus = isElimination
                ? GamePlayerStatus.Lost
                : isValidWin && isRisk
                ? GamePlayerStatus.Eligible
                : isValidWin
                    ? GamePlayerStatus.Won
                    : GamePlayerStatus.Lost,
            waitsForWinningCheckAnimation = isValidWin && !isRisk && !isElimination,
            requiresRiskDecision = isValidWin && awardedRiskPoints
        };

        return true;
    }

    public GamePlayerStatus ResolveEligiblePlayerAtMatchEnd(GameEndReason endReason)
    {
        switch (activeRuleType)
        {
            case BingoRuleType.Traditional:
            case BingoRuleType.Blackout:
            case BingoRuleType.Risk:
                return GamePlayerStatus.Lost;

            case BingoRuleType.Elimination:
                return GamePlayerStatus.Won;

            default:
                return GamePlayerStatus.Lost;
        }
    }

    public bool ShouldEndBeforeNextBall(int eligiblePlayerCount)
    {
        int safeEligiblePlayerCount = Math.Max(0, eligiblePlayerCount);
        return IsElimination
            ? safeEligiblePlayerCount <= 1
            : safeEligiblePlayerCount == 0;
    }

    private static BingoRuleType ResolveDefaultRule(BingoGameModeType gameModeType)
    {
        switch (gameModeType)
        {
            case BingoGameModeType.Blackout:
                return BingoRuleType.Blackout;

            case BingoGameModeType.Risk:
                return BingoRuleType.Risk;

            case BingoGameModeType.Death:
                return BingoRuleType.Elimination;

            default:
                return BingoRuleType.Traditional;
        }
    }
}
