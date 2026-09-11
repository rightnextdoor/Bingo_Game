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

        if (ruleDecision.playerStatus == GamePlayerStatus.Eligible)
        {
            playerData.gameStatus = GamePlayerStatus.Eligible;
        }
        else
        {
            TrySetFinalStatus(gameSessionData, playerData, ruleDecision.playerStatus);
        }

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
                changed |= TrySetFinalStatus(gameSessionData, playerData, finalStatus);
            }
        }

        return changed;
    }

    public static bool TrySetFinalStatus(
        GameSessionData gameSessionData,
        GamePlayerData playerData,
        GamePlayerStatus finalStatus)
    {
        if (gameSessionData == null ||
            playerData == null ||
            (finalStatus != GamePlayerStatus.Won && finalStatus != GamePlayerStatus.Lost))
        {
            return false;
        }

        if ((playerData.gameStatus == GamePlayerStatus.Won ||
             playerData.gameStatus == GamePlayerStatus.Lost) &&
            playerData.gameStatus != finalStatus)
        {
            return false;
        }

        bool changed = playerData.gameStatus != finalStatus;
        playerData.gameStatus = finalStatus;
        return FinalizePlayerIfNeeded(gameSessionData, playerData) || changed;
    }

    public static bool FinalizePlayerIfNeeded(
        GameSessionData gameSessionData,
        GamePlayerData playerData)
    {
        if (gameSessionData == null ||
            playerData == null ||
            playerData.areStatisticsFinalized ||
            (playerData.gameStatus != GamePlayerStatus.Won &&
             playerData.gameStatus != GamePlayerStatus.Lost))
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

        MarkStatisticsFinalized(playerData);
        return true;
    }

    public static bool FinalizeDepartingHumanLoss(
        GameSessionData gameSessionData,
        GamePlayerData playerData)
    {
        if (gameSessionData == null ||
            playerData == null ||
            playerData.userTag != UserTag.Player ||
            playerData.areStatisticsFinalized)
        {
            return false;
        }

        gameSessionData.EnsureScoreValuesCached();
        playerData.finalizedScoreDelta = -Mathf.Max(0, gameSessionData.cachedLossPoints);
        MarkStatisticsFinalized(playerData);
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

            if (PersistFinalizedScore(
                    gameSessionData,
                    playerData,
                    scorePlayMode,
                    scoreGameMode))
            {
                playerData.isScorePersisted = true;
                changed = true;
            }
        }

        return changed;
    }

    public static bool PersistFinalizedScoreForCurrentUser(
        GameSessionData gameSessionData)
    {
        if (gameSessionData == null ||
            UserManager.instance == null ||
            MultiplayerPlayModeTestContext.IsActive)
        {
            return false;
        }

        GamePlayerData playerData = gameSessionData.GetPlayer(UserManager.instance.UserId);

        if (playerData == null ||
            playerData.userTag != UserTag.Player ||
            !playerData.areStatisticsFinalized)
        {
            return false;
        }

        bool persisted = PersistFinalizedScore(
            gameSessionData,
            playerData,
            ResolveScorePlayMode(gameSessionData.playMode),
            ResolveScoreGameMode(gameSessionData));

        if (persisted)
        {
            playerData.isScorePersisted = true;
        }

        return persisted;
    }

    public static GameFinalScoreResultData CreateFinalScoreResult(
        GameSessionData gameSessionData,
        GamePlayerData playerData)
    {
        if (gameSessionData == null ||
            playerData == null ||
            !playerData.areStatisticsFinalized)
        {
            return null;
        }

        return new GameFinalScoreResultData
        {
            resultId = GetScoreResultId(gameSessionData, playerData),
            userId = playerData.userId ?? string.Empty,
            playMode = ResolveScorePlayMode(gameSessionData.playMode),
            gameModeType = ResolveScoreGameMode(gameSessionData),
            scoreDelta = playerData.finalizedScoreDelta
        };
    }

    public static bool PersistFinalScoreResult(GameFinalScoreResultData scoreResult)
    {
        return scoreResult != null &&
               UserManager.instance != null &&
               UserManager.instance.ApplyGameScoreOnce(
                   scoreResult.resultId,
                   scoreResult.userId,
                   scoreResult.playMode,
                   scoreResult.gameModeType,
                   scoreResult.scoreDelta);
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

    private static bool PersistFinalizedScore(
        GameSessionData gameSessionData,
        GamePlayerData playerData,
        ScorePlayMode scorePlayMode,
        BingoGameModeType scoreGameMode)
    {
        return UserManager.instance != null &&
               UserManager.instance.ApplyGameScoreOnce(
                   GetScoreResultId(gameSessionData, playerData),
                   playerData.userId,
                   scorePlayMode,
                   scoreGameMode,
                   playerData.finalizedScoreDelta);
    }

    private static string GetScoreResultId(
        GameSessionData gameSessionData,
        GamePlayerData playerData)
    {
        return $"{gameSessionData?.gameId ?? string.Empty}:{playerData?.userId ?? string.Empty}";
    }

    private static void MarkStatisticsFinalized(GamePlayerData playerData)
    {
        playerData.areStatisticsFinalized = true;
        playerData.isScorePersisted = false;
        playerData.isSubmitTimerActive = false;
        playerData.submitTimerEndTime = 0d;
        playerData.isRiskDecisionPending = false;
        playerData.queuedRiskPatterns?.Clear();
        playerData.activeRiskSubmitPatterns?.Clear();
        playerData.lateRiskPatterns?.Clear();
        playerData.pendingRiskCheckPatterns?.Clear();
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
