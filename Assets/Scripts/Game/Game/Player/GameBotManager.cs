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

        if (playerData == null ||
            playerData.userTag != UserTag.Player ||
            playerData.controlType == GamePlayerControlType.Bot)
        {
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

            if (!DeathGameplayAuthority.IsDeathGame(gameSessionData))
            {
                playerData.gameStatus = GamePlayerStatus.Eligible;
                GameAutomaticBoardAuthority.PreparePlayerTakeover(
                    gameSessionData,
                    playerData);
            }

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
            playerData.controlType != GamePlayerControlType.Human ||
            playerData.returnState == GamePlayerReturnState.DeclinedReturn)
        {
            return false;
        }

        bool shouldUseAutomaticBoard = DeathGameplayAuthority.IsDeathGame(gameSessionData);
        bool changed = playerData.isConnected ||
                       !playerData.canRejoin ||
                       playerData.returnState != GamePlayerReturnState.FrozenAwaitingReturn ||
                       playerData.isAutomaticBoardEnabled != shouldUseAutomaticBoard;
        playerData.isConnected = false;
        playerData.isGameSceneReady = true;
        playerData.canRejoin = true;
        playerData.returnState = GamePlayerReturnState.FrozenAwaitingReturn;
        playerData.isAutomaticBoardEnabled = shouldUseAutomaticBoard;

        if (!shouldUseAutomaticBoard)
        {
            GameAutomaticBoardAuthority.ClearPendingActions(playerData);
        }

        return changed;
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

        if (playController.IsRiskRule)
        {
            RiskGameplayAuthority.CompletePendingCheck(gameSessionData, playerData);
        }

        if (playerData.gameStatus == GamePlayerStatus.Won)
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
            GameScoreAuthority.FinalizeEligiblePlayers(
                gameSessionData,
                playController.ResolveEligiblePlayerAtMatchEnd());
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
