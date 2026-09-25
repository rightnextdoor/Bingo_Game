using System;
using UnityEngine;

public sealed class GameBotTransitionResult
{
    public bool changed;
    public bool botTookControl;
    public bool shouldDeleteGame;
    public GamePlayerDepartureReason departureReason;
}

public static class GameBotManager
{
    public static bool UpdateBots(GameSessionData gameSessionData)
    {
        if (gameSessionData == null ||
            gameSessionData.players == null ||
            gameSessionData.gameState != GameSessionState.InProgress ||
            gameSessionData.gamePlayController == null ||
            gameSessionData.gamePlayController.Phase == GamePlayPhase.Ended ||
            SessionPauseManager.IsPaused)
        {
            return false;
        }

        bool changed = false;

        if (!DeathGameplayAuthority.IsDeathGame(gameSessionData))
        {
            for (int i = 0; i < gameSessionData.players.Count; i++)
            {
                GamePlayerData playerData = gameSessionData.players[i];

                if (!IsBotControlledAndPlaying(playerData))
                {
                    continue;
                }

                changed |= GameAutomaticBoardAuthority.UpdatePlayer(
                    gameSessionData,
                    playerData,
                    true,
                    out GameAutomaticBoardCheckRequest checkRequest);

                if (checkRequest != null)
                {
                    changed |= ProcessAutomaticCheck(
                        gameSessionData,
                        playerData,
                        checkRequest);
                }
            }
        }

        changed |= ResolvePendingRiskDecisions(gameSessionData);
        return changed;
    }

    public static GameBotTransitionResult HandlePlayerLeave(
        GameSessionData gameSessionData,
        string userId)
    {
        return HandlePlayerDeparture(
            gameSessionData,
            userId,
            GamePlayerDepartureReason.Leave);
    }

    public static GameBotTransitionResult HandlePlayerDeclinedReturn(
        GameSessionData gameSessionData,
        string userId)
    {
        return HandlePlayerDeparture(
            gameSessionData,
            userId,
            GamePlayerDepartureReason.DeclinedReturn);
    }

    public static GameBotTransitionResult HandleHostKick(
        GameSessionData gameSessionData,
        string userId)
    {
        return HandlePlayerDeparture(
            gameSessionData,
            userId,
            GamePlayerDepartureReason.HostKick);
    }

    public static GameBotTransitionResult HandlePlayerDeparture(
        GameSessionData gameSessionData,
        string userId,
        GamePlayerDepartureReason departureReason)
    {
        GameBotTransitionResult result = new GameBotTransitionResult
        {
            departureReason = departureReason
        };

        if (gameSessionData == null || string.IsNullOrWhiteSpace(userId))
        {
            return result;
        }

        GamePlayerData playerData = gameSessionData.GetPlayer(userId);

        if (playerData == null || playerData.userTag != UserTag.Player)
        {
            return result;
        }

        if (playerData.controlType == GamePlayerControlType.Bot)
        {
            if (!playerData.canRejoin && !playerData.isConnected)
            {
                return result;
            }

            playerData.isConnected = false;
            playerData.canRejoin = false;
            playerData.returnState = GamePlayerReturnState.DeclinedReturn;
            result.changed = true;

            if (gameSessionData.GetRemainingRealHumanCount() == 0)
            {
                gameSessionData.gamePlayController?.EndGame(GameEndReason.AuthorityEnded);
                gameSessionData.gameState = GameSessionState.Completed;
                result.shouldDeleteGame = true;
            }

            return result;
        }

        GameScoreAuthority.FinalizeDepartingHumanLoss(gameSessionData, playerData);
        gameSessionData.gamePlayController?.CancelPendingCheckAnimation(userId);
        playerData.isConnected = false;
        playerData.canRejoin = false;
        playerData.returnState = GamePlayerReturnState.DeclinedReturn;
        result.changed = true;

        bool canContinueWithBot =
            gameSessionData.gameState == GameSessionState.InProgress &&
            (playerData.gameStatus == GamePlayerStatus.Eligible ||
             playerData.gameStatus == GamePlayerStatus.Checking) &&
            gameSessionData.GetRemainingRealHumanCount(userId) > 0;

        if (canContinueWithBot)
        {
            playerData.controlType = GamePlayerControlType.Bot;
            playerData.isAutomaticBoardEnabled = true;
            playerData.isGameSceneReady = true;
            playerData.gameStatus = GamePlayerStatus.Eligible;
            playerData.isRankWinBlocked = false;
            GameAutomaticBoardAuthority.PreparePlayerTakeover(
                gameSessionData,
                playerData);

            result.botTookControl = true;
            return result;
        }

        playerData.isGameSceneReady = false;
        playerData.isAutomaticBoardEnabled = false;
        GameAutomaticBoardAuthority.ClearPendingActions(playerData);

        if (playerData.gameStatus == GamePlayerStatus.Eligible ||
            playerData.gameStatus == GamePlayerStatus.Checking)
        {
            GameScoreAuthority.TrySetFinalStatus(
                gameSessionData,
                playerData,
                GamePlayerStatus.Lost);
        }

        if (gameSessionData.GetRemainingRealHumanCount() == 0)
        {
            gameSessionData.gamePlayController?.EndGame(GameEndReason.AuthorityEnded);
            gameSessionData.gameState = GameSessionState.Completed;
            result.shouldDeleteGame = true;
        }

        return result;
    }

    public static bool FreezePlayerAwaitingReturn(
        GameSessionData gameSessionData,
        string userId)
    {
        GamePlayerData playerData = gameSessionData?.GetPlayer(userId);

        if (playerData == null ||
            playerData.userTag != UserTag.Player ||
            playerData.returnState == GamePlayerReturnState.DeclinedReturn)
        {
            return false;
        }

        bool shouldUseAutomaticBoard = playerData.controlType == GamePlayerControlType.Bot ||
                                       DeathGameplayAuthority.IsDeathGame(gameSessionData);
        GamePlayerReturnState frozenState = playerData.controlType == GamePlayerControlType.Bot
            ? GamePlayerReturnState.Active
            : GamePlayerReturnState.FrozenAwaitingReturn;
        bool changed = playerData.isConnected ||
                       !playerData.canRejoin ||
                       playerData.returnState != frozenState ||
                       playerData.isAutomaticBoardEnabled != shouldUseAutomaticBoard;
        playerData.isConnected = false;
        playerData.isGameSceneReady = true;
        playerData.canRejoin = true;
        playerData.returnState = frozenState;
        playerData.isAutomaticBoardEnabled = shouldUseAutomaticBoard;

        if (!shouldUseAutomaticBoard)
        {
            GameAutomaticBoardAuthority.ClearPendingActions(playerData);
        }

        return changed;
    }

    public static bool MarkPlayerReconnecting(GameSessionData gameSessionData, string userId)
    {
        GamePlayerData playerData = gameSessionData?.GetPlayer(userId);

        if (playerData == null || playerData.userTag != UserTag.Player ||
            playerData.returnState == GamePlayerReturnState.DeclinedReturn ||
            !playerData.canRejoin)
        {
            return false;
        }

        playerData.isConnected = false;
        playerData.returnState = GamePlayerReturnState.Reconnecting;
        // Keep the board's existing manual/automatic control. In particular,
        // Death continues marking without converting the human to a bot.
        return true;
    }

    public static bool RestorePlayerControl(
        GameSessionData gameSessionData,
        string userId,
        bool isGameSceneReady)
    {
        GamePlayerData playerData = gameSessionData?.GetPlayer(userId);

        if (playerData == null ||
            playerData.userTag != UserTag.Player ||
            playerData.controlType != GamePlayerControlType.Human ||
            playerData.returnState == GamePlayerReturnState.DeclinedReturn ||
            !playerData.canRejoin)
        {
            return false;
        }

        bool shouldUseAutomaticBoard = DeathGameplayAuthority.IsDeathGame(gameSessionData);
        bool changed = !playerData.isConnected ||
                       playerData.isGameSceneReady != isGameSceneReady ||
                       playerData.returnState != GamePlayerReturnState.Active ||
                       playerData.isAutomaticBoardEnabled != shouldUseAutomaticBoard;
        playerData.isConnected = true;
        playerData.isGameSceneReady = isGameSceneReady;
        playerData.returnState = GamePlayerReturnState.Active;
        playerData.isAutomaticBoardEnabled = shouldUseAutomaticBoard;

        if (!shouldUseAutomaticBoard)
        {
            GameAutomaticBoardAuthority.ClearPendingActions(playerData);
        }

        return changed;
    }

    public static bool HandleFrozenPlayerThreshold(
        GameSessionData gameSessionData,
        string userId)
    {
        GamePlayerData playerData = gameSessionData?.GetPlayer(userId);

        if (playerData == null ||
            playerData.controlType != GamePlayerControlType.Human ||
            playerData.returnState != GamePlayerReturnState.FrozenAwaitingReturn)
        {
            return false;
        }

        GameScoreAuthority.FinalizeDepartingHumanLoss(gameSessionData, playerData);
        gameSessionData.gamePlayController?.CancelPendingCheckAnimation(userId);
        bool hasOtherRealHuman = gameSessionData.GetRemainingRealHumanCount(userId) > 0;

        if (!hasOtherRealHuman)
        {
            playerData.canRejoin = false;
            playerData.returnState = GamePlayerReturnState.DeclinedReturn;
            GameScoreAuthority.TrySetFinalStatus(gameSessionData, playerData, GamePlayerStatus.Lost);
            gameSessionData.gamePlayController?.EndGame(GameEndReason.AuthorityEnded);
            gameSessionData.gameState = GameSessionState.Completed;
            return true;
        }

        // The human's score is final, but the same board slot can keep playing as a bot.
        playerData.controlType = GamePlayerControlType.Bot;
        playerData.returnState = GamePlayerReturnState.Active;
        playerData.gameStatus = GamePlayerStatus.Eligible;
        playerData.isRankWinBlocked = false;
        playerData.isAutomaticBoardEnabled = true;
        playerData.isGameSceneReady = true;
        playerData.canRejoin = true;
        GameAutomaticBoardAuthority.PreparePlayerTakeover(gameSessionData, playerData);
        if (playerData.isRiskSubmitReconnectGrace)
        {
            RiskGameplayAuthority.ResumeSubmitAfterReconnect(gameSessionData, playerData);
        }
        return true;
    }

    public static bool RestoreSpectatorAfterTakeover(
        GameSessionData gameSessionData,
        string userId,
        bool isGameSceneReady)
    {
        GamePlayerData playerData = gameSessionData?.GetPlayer(userId);

        if (playerData == null ||
            playerData.userTag != UserTag.Player ||
            playerData.controlType != GamePlayerControlType.Bot ||
            !playerData.canRejoin)
        {
            return false;
        }

        bool changed = !playerData.isConnected ||
                       playerData.isGameSceneReady != isGameSceneReady;
        playerData.isConnected = true;
        playerData.isGameSceneReady = isGameSceneReady;
        playerData.returnState = GamePlayerReturnState.Active;
        return changed;
    }

    public static float CalculateRiskStopChance(
        float remainingSeconds,
        float totalSeconds)
    {
        if (totalSeconds <= 0f)
        {
            return 1f;
        }

        float progress = 1f - Mathf.Clamp01(remainingSeconds / totalSeconds);
        return progress * progress;
    }

    private static bool IsBotControlledAndPlaying(GamePlayerData playerData)
    {
        return playerData != null &&
               playerData.controlType == GamePlayerControlType.Bot &&
               playerData.gameStatus == GamePlayerStatus.Eligible &&
               !playerData.hasRiskCashedOut &&
               !playerData.isRiskDecisionPending;
    }

    private static bool ProcessAutomaticCheck(
        GameSessionData gameSessionData,
        GamePlayerData playerData,
        GameAutomaticBoardCheckRequest checkRequest)
    {
        GamePlayController playController = gameSessionData.gamePlayController;
        bool checkStarted = playController.TryCheckAutomaticBingo(
            playerData.userId,
            playerData.boardData,
            playerData.markedCellIndices,
            checkRequest.calledNumbers,
            gameSessionData.patternTypes,
            playController.IsRiskRule ? playerData.lateRiskPatterns : null,
            out BingoCheckResult checkResult,
            out GameRuleCheckDecision ruleDecision);

        if (!checkStarted)
        {
            return true;
        }

        if (playController.IsRiskRule)
        {
            RiskGameplayAuthority.PrepareCheckResult(
                playerData,
                checkResult,
                ruleDecision);
        }

        GameScoreAuthority.ApplyCheckResult(
            gameSessionData,
            playerData,
            checkResult,
            ruleDecision);

        if (playerData.hasPendingRankCheck)
        {
            GameRankAuthority.CompleteDefaultRankedCheck(
                gameSessionData,
                playerData);
        }

        if (playController.IsRiskRule)
        {
            RiskGameplayAuthority.CompletePendingCheck(gameSessionData, playerData);
        }

        if (!GameRankAuthority.IsEnabled(gameSessionData) &&
            playerData.gameStatus == GamePlayerStatus.Won)
        {
            playController.EndGame(GameEndReason.RuleCompleted);
        }
        else if (playerData.gameStatus == GamePlayerStatus.Lost &&
                 gameSessionData.GetEligiblePlayerCount() == 0)
        {
            playController.EndGame(GameEndReason.NoEligiblePlayers);
        }

        if (playController.Phase == GamePlayPhase.Ended)
        {
            if (RiskGameplayAuthority.IsRiskGame(gameSessionData))
            {
                GameRankAuthority.FinalizeRiskMatch(gameSessionData);
            }
            else
            {
                GameScoreAuthority.FinalizeEligiblePlayers(
                    gameSessionData,
                    playController.ResolveEligiblePlayerAtMatchEnd());
            }
            gameSessionData.gameState = GameSessionState.Completed;
        }

        return true;
    }

    private static bool ResolvePendingRiskDecisions(GameSessionData gameSessionData)
    {
        GamePlayController playController = gameSessionData.gamePlayController;

        if (!playController.IsRiskRule || gameSessionData.players == null)
        {
            return false;
        }

        bool changed = false;
        float remainingSeconds = playController.RiskTimer?.GetRemainingSeconds() ?? 0f;
        float stopChance = CalculateRiskStopChance(
            remainingSeconds,
            playController.RiskMatchDurationSeconds);

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null ||
                playerData.controlType != GamePlayerControlType.Bot ||
                playerData.gameStatus != GamePlayerStatus.Eligible ||
                playerData.hasRiskCashedOut ||
                !playerData.isRiskDecisionPending)
            {
                continue;
            }

            bool endPlayerGame = UnityEngine.Random.value <= stopChance;
            changed |= RiskGameplayAuthority.ResolveDecision(
                gameSessionData,
                playerData.userId,
                endPlayerGame);
        }

        return changed;
    }

}
