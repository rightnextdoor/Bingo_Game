using System;
using System.Collections.Generic;

public static class GameBingoCheckAuthority
{
    public static bool UpdateSessionLoop(GameSessionData gameSessionData)
    {
        if (gameSessionData == null ||
            gameSessionData.gameState != GameSessionState.InProgress ||
            gameSessionData.gamePlayController == null)
        {
            return false;
        }

        GamePlayController playController = gameSessionData.gamePlayController;
        bool changed = playController.UpdateBallCallLoop();

        if (playController.Phase == GamePlayPhase.Ended &&
            gameSessionData.gameState != GameSessionState.Completed)
        {
            gameSessionData.FinalizeEligiblePlayers(
                playController.ResolveEligiblePlayerAtMatchEnd());
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

        GamePlayerData playerData = gameSessionData.GetPlayer(userId);

        if (playerData == null || playerData.userTag == UserTag.Bot)
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
            playerData.gameStatus != GamePlayerStatus.Eligible)
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

        if (!playController.TryCheckBingo(
                userId,
                playerData.boardData,
                requestData.markedCellIndices,
                gameSessionData.patternTypes,
                out BingoCheckResult checkResult,
                out GameRuleCheckDecision ruleDecision))
        {
            return GameBingoCheckResolvedData.Rejected(
                gameId,
                userId,
                revision,
                "The Bingo checker or active rule could not process this request.");
        }

        playerData.gameStatus = ruleDecision.playerStatus;

        if (playerData.gameStatus == GamePlayerStatus.Lost &&
            gameSessionData.GetPlayerCountWithStatus(GamePlayerStatus.Eligible) == 0)
        {
            playController.EndGame(GameEndReason.NoEligiblePlayers);
        }

        if (playController.Phase == GamePlayPhase.Ended)
        {
            gameSessionData.FinalizeEligiblePlayers(
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
            matchCompleted = gameSessionData.gameState == GameSessionState.Completed,
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

        if (playerData == null ||
            !gameSessionData.gamePlayController.TryCompleteBingoCheckAnimation(userId))
        {
            return false;
        }

        if (gameSessionData.gamePlayController.Phase == GamePlayPhase.Ended &&
            gameSessionData.gameState != GameSessionState.Completed)
        {
            gameSessionData.FinalizeEligiblePlayers(
                gameSessionData.gamePlayController.ResolveEligiblePlayerAtMatchEnd());
            gameSessionData.gameState = GameSessionState.Completed;
        }

        return true;
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
