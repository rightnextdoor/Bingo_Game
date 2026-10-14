using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameRankAuthority
{
    public const int MaximumWinningRank = 3;

    public static bool IsEnabled(GameSessionData gameSessionData)
    {
        return gameSessionData?.useRank == true;
    }

    public static bool UsesDefaultRankRules(GameSessionData gameSessionData)
    {
        return IsEnabled(gameSessionData) &&
               gameSessionData.gamePlayController?.IsRiskRule != true &&
               gameSessionData.gamePlayController?.IsDeathRule != true;
    }

    public static void Initialize(GameSessionData gameSessionData)
    {
        if (gameSessionData?.players == null)
        {
            return;
        }

        gameSessionData.rankPlayerTotal = gameSessionData.players.Count;
        gameSessionData.rankResolutionSequence = 0;
        int initialRank = ResolveInitialRank(gameSessionData);

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null)
            {
                continue;
            }

            playerData.rank = initialRank;
            playerData.isRankFinal = false;
            playerData.isRankWinBlocked = false;
            playerData.hasRiskCashedOut = false;
            playerData.hasPendingRankCheck = false;
            playerData.pendingRankCheckScore = 0;
            playerData.rankResolutionOrder = 0;
        }

        RefreshLiveRanks(gameSessionData);
    }

    public static bool PrepareDefaultRankedCheck(
        GameSessionData gameSessionData,
        GamePlayerData playerData,
        int checkScore)
    {
        if (!UsesDefaultRankRules(gameSessionData) || playerData == null)
        {
            return false;
        }

        playerData.hasPendingRankCheck = true;
        playerData.pendingRankCheckScore = Mathf.Max(0, checkScore);
        playerData.rankResolutionOrder = ++gameSessionData.rankResolutionSequence;
        playerData.gameStatus = GamePlayerStatus.Checking;
        return true;
    }

    public static bool CompleteDefaultRankedCheck(
        GameSessionData gameSessionData,
        GamePlayerData playerData)
    {
        if (!UsesDefaultRankRules(gameSessionData) ||
            playerData?.hasPendingRankCheck != true)
        {
            return false;
        }

        int pendingScore = playerData.pendingRankCheckScore;
        playerData.hasPendingRankCheck = false;
        playerData.pendingRankCheckScore = 0;

        int completedWinnerCount = CountFinalWinners(gameSessionData);

        if (completedWinnerCount < MaximumWinningRank)
        {
            playerData.rank = completedWinnerCount + 1;
            playerData.isRankFinal = true;
            playerData.currentMatchScore = ClampMatchScore(
                (long)playerData.currentMatchScore + pendingScore);
            GameScoreAuthority.TrySetFinalStatus(
                gameSessionData,
                playerData,
                GamePlayerStatus.Won);
        }
        else
        {
            AssignLossRank(gameSessionData, playerData);
            GameScoreAuthority.TrySetFinalStatus(
                gameSessionData,
                playerData,
                GamePlayerStatus.Lost);
        }

        RefreshLiveRanks(gameSessionData);

        if (CountFinalWinners(gameSessionData) >= MaximumWinningRank)
        {
            GameScoreAuthority.FinalizeEligiblePlayers(
                gameSessionData,
                GamePlayerStatus.Lost);
            gameSessionData.gamePlayController?.EndGame(GameEndReason.RuleCompleted);
        }

        return true;
    }

    public static bool ResolveDeathCohort(
        GameSessionData gameSessionData,
        List<GamePlayerData> cohort)
    {
        if (!IsEnabled(gameSessionData) ||
            gameSessionData?.gamePlayController?.IsDeathRule != true ||
            cohort == null ||
            cohort.Count == 0)
        {
            return false;
        }

        cohort.Sort(CompareResolutionOrder);
        int remainingPlayers = gameSessionData.GetEligiblePlayerCount();
        int bestCohortRank = remainingPlayers + 1;
        bool winningCohort = bestCohortRank <= MaximumWinningRank;
        bool changed = false;

        for (int i = 0; i < cohort.Count; i++)
        {
            GamePlayerData playerData = cohort[i];

            if (playerData == null || playerData.gameStatus != GamePlayerStatus.Checking)
            {
                continue;
            }

            int resolvedRank = winningCohort
                ? bestCohortRank
                : remainingPlayers + cohort.Count - i;

            playerData.rank = Mathf.Clamp(
                resolvedRank,
                1,
                Math.Max(1, gameSessionData.rankPlayerTotal));
            playerData.isRankFinal = true;

            bool canWin = winningCohort &&
                          playerData.deathCheckSucceeded &&
                          playerData.deathCheckCanWin &&
                          !playerData.isRankWinBlocked;

            changed |= GameScoreAuthority.TrySetFinalStatus(
                gameSessionData,
                playerData,
                canWin ? GamePlayerStatus.Won : GamePlayerStatus.Lost);
        }

        RefreshLiveRanks(gameSessionData);
        return changed;
    }

    public static bool FinalizeRankedDeathMatch(GameSessionData gameSessionData)
    {
        if (!IsEnabled(gameSessionData) ||
            gameSessionData?.gamePlayController?.IsDeathRule != true ||
            gameSessionData.players == null)
        {
            return false;
        }

        bool changed = false;
        List<GamePlayerData> survivors = new List<GamePlayerData>();

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null ||
                playerData.gameStatus == GamePlayerStatus.Won ||
                playerData.gameStatus == GamePlayerStatus.Lost)
            {
                continue;
            }

            if (!playerData.isRankWinBlocked &&
                playerData.returnState != GamePlayerReturnState.FrozenAwaitingReturn &&
                (playerData.gameStatus == GamePlayerStatus.Eligible ||
                 (playerData.gameStatus == GamePlayerStatus.Checking &&
                  playerData.deathCheckSucceeded &&
                  playerData.deathCheckCanWin)))
            {
                survivors.Add(playerData);
            }
            else
            {
                AssignLossRank(gameSessionData, playerData);
                changed |= GameScoreAuthority.TrySetFinalStatus(
                    gameSessionData,
                    playerData,
                    GamePlayerStatus.Lost);
            }
        }

        for (int i = 0; i < survivors.Count; i++)
        {
            GamePlayerData playerData = survivors[i];
            playerData.rank = 1;
            playerData.isRankFinal = true;
            changed |= GameScoreAuthority.TrySetFinalStatus(
                gameSessionData,
                playerData,
                GamePlayerStatus.Won);
        }

        RefreshLiveRanks(gameSessionData);
        return changed;
    }

    public static bool FinalizeRiskMatch(GameSessionData gameSessionData)
    {
        if (gameSessionData?.gamePlayController?.IsRiskRule != true ||
            gameSessionData.players == null)
        {
            return false;
        }

        return IsEnabled(gameSessionData)
            ? FinalizeRankedRiskMatch(gameSessionData)
            : FinalizeUnrankedRiskMatch(gameSessionData);
    }

    public static bool RefreshLiveRanks(GameSessionData gameSessionData)
    {
        if (!IsEnabled(gameSessionData) || gameSessionData.players == null)
        {
            return false;
        }

        if (gameSessionData.rankPlayerTotal <= 0)
        {
            gameSessionData.rankPlayerTotal = gameSessionData.players.Count;
        }

        if (gameSessionData.gamePlayController?.IsRiskRule == true)
        {
            return RefreshRiskRanks(gameSessionData);
        }

        bool changed = false;
        int activeRank = gameSessionData.gamePlayController?.IsDeathRule == true
            ? CountDeathLiveContenders(gameSessionData)
            : Math.Min(MaximumWinningRank, CountFinalWinners(gameSessionData) + 1);

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null || playerData.isRankFinal)
            {
                continue;
            }

            if (playerData.rank != activeRank)
            {
                playerData.rank = activeRank;
                changed = true;
            }
        }

        return changed;
    }

    public static void PrepareFinalRank(
        GameSessionData gameSessionData,
        GamePlayerData playerData,
        GamePlayerStatus finalStatus)
    {
        if (!IsEnabled(gameSessionData) || playerData == null || playerData.isRankFinal)
        {
            return;
        }

        if (finalStatus == GamePlayerStatus.Lost)
        {
            AssignLossRank(gameSessionData, playerData);
            return;
        }

        if (finalStatus == GamePlayerStatus.Won)
        {
            playerData.rank = Math.Max(1, CountFinalWinners(gameSessionData) + 1);
            playerData.isRankFinal = true;
        }
    }

    public static int ApplyPlacementPercentage(int baseScore, int rank)
    {
        float percentage = rank switch
        {
            1 => 1f,
            2 => 0.75f,
            3 => 0.5f,
            _ => 0f
        };

        int adjustedScore = Mathf.RoundToInt(Mathf.Max(0, baseScore) * percentage);
        return Mathf.Max(0, ((adjustedScore + 5) / 10) * 10);
    }

    private static bool FinalizeRankedRiskMatch(GameSessionData gameSessionData)
    {
        bool changed = false;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null ||
                playerData.gameStatus == GamePlayerStatus.Won ||
                playerData.gameStatus == GamePlayerStatus.Lost)
            {
                continue;
            }

            if (!playerData.hasRiskCashedOut)
            {
                AssignLossRank(gameSessionData, playerData);
                changed |= GameScoreAuthority.TrySetFinalStatus(
                    gameSessionData,
                    playerData,
                    GamePlayerStatus.Lost);
            }
        }

        RefreshRiskRanks(gameSessionData);

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null ||
                playerData.gameStatus == GamePlayerStatus.Won ||
                playerData.gameStatus == GamePlayerStatus.Lost ||
                !playerData.hasRiskCashedOut)
            {
                continue;
            }

            playerData.isRankFinal = true;
            changed |= GameScoreAuthority.TrySetFinalStatus(
                gameSessionData,
                playerData,
                playerData.rank <= MaximumWinningRank
                    ? GamePlayerStatus.Won
                    : GamePlayerStatus.Lost);
        }

        return changed;
    }

    private static bool FinalizeUnrankedRiskMatch(GameSessionData gameSessionData)
    {
        int highestScore = -1;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData != null &&
                playerData.gameStatus != GamePlayerStatus.Lost &&
                playerData.hasRiskCashedOut)
            {
                highestScore = Math.Max(highestScore, playerData.currentMatchScore);
            }
        }

        bool changed = false;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null ||
                playerData.gameStatus == GamePlayerStatus.Won ||
                playerData.gameStatus == GamePlayerStatus.Lost)
            {
                continue;
            }

            bool won = playerData.hasRiskCashedOut &&
                       highestScore >= 0 &&
                       playerData.currentMatchScore == highestScore;
            changed |= GameScoreAuthority.TrySetFinalStatus(
                gameSessionData,
                playerData,
                won ? GamePlayerStatus.Won : GamePlayerStatus.Lost);
        }

        return changed;
    }

    private static bool RefreshRiskRanks(GameSessionData gameSessionData)
    {
        List<GamePlayerData> contenders = new List<GamePlayerData>();

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData != null &&
                playerData.gameStatus != GamePlayerStatus.Lost &&
                !playerData.isRankFinal)
            {
                contenders.Add(playerData);
            }
        }

        contenders.Sort(CompareRiskStanding);
        bool changed = false;
        int previousScore = int.MinValue;
        int competitionRank = 0;
        int lastPlaceRank = Math.Max(1, gameSessionData.rankPlayerTotal);

        for (int i = 0; i < contenders.Count; i++)
        {
            GamePlayerData playerData = contenders[i];

            int resolvedRank;

            if (playerData.currentMatchScore <= 0)
            {
                resolvedRank = lastPlaceRank;
            }
            else
            {
                if (i == 0 || playerData.currentMatchScore != previousScore)
                {
                    competitionRank = i + 1;
                    previousScore = playerData.currentMatchScore;
                }

                resolvedRank = competitionRank;
            }

            if (playerData.rank != resolvedRank)
            {
                playerData.rank = resolvedRank;
                changed = true;
            }
        }

        return changed;
    }

    private static int ResolveInitialRank(GameSessionData gameSessionData)
    {
        if (!IsEnabled(gameSessionData))
        {
            return 0;
        }

        bool startsInLastPlace =
            gameSessionData.gamePlayController?.IsRiskRule == true ||
            gameSessionData.gamePlayController?.IsDeathRule == true;

        return startsInLastPlace
            ? Math.Max(1, gameSessionData.rankPlayerTotal)
            : 1;
    }

    private static int CountDeathLiveContenders(GameSessionData gameSessionData)
    {
        int contenderCount = 0;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData != null &&
                !playerData.isRankFinal &&
                playerData.gameStatus != GamePlayerStatus.Won &&
                playerData.gameStatus != GamePlayerStatus.Lost)
            {
                contenderCount++;
            }
        }

        return Mathf.Clamp(
            contenderCount,
            1,
            Math.Max(1, gameSessionData.rankPlayerTotal));
    }

    private static void AssignLossRank(
        GameSessionData gameSessionData,
        GamePlayerData playerData)
    {
        if (gameSessionData?.players == null || playerData == null)
        {
            return;
        }

        int nonFinalCount = 0;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData otherPlayer = gameSessionData.players[i];

            if (otherPlayer != null &&
                otherPlayer != playerData &&
                !otherPlayer.isRankFinal &&
                otherPlayer.gameStatus != GamePlayerStatus.Won &&
                otherPlayer.gameStatus != GamePlayerStatus.Lost)
            {
                nonFinalCount++;
            }
        }

        playerData.rank = Mathf.Clamp(
            nonFinalCount + 1,
            1,
            Math.Max(1, gameSessionData.rankPlayerTotal));
        playerData.isRankFinal = true;
    }

    private static int CountFinalWinners(GameSessionData gameSessionData)
    {
        if (gameSessionData?.players == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            if (gameSessionData.players[i]?.gameStatus == GamePlayerStatus.Won)
            {
                count++;
            }
        }

        return count;
    }

    private static int CompareResolutionOrder(GamePlayerData left, GamePlayerData right)
    {
        long leftOrder = left?.rankResolutionOrder ?? long.MaxValue;
        long rightOrder = right?.rankResolutionOrder ?? long.MaxValue;
        int orderComparison = leftOrder.CompareTo(rightOrder);

        if (orderComparison != 0)
        {
            return orderComparison;
        }

        return string.Compare(
            left?.userId,
            right?.userId,
            StringComparison.Ordinal);
    }

    private static int CompareRiskStanding(GamePlayerData left, GamePlayerData right)
    {
        int scoreComparison = (right?.currentMatchScore ?? 0)
            .CompareTo(left?.currentMatchScore ?? 0);

        if (scoreComparison != 0)
        {
            return scoreComparison;
        }

        return string.Compare(
            left?.userId,
            right?.userId,
            StringComparison.Ordinal);
    }

    private static int ClampMatchScore(long score)
    {
        int maximumScore = GameSettings.instance != null
            ? GameSettings.instance.MaximumScore
            : UserStats.DefaultMaximumScore;

        return (int)Math.Max(0L, Math.Min(score, maximumScore));
    }
}
