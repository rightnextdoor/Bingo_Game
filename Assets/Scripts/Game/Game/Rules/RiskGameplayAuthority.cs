using System.Collections.Generic;

public static class RiskGameplayAuthority
{
    public static bool IsRiskGame(GameSessionData gameSessionData)
    {
        return gameSessionData?.gamePlayController?.IsRiskRule == true;
    }

    public static bool UpdatePatternWindows(GameSessionData gameSessionData)
    {
        if (!IsRiskGame(gameSessionData) ||
            gameSessionData.gameState != GameSessionState.InProgress ||
            gameSessionData.gamePlayController.Phase == GamePlayPhase.Ended)
        {
            return false;
        }

        bool changed = false;
        GamePlayController playController = gameSessionData.gamePlayController;
        int ballCallCount = playController.BallCallRequestCount;

        if (ballCallCount > gameSessionData.lastRiskPatternScanBallCallCount)
        {
            changed |= ScanForNewPatterns(gameSessionData);
            gameSessionData.lastRiskPatternScanBallCallCount = ballCallCount;
        }

        if (ballCallCount > 0 &&
            playController.Phase == GamePlayPhase.NextBallCountdown &&
            playController.BallTimer?.IsActive == true &&
            playController.BallTimer.GetRemainingSeconds() <=
                GamePlayController.RiskSubmitCutoffSeconds &&
            gameSessionData.lastRiskSubmitCutoffBallCallCount < ballCallCount)
        {
            changed |= ProcessSubmitCutoff(gameSessionData);
            gameSessionData.lastRiskSubmitCutoffBallCallCount = ballCallCount;
        }

        return changed;
    }

    public static void PrepareCheckResult(
        GamePlayerData playerData,
        BingoCheckResult checkResult,
        GameRuleCheckDecision ruleDecision)
    {
        if (playerData == null || checkResult == null || ruleDecision == null)
        {
            return;
        }

        EnsureCollections(playerData);
        playerData.pendingRiskCheckPatterns.Clear();

        if (checkResult.patterns != null)
        {
            for (int i = 0; i < checkResult.patterns.Count; i++)
            {
                BingoPatternCheckResult patternResult = checkResult.patterns[i];

                if (patternResult?.isWinningPattern != true)
                {
                    continue;
                }

                BingoPatternIdentityList.AddUnique(
                    playerData.pendingRiskCheckPatterns,
                    BingoPatternIdentity.FromResult(patternResult));
            }
        }

        playerData.isRiskDecisionPending = ruleDecision.requiresRiskDecision;
    }

    public static bool CompletePendingCheck(
        GameSessionData gameSessionData,
        GamePlayerData playerData)
    {
        if (!IsRiskGame(gameSessionData) || playerData == null)
        {
            return false;
        }

        EnsureCollections(playerData);
        bool changed = false;

        for (int i = 0; i < playerData.pendingRiskCheckPatterns.Count; i++)
        {
            BingoPatternIdentity identity = playerData.pendingRiskCheckPatterns[i];
            changed |= BingoPatternIdentityList.Remove(playerData.queuedRiskPatterns, identity);
            changed |= BingoPatternIdentityList.Remove(playerData.activeRiskSubmitPatterns, identity);
            changed |= BingoPatternIdentityList.Remove(playerData.lateRiskPatterns, identity);
        }

        if (playerData.pendingRiskCheckPatterns.Count > 0)
        {
            playerData.pendingRiskCheckPatterns.Clear();
            changed = true;
        }

        if (playerData.gameStatus != GamePlayerStatus.Eligible)
        {
            return ClearPlayerRiskState(playerData, true) || changed;
        }

        if (playerData.isSubmitTimerActive &&
            playerData.submitTimerEndTime <= GamePlayTimer.GetCurrentTime())
        {
            changed |= ExpireUnsubmittedActivePatterns(playerData);
        }

        if (playerData.activeRiskSubmitPatterns.Count == 0)
        {
            changed |= StopSubmitTimer(playerData);

            GamePlayController playController = gameSessionData.gamePlayController;
            int ballCallCount = playController.BallCallRequestCount;

            if (playerData.queuedRiskPatterns.Count > 0 &&
                ballCallCount > 0 &&
                gameSessionData.lastRiskSubmitCutoffBallCallCount >= ballCallCount &&
                playController.Phase == GamePlayPhase.NextBallCountdown &&
                playController.BallTimer?.IsActive == true)
            {
                changed |= StartNextSubmitWindow(gameSessionData, playerData);
            }
        }

        return changed;
    }

    public static bool ResolveDecision(
        GameSessionData gameSessionData,
        string userId,
        bool endPlayerGame)
    {
        if (!IsRiskGame(gameSessionData) ||
            gameSessionData.gameState != GameSessionState.InProgress ||
            string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        GamePlayerData playerData = gameSessionData.GetPlayer(userId);

        if (playerData == null ||
            playerData.gameStatus != GamePlayerStatus.Eligible ||
            !playerData.isRiskDecisionPending)
        {
            return false;
        }

        playerData.isRiskDecisionPending = false;

        if (!endPlayerGame)
        {
            return true;
        }

        playerData.gameStatus = GamePlayerStatus.Won;
        ClearPlayerRiskState(playerData, true);
        GameScoreAuthority.FinalizePlayerIfNeeded(gameSessionData, playerData);

        if (gameSessionData.GetEligiblePlayerCount() == 0 &&
            !gameSessionData.gamePlayController.HasPendingCheckAnimations)
        {
            gameSessionData.gamePlayController.EndGame(GameEndReason.NoEligiblePlayers);
            gameSessionData.gameState = GameSessionState.Completed;
        }

        return true;
    }

    public static bool ClearAllPlayerTiming(GameSessionData gameSessionData)
    {
        if (gameSessionData?.players == null)
        {
            return false;
        }

        bool changed = false;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            if (gameSessionData.players[i] != null)
            {
                changed |= ClearPlayerRiskState(gameSessionData.players[i], false);
            }
        }

        return changed;
    }

    private static bool ScanForNewPatterns(GameSessionData gameSessionData)
    {
        if (gameSessionData.players == null)
        {
            return false;
        }

        bool changed = false;
        GamePlayController playController = gameSessionData.gamePlayController;

        for (int playerIndex = 0; playerIndex < gameSessionData.players.Count; playerIndex++)
        {
            GamePlayerData playerData = gameSessionData.players[playerIndex];

            if (playerData == null ||
                playerData.gameStatus != GamePlayerStatus.Eligible ||
                playerData.boardData == null)
            {
                continue;
            }

            EnsureCollections(playerData);

            List<BingoPatternCheckResult> completedPatterns =
                playController.GetCompletedAvailablePatterns(
                    playerData.userId,
                    playerData.boardData,
                    gameSessionData.patternTypes);

            for (int patternIndex = 0; patternIndex < completedPatterns.Count; patternIndex++)
            {
                BingoPatternIdentity identity =
                    BingoPatternIdentity.FromResult(completedPatterns[patternIndex]);

                if (IsTracked(playerData, identity))
                {
                    continue;
                }

                changed |= BingoPatternIdentityList.AddUnique(
                    playerData.queuedRiskPatterns,
                    identity);
            }
        }

        return changed;
    }

    private static bool ProcessSubmitCutoff(GameSessionData gameSessionData)
    {
        if (gameSessionData.players == null)
        {
            return false;
        }

        bool changed = false;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null || playerData.gameStatus != GamePlayerStatus.Eligible)
            {
                continue;
            }

            EnsureCollections(playerData);

            if (playerData.isSubmitTimerActive)
            {
                changed |= ExpireUnsubmittedActivePatterns(playerData);
            }

            if (!playerData.isSubmitTimerActive &&
                playerData.activeRiskSubmitPatterns.Count == 0 &&
                playerData.queuedRiskPatterns.Count > 0)
            {
                changed |= StartNextSubmitWindow(gameSessionData, playerData);
            }
        }

        return changed;
    }

    private static bool ExpireUnsubmittedActivePatterns(GamePlayerData playerData)
    {
        bool changed = false;

        for (int i = playerData.activeRiskSubmitPatterns.Count - 1; i >= 0; i--)
        {
            BingoPatternIdentity identity = playerData.activeRiskSubmitPatterns[i];

            if (BingoPatternIdentityList.Contains(
                    playerData.pendingRiskCheckPatterns,
                    identity))
            {
                continue;
            }

            BingoPatternIdentityList.AddUnique(playerData.lateRiskPatterns, identity);
            playerData.activeRiskSubmitPatterns.RemoveAt(i);
            changed = true;
        }

        if (playerData.activeRiskSubmitPatterns.Count == 0)
        {
            changed |= StopSubmitTimer(playerData);
        }

        return changed;
    }

    private static bool StartNextSubmitWindow(
        GameSessionData gameSessionData,
        GamePlayerData playerData)
    {
        GamePlayController playController = gameSessionData.gamePlayController;

        for (int i = 0; i < playerData.queuedRiskPatterns.Count; i++)
        {
            BingoPatternIdentityList.AddUnique(
                playerData.activeRiskSubmitPatterns,
                playerData.queuedRiskPatterns[i]);
        }

        playerData.queuedRiskPatterns.Clear();
        playerData.isSubmitTimerActive = true;

        double nextCutoffTime =
            (playController.BallTimer?.EndTime ?? GamePlayTimer.GetCurrentTime()) +
            playController.NextBallCountdownSeconds -
            GamePlayController.RiskSubmitCutoffSeconds;

        if (nextCutoffTime <= GamePlayTimer.GetCurrentTime())
        {
            nextCutoffTime = GamePlayTimer.GetCurrentTime() +
                             System.Math.Max(0.1d, playController.NextBallCountdownSeconds);
        }

        playerData.submitTimerEndTime = nextCutoffTime;
        return true;
    }

    private static bool StopSubmitTimer(GamePlayerData playerData)
    {
        if (!playerData.isSubmitTimerActive && playerData.submitTimerEndTime <= 0d)
        {
            return false;
        }

        playerData.isSubmitTimerActive = false;
        playerData.submitTimerEndTime = 0d;
        return true;
    }

    private static bool ClearPlayerRiskState(
        GamePlayerData playerData,
        bool clearLatePatterns)
    {
        if (playerData == null)
        {
            return false;
        }

        EnsureCollections(playerData);
        bool changed = playerData.isSubmitTimerActive ||
                       playerData.submitTimerEndTime > 0d ||
                       playerData.isRiskDecisionPending ||
                       playerData.queuedRiskPatterns.Count > 0 ||
                       playerData.activeRiskSubmitPatterns.Count > 0 ||
                       playerData.pendingRiskCheckPatterns.Count > 0 ||
                       (clearLatePatterns && playerData.lateRiskPatterns.Count > 0);

        playerData.isSubmitTimerActive = false;
        playerData.submitTimerEndTime = 0d;
        playerData.isRiskDecisionPending = false;
        playerData.queuedRiskPatterns.Clear();
        playerData.activeRiskSubmitPatterns.Clear();
        playerData.pendingRiskCheckPatterns.Clear();

        if (clearLatePatterns)
        {
            playerData.lateRiskPatterns.Clear();
        }

        return changed;
    }

    private static bool IsTracked(
        GamePlayerData playerData,
        BingoPatternIdentity identity)
    {
        return BingoPatternIdentityList.Contains(playerData.queuedRiskPatterns, identity) ||
               BingoPatternIdentityList.Contains(playerData.activeRiskSubmitPatterns, identity) ||
               BingoPatternIdentityList.Contains(playerData.lateRiskPatterns, identity) ||
               BingoPatternIdentityList.Contains(playerData.pendingRiskCheckPatterns, identity);
    }

    private static void EnsureCollections(GamePlayerData playerData)
    {
        playerData.queuedRiskPatterns ??= new List<BingoPatternIdentity>();
        playerData.activeRiskSubmitPatterns ??= new List<BingoPatternIdentity>();
        playerData.lateRiskPatterns ??= new List<BingoPatternIdentity>();
        playerData.pendingRiskCheckPatterns ??= new List<BingoPatternIdentity>();
    }
}
