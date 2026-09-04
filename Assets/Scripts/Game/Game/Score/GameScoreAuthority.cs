using System;
using UnityEngine;

public static class GameScoreAuthority
{
    public static int ApplyCheckResult(
        GameSessionData gameSessionData,
        GamePlayerData playerData,
        BingoCheckResult checkResult,
        GameRuleCheckDecision ruleDecision)
    {
        if (gameSessionData == null || playerData == null || ruleDecision == null)
        {
            return 0;
        }

        int currentCheckPoints = 0;

        if (ruleDecision.playerStatus != GamePlayerStatus.Lost &&
            checkResult != null &&
            checkResult.HasWinningPattern &&
            !checkResult.HasFailedPattern)
        {
            currentCheckPoints = GetCurrentCheckPatternPoints(checkResult);
            playerData.currentMatchScore = ClampMatchScore(
                (long)playerData.currentMatchScore + currentCheckPoints);
        }

        playerData.gameStatus = ruleDecision.playerStatus;
        FinalizePlayerIfNeeded(gameSessionData, playerData);
        return currentCheckPoints;
    }

    public static bool FinalizeEligiblePlayers(
        GameSessionData gameSessionData,
        GamePlayerStatus finalStatus)
    {
        if (gameSessionData?.players == null || finalStatus == GamePlayerStatus.Eligible)
        {
            return false;
        }

        bool changed = false;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null)
            {
                continue;
            }

            if (playerData.gameStatus == GamePlayerStatus.Eligible)
            {
                playerData.gameStatus = finalStatus;
                changed = true;
            }

            if (FinalizePlayerIfNeeded(gameSessionData, playerData))
            {
                changed = true;
            }
        }

        return changed;
    }

    public static bool FinalizePlayerIfNeeded(
        GameSessionData gameSessionData,
        GamePlayerData playerData)
    {
        if (gameSessionData == null ||
            playerData == null ||
            playerData.areStatisticsFinalized ||
            playerData.gameStatus == GamePlayerStatus.Eligible)
        {
            return false;
        }

        gameSessionData.EnsureScoreValuesCached();

        if (playerData.gameStatus == GamePlayerStatus.Lost)
        {
            playerData.currentMatchScore = 0;
            playerData.finalizedScoreDelta = -Mathf.Max(0, gameSessionData.cachedLossPoints);
        }
        else if (UsesDeathWinScore(gameSessionData))
        {
            playerData.currentMatchScore = ClampMatchScore(gameSessionData.cachedDeathWinPoints);
            playerData.finalizedScoreDelta = playerData.currentMatchScore;
        }
        else
        {
            playerData.currentMatchScore = ClampMatchScore(playerData.currentMatchScore);
            playerData.finalizedScoreDelta = playerData.currentMatchScore;
        }

        playerData.areStatisticsFinalized = true;
        playerData.isScorePersisted = false;
        return true;
    }

    public static bool PersistFinalizedLocalScores(GameSessionData gameSessionData)
    {
        if (gameSessionData?.players == null || UserManager.instance == null)
        {
            return false;
        }

        bool changed = false;
        ScorePlayMode scorePlayMode = ResolveScorePlayMode(gameSessionData.playMode);
        BingoGameModeType scoreGameMode = ResolveScoreGameMode(gameSessionData);

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null ||
                !playerData.areStatisticsFinalized ||
                playerData.isScorePersisted)
            {
                continue;
            }

            if (!ShouldPersistOnLocalAuthority(gameSessionData, playerData))
            {
                playerData.isScorePersisted = true;
                changed = true;
                continue;
            }

            if (UserManager.instance.ApplyGameScore(
                    playerData.userId,
                    scorePlayMode,
                    scoreGameMode,
                    playerData.finalizedScoreDelta))
            {
                playerData.isScorePersisted = true;
                changed = true;
            }
        }

        return changed;
    }

    public static ScorePlayMode ResolveScorePlayMode(MainMenuPlayMode playMode)
    {
        return playMode == MainMenuPlayMode.Solo
            ? ScorePlayMode.Solo
            : ScorePlayMode.Online;
    }

    public static BingoGameModeType ResolveScoreGameMode(GameSessionData gameSessionData)
    {
        if (gameSessionData == null || gameSessionData.gameModeType != BingoGameModeType.Custom)
        {
            return gameSessionData?.gameModeType ?? BingoGameModeType.Traditional;
        }

        switch (gameSessionData.ruleType)
        {
            case BingoRuleType.Blackout:
                return BingoGameModeType.Blackout;

            case BingoRuleType.Risk:
                return BingoGameModeType.Risk;

            case BingoRuleType.Elimination:
                return BingoGameModeType.Death;

            default:
                return BingoGameModeType.Traditional;
        }
    }

    private static int GetCurrentCheckPatternPoints(BingoCheckResult checkResult)
    {
        if (checkResult == null)
        {
            return 0;
        }

        return ClampMatchScore(checkResult.currentCheckPatternPoints);
    }

    private static bool UsesDeathWinScore(GameSessionData gameSessionData)
    {
        return ResolveScoreGameMode(gameSessionData) == BingoGameModeType.Death;
    }

    private static bool ShouldPersistOnLocalAuthority(
        GameSessionData gameSessionData,
        GamePlayerData playerData)
    {
        if (gameSessionData.playMode == MainMenuPlayMode.Solo)
        {
            return playerData.userTag == UserTag.Player || playerData.userTag == UserTag.Bot;
        }

        if (MultiplayerPlayModeTestContext.IsActive || playerData.userTag != UserTag.Player)
        {
            return false;
        }

        return UserManager.instance != null &&
               string.Equals(
                   UserManager.instance.UserId,
                   playerData.userId,
                   StringComparison.Ordinal);
    }

    private static int ClampMatchScore(long score)
    {
        int maximumScore = GameSettings.instance != null
            ? GameSettings.instance.MaximumScore
            : UserStats.DefaultMaximumScore;

        if (score <= 0)
        {
            return 0;
        }

        if (score >= maximumScore)
        {
            return maximumScore;
        }

        return (int)score;
    }
}
