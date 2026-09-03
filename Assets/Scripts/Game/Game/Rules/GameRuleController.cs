using System;

[Serializable]
public class GameRuleCheckDecision
{
    public GamePlayerStatus playerStatus = GamePlayerStatus.Eligible;
    public bool waitsForWinningCheckAnimation;

    public bool IsFinalForPlayer => playerStatus != GamePlayerStatus.Eligible;
}

[Serializable]
public class GameRuleController
{
    private BingoRuleType activeRuleType = BingoRuleType.Traditional;
    private bool isSupported;

    public BingoRuleType ActiveRuleType => activeRuleType;
    public bool IsSupported => isSupported;

    public void Setup(
        BingoGameModeType gameModeType,
        bool hasRule,
        BingoRuleType requestedRuleType)
    {
        activeRuleType = hasRule
            ? requestedRuleType
            : ResolveDefaultRule(gameModeType);

        isSupported = activeRuleType == BingoRuleType.Traditional ||
                      activeRuleType == BingoRuleType.Blackout;
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

        decision = new GameRuleCheckDecision
        {
            playerStatus = isValidWin
                ? GamePlayerStatus.Won
                : GamePlayerStatus.Lost,
            waitsForWinningCheckAnimation = isValidWin
        };

        return true;
    }

    public GamePlayerStatus ResolveEligiblePlayerAtMatchEnd(GameEndReason endReason)
    {
        switch (activeRuleType)
        {
            case BingoRuleType.Traditional:
            case BingoRuleType.Blackout:
                return GamePlayerStatus.Lost;

            default:
                return GamePlayerStatus.Lost;
        }
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
