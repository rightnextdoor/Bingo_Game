using System;
using System.Collections.Generic;

public static class GameBingoCheckAuthority
{
    public static bool UpdateSessionLoop(GameSessionData gameSessionData)
    {
        if (gameSessionData == null ||
            gameSessionData.gameState != GameSessionState.InProgress ||
            gameSessionData.gamePlayController == null ||
            SessionPauseManager.IsPaused)
        {
            return false;
        }

        GamePlayController playController = gameSessionData.gamePlayController;
        bool changed = playController.UpdateRiskTimer();

        if (playController.Phase != GamePlayPhase.Ended)
        {
            changed |= DeathGameplayAuthority.UpdateAutomaticBoards(gameSessionData);
            changed |= GameBotManager.UpdateBots(gameSessionData);
            changed |= DeathGameplayAuthority.ApplyFrozenPlayerThreshold(gameSessionData);
            changed |= RiskGameplayAuthority.UpdatePatternWindows(gameSessionData);

            bool deathHandledBallBoundary =
                DeathGameplayAuthority.TryHandleBallBoundary(gameSessionData, out bool deathBoundaryChanged);
            changed |= deathBoundaryChanged;

            if (!deathHandledBallBoundary &&
                playController.BallTimer?.HasExpired() == true &&
                playController.ShouldEndBeforeNextBall(gameSessionData.GetEligiblePlayerCount()))
            {
                if (!playController.HasPendingCheckAnimations)
                {
                    changed |= playController.EndGame(GameEndReason.NoEligiblePlayers);
                }
            }
            else if (!deathHandledBallBoundary)
            {
                changed |= playController.UpdateBallCallLoop();
            }

            changed |= DeathGameplayAuthority.UpdateAutomaticBoards(gameSessionData);
            changed |= GameBotManager.UpdateBots(gameSessionData);
            changed |= RiskGameplayAuthority.UpdatePatternWindows(gameSessionData);
        }

        if (playController.Phase == GamePlayPhase.Ended &&
            gameSessionData.gameState != GameSessionState.Completed)
        {
            RiskGameplayAuthority.ClearAllPlayerTiming(gameSessionData);
            if (DeathGameplayAuthority.IsDeathGame(gameSessionData))
            {
                DeathGameplayAuthority.FinalizeMatch(gameSessionData);
            }
            else
            {
                GameScoreAuthority.FinalizeEligiblePlayers(
                    gameSessionData,
                    playController.ResolveEligiblePlayerAtMatchEnd());
            }

            gameSessionData.gameState = GameSessionState.Completed;
            changed = true;
        }

        return changed;
    }

    public static GameBingoCheckResolvedData ProcessCheck(
        GameSessionData gameSessionData,
        string userId,
        GameBingoCheckRequestData requestData)
    {
        long revision = gameSessionData?.revision ?? 0;
        string gameId = gameSessionData?.gameId ?? string.Empty;

        if (gameSessionData == null ||
            string.IsNullOrWhiteSpace(userId) ||
            requestData?.boardData == null)
        {
            return GameBingoCheckResolvedData.Rejected(
                gameId,
                userId,
                revision,
                "The Bingo check request was incomplete.");
        }

        if (SessionPauseManager.IsPaused)
        {
            return GameBingoCheckResolvedData.Rejected(
                gameId,
                userId,
                revision,
                "The game is paused.");
        }

        GamePlayerData playerData = gameSessionData.GetPlayer(userId);

        if (playerData == null ||
            playerData.userTag == UserTag.Bot ||
            playerData.controlType != GamePlayerControlType.Human ||
            playerData.returnState != GamePlayerReturnState.Active)
        {
            return GameBingoCheckResolvedData.Rejected(
                gameId,
                userId,
                revision,
                "The player could not be resolved for this Bingo check.");
        }

        if (gameSessionData.gameState != GameSessionState.InProgress ||
            gameSessionData.gamePlayController == null ||
            !gameSessionData.gamePlayController.CanAcceptBingoChecks)
        {
            return GameBingoCheckResolvedData.Rejected(
                gameId,
                userId,
                revision,
                "This match is not accepting Bingo checks.");
        }

        if (!playerData.isConnected ||
            !playerData.canRejoin ||
            playerData.gameStatus != GamePlayerStatus.Eligible ||
            playerData.isRiskDecisionPending ||
            gameSessionData.gamePlayController.HasPendingCheckAnimation(userId))
        {
            return GameBingoCheckResolvedData.Rejected(
                gameId,
                userId,
                revision,
                "This player is not eligible to submit another Bingo check.");
        }

        if (!BoardsMatch(playerData.boardData, requestData.boardData))
        {
            return GameBingoCheckResolvedData.Rejected(
                gameId,
                userId,
                revision,
                "The submitted board does not match the authoritative player board.");
        }

        GamePlayController playController = gameSessionData.gamePlayController;

        if (playController.IsDeathRule)
        {
            return GameBingoCheckResolvedData.Rejected(
                gameId,
                userId,
                revision,
                "Death boards submit Bingo checks automatically.");
        }

        bool checkStarted = playController.IsRiskRule
            ? playController.TryCheckRiskBingo(
                userId,
                playerData.boardData,
                requestData.markedCellIndices,
                gameSessionData.patternTypes,
                playerData.lateRiskPatterns,
                out BingoCheckResult checkResult,
                out GameRuleCheckDecision ruleDecision)
            : playController.TryCheckBingo(
                userId,
                playerData.boardData,
                requestData.markedCellIndices,
                gameSessionData.patternTypes,
                out checkResult,
                out ruleDecision);

        if (!checkStarted)
        {
            return GameBingoCheckResolvedData.Rejected(
                gameId,
                userId,
                revision,
                "The Bingo checker or active rule could not process this request.");
        }

        if (playController.IsRiskRule)
        {
            RiskGameplayAuthority.PrepareCheckResult(
                playerData,
                checkResult,
                ruleDecision);
        }

        int currentCheckScore = GameScoreAuthority.ApplyCheckResult(
            gameSessionData,
            playerData,
            checkResult,
            ruleDecision);

        if (playerData.gameStatus == GamePlayerStatus.Lost &&
            gameSessionData.GetEligiblePlayerCount() == 0 &&
            !playController.HasPendingCheckAnimations)
        {
            playController.EndGame(GameEndReason.NoEligiblePlayers);
        }

        if (playController.Phase == GamePlayPhase.Ended)
        {
            GameScoreAuthority.FinalizeEligiblePlayers(
                gameSessionData,
                playController.ResolveEligiblePlayerAtMatchEnd());
            gameSessionData.gameState = GameSessionState.Completed;
        }

        return new GameBingoCheckResolvedData
        {
            gameId = gameSessionData.gameId ?? string.Empty,
            userId = userId,
            revision = gameSessionData.revision,
            wasAccepted = true,
            checkResult = checkResult,
            playerStatus = playerData.gameStatus,
            currentCheckScore = currentCheckScore,
            currentMatchScore = playerData.currentMatchScore,
            matchCompleted = gameSessionData.gameState == GameSessionState.Completed,
            requiresRiskDecision = ruleDecision.requiresRiskDecision,
            latePatternCount = checkResult?.LatePatternCount ?? 0,
            availablePatternTypes = playController.GetAvailablePatternTypes(
                userId,
                gameSessionData.patternTypes)
        };
    }

    public static bool CompleteBingoCheckAnimation(
        GameSessionData gameSessionData,
        string userId)
    {
        if (gameSessionData == null ||
            gameSessionData.gamePlayController == null ||
            string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        GamePlayerData playerData = gameSessionData.GetPlayer(userId);

        if (playerData == null)
        {
            return false;
        }

        GamePlayController playController = gameSessionData.gamePlayController;
        bool resolveSuccessfulRiskCheckAsWin =
            playController.IsMatchEndPendingChecks &&
            RiskGameplayAuthority.IsRiskGame(gameSessionData) &&
            playerData.gameStatus == GamePlayerStatus.Eligible &&
            playerData.isRiskDecisionPending;

        if (!playController.TryCompleteBingoCheckAnimation(userId))
        {
            return false;
        }

        RiskGameplayAuthority.CompletePendingCheck(gameSessionData, playerData);

        if (resolveSuccessfulRiskCheckAsWin)
        {
            RiskGameplayAuthority.ResolveDecision(
                gameSessionData,
                userId,
                true);
        }

        if (playController.Phase != GamePlayPhase.Ended &&
            !playController.HasPendingCheckAnimations &&
            gameSessionData.GetEligiblePlayerCount() == 0)
        {
            playController.EndGame(GameEndReason.NoEligiblePlayers);
        }

        if (playController.Phase == GamePlayPhase.Ended &&
            gameSessionData.gameState != GameSessionState.Completed)
        {
            GameScoreAuthority.FinalizeEligiblePlayers(
                gameSessionData,
                playController.ResolveEligiblePlayerAtMatchEnd());
            gameSessionData.gameState = GameSessionState.Completed;
        }

        return true;
    }

    public static bool ResolveRiskDecision(
        GameSessionData gameSessionData,
        string userId,
        bool endPlayerGame)
    {
        return RiskGameplayAuthority.ResolveDecision(
            gameSessionData,
            userId,
            endPlayerGame);
    }

    private static bool BoardsMatch(LobbyBoardData authoritativeBoard, LobbyBoardData submittedBoard)
    {
        if (authoritativeBoard == null ||
            submittedBoard == null ||
            authoritativeBoard.ballCountType != submittedBoard.ballCountType ||
            authoritativeBoard.usesFreeCell != submittedBoard.usesFreeCell ||
            authoritativeBoard.cellNumbers == null ||
            submittedBoard.cellNumbers == null ||
            authoritativeBoard.cellNumbers.Count != submittedBoard.cellNumbers.Count)
        {
            return false;
        }

        for (int i = 0; i < authoritativeBoard.cellNumbers.Count; i++)
        {
            if (authoritativeBoard.cellNumbers[i] != submittedBoard.cellNumbers[i])
            {
                return false;
            }
        }

        return true;
    }
}

public static class DeathGameplayAuthority
{
    public static bool IsDeathGame(GameSessionData gameSessionData)
    {
        return gameSessionData?.gamePlayController?.IsDeathRule == true;
    }

    public static bool UpdateAutomaticBoards(GameSessionData gameSessionData)
    {
        if (!IsDeathGame(gameSessionData) ||
            gameSessionData.gameState != GameSessionState.InProgress ||
            gameSessionData.gamePlayController.Phase == GamePlayPhase.Ended ||
            gameSessionData.players == null)
        {
            return false;
        }

        bool changed = false;
        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null ||
                playerData.gameStatus != GamePlayerStatus.Eligible ||
                playerData.boardData?.cellNumbers == null)
            {
                continue;
            }

            changed |= GameAutomaticBoardAuthority.UpdatePlayer(
                gameSessionData,
                playerData,
                false,
                out GameAutomaticBoardCheckRequest checkRequest);

            if (checkRequest != null)
            {
                changed |= CompletePendingCheck(
                    gameSessionData,
                    playerData,
                    checkRequest);
            }
        }

        bool resolvedCheckingPlayers = ResolveOrdinaryCheckingPlayers(gameSessionData);
        changed |= resolvedCheckingPlayers;
        changed |= ResolveZeroEligibleTieBreak(gameSessionData);

        if (gameSessionData.gamePlayController.Phase == GamePlayPhase.Ended)
        {
            return changed;
        }

        if (resolvedCheckingPlayers &&
            gameSessionData.GetEligiblePlayerCount() == 1 &&
            !HasCheckingPlayers(gameSessionData))
        {
            changed |= gameSessionData.gamePlayController.EndGame(GameEndReason.RuleCompleted);
        }

        return changed;
    }

    private static bool ResolveZeroEligibleTieBreak(GameSessionData gameSessionData)
    {
        if (!IsDeathGame(gameSessionData) ||
            gameSessionData.gameState != GameSessionState.InProgress ||
            gameSessionData.players == null ||
            gameSessionData.GetEligiblePlayerCount() != 0)
        {
            return false;
        }

        GamePlayController playController = gameSessionData.gamePlayController;
        int finalBallCallId = 0;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData?.gameStatus == GamePlayerStatus.Checking &&
                playerData.deathCheckCanWin)
            {
                finalBallCallId = Math.Max(finalBallCallId, playerData.deathCheckBallCallId);
            }
        }

        if (finalBallCallId <= 0)
        {
            return false;
        }

        List<GamePlayerData> finalCohort = new List<GamePlayerData>();
        List<GamePlayerData> successfulPlayers = new List<GamePlayerData>();
        List<GamePlayerData> exhaustedPlayers = new List<GamePlayerData>();

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData?.gameStatus != GamePlayerStatus.Checking ||
                !playerData.deathCheckCanWin ||
                playerData.deathCheckBallCallId != finalBallCallId)
            {
                continue;
            }

            finalCohort.Add(playerData);

            if (!playerData.deathCheckSucceeded)
            {
                continue;
            }

            successfulPlayers.Add(playerData);

            if (playController.GetAvailablePatternTypes(
                    playerData.userId,
                    gameSessionData.patternTypes).Count == 0)
            {
                exhaustedPlayers.Add(playerData);
            }
        }

        if (finalCohort.Count <= 1)
        {
            return false;
        }

        if (playController.BallController?.HasRemainingNumbers != true)
        {
            if (playController.DeathFinalChecksWereRequired)
            {
                return false;
            }

            playController.MarkDeathFinalChecksRequired();
            return true;
        }

        if (successfulPlayers.Count <= 1)
        {
            bool changed = FinalizeTieBreakPlayers(
                gameSessionData,
                successfulPlayers);
            changed |= playController.EndGame(
                successfulPlayers.Count == 0
                    ? GameEndReason.NoEligiblePlayers
                    : GameEndReason.RuleCompleted);
            return changed;
        }

        if (exhaustedPlayers.Count > 0)
        {
            bool changed = FinalizeTieBreakPlayers(
                gameSessionData,
                exhaustedPlayers);
            changed |= playController.EndGame(GameEndReason.RuleCompleted);
            return changed;
        }

        bool continued = false;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData?.gameStatus != GamePlayerStatus.Checking)
            {
                continue;
            }

            if (successfulPlayers.Contains(playerData))
            {
                playerData.gameStatus = GamePlayerStatus.Eligible;
                ClearAutomaticRuntime(playerData);
                ClearDeathCheckRuntime(playerData);
                continued = true;
                continue;
            }

            continued |= GameScoreAuthority.TrySetFinalStatus(
                gameSessionData,
                playerData,
                GamePlayerStatus.Lost);
            ClearAutomaticRuntime(playerData);
        }

        return continued;
    }

    private static bool FinalizeTieBreakPlayers(
        GameSessionData gameSessionData,
        List<GamePlayerData> winningPlayers)
    {
        bool changed = false;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData?.gameStatus != GamePlayerStatus.Checking)
            {
                continue;
            }

            changed |= GameScoreAuthority.TrySetFinalStatus(
                gameSessionData,
                playerData,
                winningPlayers != null && winningPlayers.Contains(playerData)
                    ? GamePlayerStatus.Won
                    : GamePlayerStatus.Lost);
            ClearAutomaticRuntime(playerData);
        }

        return changed;
    }

    public static bool ApplyFrozenPlayerThreshold(GameSessionData gameSessionData)
    {
        if (!IsDeathGame(gameSessionData) || gameSessionData.players == null)
        {
            return false;
        }

        int threshold = GameSettings.instance != null
            ? GameSettings.instance.DeathFrozenPlayerOutEligibleCount
            : GameSettings.DefaultDeathFrozenPlayerOutEligibleCount;

        if (gameSessionData.GetEligiblePlayerCount() > threshold)
        {
            return false;
        }

        bool changed = false;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null ||
                playerData.isConnected ||
                (playerData.gameStatus != GamePlayerStatus.Eligible &&
                 playerData.gameStatus != GamePlayerStatus.Checking))
            {
                continue;
            }

            changed |= GameScoreAuthority.TrySetFinalStatus(
                gameSessionData,
                playerData,
                GamePlayerStatus.Lost);
            ClearAutomaticRuntime(playerData);
        }

        return changed;
    }

    public static bool TryHandleBallBoundary(
        GameSessionData gameSessionData,
        out bool changed)
    {
        changed = false;

        if (!IsDeathGame(gameSessionData) ||
            gameSessionData.gameState != GameSessionState.InProgress ||
            gameSessionData.gamePlayController?.BallTimer?.HasExpired() != true)
        {
            return false;
        }

        GamePlayController playController = gameSessionData.gamePlayController;
        int eligiblePlayerCount = gameSessionData.GetEligiblePlayerCount();

        if (eligiblePlayerCount > 1)
        {
            changed |= ResolveCheckingPlayersAsOut(gameSessionData);

            eligiblePlayerCount = gameSessionData.GetEligiblePlayerCount();

            if (eligiblePlayerCount > 1 || HasCheckingPlayers(gameSessionData))
            {
                return false;
            }
        }

        GameEndReason endReason = eligiblePlayerCount == 0
            ? GameEndReason.NoEligiblePlayers
            : GameEndReason.RuleCompleted;
        changed |= playController.EndGame(endReason);
        changed |= FinalizeMatch(gameSessionData);
        return true;
    }

    public static bool FinalizeMatch(GameSessionData gameSessionData)
    {
        if (!IsDeathGame(gameSessionData) || gameSessionData.players == null)
        {
            return false;
        }

        List<GamePlayerData> activeEligiblePlayers = new List<GamePlayerData>();
        List<GamePlayerData> checkingPlayers = new List<GamePlayerData>();
        GamePlayController playController = gameSessionData.gamePlayController;
        bool changed = false;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null)
            {
                continue;
            }

            if (gameSessionData.IsPlayerEligibleForCount(playerData))
            {
                activeEligiblePlayers.Add(playerData);
            }
            else if (playerData.gameStatus == GamePlayerStatus.Checking)
            {
                checkingPlayers.Add(playerData);
            }
        }

        if (activeEligiblePlayers.Count > 0)
        {
            for (int i = 0; i < activeEligiblePlayers.Count; i++)
            {
                changed |= GameScoreAuthority.TrySetFinalStatus(
                    gameSessionData,
                    activeEligiblePlayers[i],
                    GamePlayerStatus.Won);
                ClearAutomaticRuntime(activeEligiblePlayers[i]);
            }

            for (int i = 0; i < checkingPlayers.Count; i++)
            {
                changed |= GameScoreAuthority.TrySetFinalStatus(
                    gameSessionData,
                    checkingPlayers[i],
                    GamePlayerStatus.Lost);
                ClearAutomaticRuntime(checkingPlayers[i]);
            }
        }
        else if (checkingPlayers.Count > 0)
        {
            int finalBallCallId = 0;

            for (int i = 0; i < checkingPlayers.Count; i++)
            {
                if (checkingPlayers[i].deathCheckCanWin)
                {
                    finalBallCallId = Math.Max(
                        finalBallCallId,
                        checkingPlayers[i].deathCheckBallCallId);
                }
            }

            int finalCohortCount = 0;

            for (int i = 0; i < checkingPlayers.Count; i++)
            {
                GamePlayerData playerData = checkingPlayers[i];

                if (playerData.deathCheckCanWin &&
                    playerData.deathCheckBallCallId == finalBallCallId)
                {
                    finalCohortCount++;
                }
            }

            if (finalCohortCount > 1)
            {
                playController.MarkDeathFinalChecksRequired();
                changed = true;
            }

            for (int i = 0; i < checkingPlayers.Count; i++)
            {
                GamePlayerData playerData = checkingPlayers[i];
                bool isWinningFinalCheck =
                    finalBallCallId > 0 &&
                    playerData.deathCheckCanWin &&
                    playerData.deathCheckBallCallId == finalBallCallId &&
                    playerData.deathCheckSucceeded;

                changed |= GameScoreAuthority.TrySetFinalStatus(
                    gameSessionData,
                    playerData,
                    isWinningFinalCheck
                        ? GamePlayerStatus.Won
                        : GamePlayerStatus.Lost);
                ClearAutomaticRuntime(playerData);
            }
        }

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null || playerData.gameStatus != GamePlayerStatus.Eligible)
            {
                continue;
            }

            changed |= GameScoreAuthority.TrySetFinalStatus(
                gameSessionData,
                playerData,
                playerData.isConnected
                    ? GamePlayerStatus.Won
                    : GamePlayerStatus.Lost);
            ClearAutomaticRuntime(playerData);
        }

        return changed;
    }

    private static bool CompletePendingCheck(
        GameSessionData gameSessionData,
        GamePlayerData playerData,
        GameAutomaticBoardCheckRequest checkRequest)
    {
        if (checkRequest == null)
        {
            return false;
        }

        int ballCallId = checkRequest.ballCallId;
        List<int> calledNumbers = checkRequest.calledNumbers;
        bool canWinFinalCohort = gameSessionData.IsPlayerEligibleForCount(playerData);
        bool checkStarted = gameSessionData.gamePlayController.TryCheckAutomaticBingo(
            playerData.userId,
            playerData.boardData,
            playerData.markedCellIndices,
            calledNumbers,
            gameSessionData.patternTypes,
            out BingoCheckResult checkResult,
            out GameRuleCheckDecision _);

        if (!checkStarted)
        {
            return true;
        }

        playerData.gameStatus = GamePlayerStatus.Checking;
        playerData.deathCheckBallCallId = ballCallId;
        playerData.deathCheckSucceeded =
            checkResult?.HasCheckedPatterns == true &&
            checkResult.HasWinningPattern &&
            !checkResult.HasFailedPattern;
        playerData.deathCheckCanWin = canWinFinalCohort;

        if (playerData.controlType == GamePlayerControlType.Human &&
            playerData.isConnected)
        {
            gameSessionData.QueueBingoCheckPresentation(
                new GameBingoCheckResolvedData
                {
                    gameId = gameSessionData.gameId,
                    userId = playerData.userId,
                    wasAccepted = true,
                    checkResult = checkResult,
                    playerStatus = GamePlayerStatus.Checking,
                    currentCheckScore = 0,
                    currentMatchScore = playerData.currentMatchScore,
                    matchCompleted = false,
                    isAutomaticCheck = true,
                    availablePatternTypes =
                        gameSessionData.gamePlayController.GetAvailablePatternTypes(
                            playerData.userId,
                            gameSessionData.patternTypes)
                });
        }

        return true;
    }

    private static bool ResolveOrdinaryCheckingPlayers(
        GameSessionData gameSessionData)
    {
        if (gameSessionData?.players == null ||
            gameSessionData.GetEligiblePlayerCount() == 0)
        {
            return false;
        }

        bool changed = false;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData?.gameStatus != GamePlayerStatus.Checking ||
                HasPotentialSameBallCheck(
                    gameSessionData,
                    playerData.deathCheckBallCallId))
            {
                continue;
            }

            changed |= GameScoreAuthority.TrySetFinalStatus(
                gameSessionData,
                playerData,
                GamePlayerStatus.Lost);
            ClearAutomaticRuntime(playerData);
        }

        return changed;
    }

    private static bool HasPotentialSameBallCheck(
        GameSessionData gameSessionData,
        int ballCallId)
    {
        if (gameSessionData?.players == null || ballCallId <= 0)
        {
            return false;
        }

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (!gameSessionData.IsPlayerEligibleForCount(playerData))
            {
                continue;
            }

            if (playerData.automaticBoardLastProcessedBallCallId < ballCallId ||
                playerData.automaticBoardPendingBallCallId == ballCallId ||
                playerData.automaticBoardPendingCheckBallCallId == ballCallId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ResolveCheckingPlayersAsOut(GameSessionData gameSessionData)
    {
        bool changed = false;

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData?.gameStatus != GamePlayerStatus.Checking)
            {
                continue;
            }

            changed |= GameScoreAuthority.TrySetFinalStatus(
                gameSessionData,
                playerData,
                GamePlayerStatus.Lost);
            ClearAutomaticRuntime(playerData);
        }

        return changed;
    }

    private static bool HasCheckingPlayers(GameSessionData gameSessionData)
    {
        if (gameSessionData?.players == null)
        {
            return false;
        }

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            if (gameSessionData.players[i]?.gameStatus == GamePlayerStatus.Checking)
            {
                return true;
            }
        }

        return false;
    }

    private static void ClearDeathCheckRuntime(GamePlayerData playerData)
    {
        if (playerData == null)
        {
            return;
        }

        playerData.deathCheckBallCallId = 0;
        playerData.deathCheckSucceeded = false;
        playerData.deathCheckCanWin = false;
    }

    private static void ClearAutomaticRuntime(GamePlayerData playerData)
    {
        if (playerData == null)
        {
            return;
        }

        playerData.automaticBoardPendingCellIndex = -1;
        playerData.automaticBoardPendingBallCallId = 0;
        playerData.automaticBoardMarkDueTime = 0d;
        playerData.automaticBoardPendingCheckBallCallId = 0;
        playerData.automaticBoardCheckDueTime = 0d;
    }
}
