using System;
using System.Collections.Generic;

public sealed class GameAutomaticBoardCheckRequest
{
    public int ballCallId;
    public List<int> calledNumbers;

    public GameAutomaticBoardCheckRequest(int ballCallId, List<int> calledNumbers)
    {
        this.ballCallId = ballCallId;
        this.calledNumbers = calledNumbers ?? new List<int>();
    }
}

public static class GameAutomaticBoardAuthority
{
    public const int TakeoverVisibleBallCount = 4;

    public static bool UpdatePlayer(
        GameSessionData gameSessionData,
        GamePlayerData playerData,
        bool includeFinalBall,
        out GameAutomaticBoardCheckRequest checkRequest)
    {
        checkRequest = null;

        if (gameSessionData == null ||
            playerData == null ||
            !IsAutomaticBoardEnabled(gameSessionData, playerData) ||
            playerData.gameStatus != GamePlayerStatus.Eligible ||
            playerData.boardData?.cellNumbers == null ||
            gameSessionData.gameState != GameSessionState.InProgress ||
            gameSessionData.gamePlayController?.Phase == GamePlayPhase.Ended)
        {
            return false;
        }

        double currentTime = GamePlayTimer.GetCurrentTime();
        bool changed = TryCompletePendingCheck(
            gameSessionData,
            playerData,
            currentTime,
            out checkRequest);

        if (checkRequest != null)
        {
            return true;
        }

        changed |= CompletePendingMark(gameSessionData, playerData, currentTime);

        if (playerData.automaticBoardPendingCellIndex >= 0 ||
            playerData.automaticBoardPendingCheckBallCallId > 0)
        {
            return changed;
        }

        changed |= ScheduleNextMark(
            gameSessionData,
            playerData,
            currentTime,
            includeFinalBall);
        return changed;
    }

    public static bool IsAutomaticBoardEnabled(
        GameSessionData gameSessionData,
        GamePlayerData playerData)
    {
        return playerData?.isAutomaticBoardEnabled == true;
    }

    public static void PreparePlayerTakeover(
        GameSessionData gameSessionData,
        GamePlayerData playerData)
    {
        if (playerData == null)
        {
            return;
        }

        int calledCount =
            gameSessionData?.gamePlayController?.BallController?.CalledNumbers?.Count ?? 0;
        playerData.automaticBoardLastProcessedBallCallId = Math.Max(
            0,
            calledCount - TakeoverVisibleBallCount);
        ClearPendingActions(playerData);
    }

    public static void ClearPendingActions(GamePlayerData playerData)
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

    private static bool ScheduleNextMark(
        GameSessionData gameSessionData,
        GamePlayerData playerData,
        double currentTime,
        bool includeFinalBall)
    {
        IReadOnlyList<int> calledNumbers =
            gameSessionData.gamePlayController.BallController?.CalledNumbers;
        int calledCount = calledNumbers?.Count ?? 0;
        int maximumAutomaticCallId = calledCount;

        if (!includeFinalBall)
        {
            int finalBallCallId = Math.Max(0, (int)gameSessionData.ballCountType);
            maximumAutomaticCallId = Math.Min(
                calledCount,
                Math.Max(0, finalBallCallId - 1));
        }

        if (playerData.automaticBoardLastProcessedBallCallId >= maximumAutomaticCallId)
        {
            return false;
        }

        int nextCallId = playerData.automaticBoardLastProcessedBallCallId + 1;
        int calledNumber = calledNumbers[nextCallId - 1];
        int cellIndex = FindBoardCell(playerData.boardData, calledNumber);

        if (cellIndex < 0)
        {
            playerData.automaticBoardLastProcessedBallCallId = nextCallId;
            return false;
        }

        playerData.automaticBoardPendingCellIndex = cellIndex;
        playerData.automaticBoardPendingBallCallId = nextCallId;
        playerData.automaticBoardMarkDueTime = currentTime + GetRandomDelay(
            GameSettings.instance != null
                ? GameSettings.instance.AutomaticMarkDelayMinimumSeconds
                : GameSettings.DefaultAutomaticMarkDelayMinimumSeconds,
            GameSettings.instance != null
                ? GameSettings.instance.AutomaticMarkDelayMaximumSeconds
                : GameSettings.DefaultAutomaticMarkDelayMaximumSeconds);
        return false;
    }

    private static bool CompletePendingMark(
        GameSessionData gameSessionData,
        GamePlayerData playerData,
        double currentTime)
    {
        if (playerData.automaticBoardPendingCellIndex < 0 ||
            currentTime < playerData.automaticBoardMarkDueTime)
        {
            return false;
        }

        int cellIndex = playerData.automaticBoardPendingCellIndex;
        int ballCallId = playerData.automaticBoardPendingBallCallId;
        bool markedCellChanged = playerData.TrySetMarkedCell(cellIndex, true);

        if (markedCellChanged)
        {
            gameSessionData.QueueMarkedCellUpdate(
                playerData,
                cellIndex,
                true,
                true);
        }
        playerData.automaticBoardLastProcessedBallCallId = Math.Max(
            playerData.automaticBoardLastProcessedBallCallId,
            ballCallId);
        playerData.automaticBoardPendingCellIndex = -1;
        playerData.automaticBoardPendingBallCallId = 0;
        playerData.automaticBoardMarkDueTime = 0d;

        List<int> calledNumbers = GetCalledNumberPrefix(gameSessionData, ballCallId);
        List<BingoPatternCheckResult> completedPatterns =
            gameSessionData.gamePlayController.GetCompletedAvailablePatterns(
                playerData.userId,
                playerData.boardData,
                playerData.markedCellIndices,
                calledNumbers,
                gameSessionData.patternTypes);

        if (completedPatterns.Count == 0)
        {
            return markedCellChanged;
        }

        playerData.automaticBoardPendingCheckBallCallId = ballCallId;
        playerData.automaticBoardCheckDueTime = currentTime + GetRandomDelay(
            GameSettings.instance != null
                ? GameSettings.instance.AutomaticBingoDelayMinimumSeconds
                : GameSettings.DefaultAutomaticBingoDelayMinimumSeconds,
            GameSettings.instance != null
                ? GameSettings.instance.AutomaticBingoDelayMaximumSeconds
                : GameSettings.DefaultAutomaticBingoDelayMaximumSeconds);
        return markedCellChanged;
    }

    private static bool TryCompletePendingCheck(
        GameSessionData gameSessionData,
        GamePlayerData playerData,
        double currentTime,
        out GameAutomaticBoardCheckRequest checkRequest)
    {
        checkRequest = null;
        int ballCallId = playerData.automaticBoardPendingCheckBallCallId;

        if (ballCallId <= 0 || currentTime < playerData.automaticBoardCheckDueTime)
        {
            return false;
        }

        checkRequest = new GameAutomaticBoardCheckRequest(
            ballCallId,
            GetCalledNumberPrefix(gameSessionData, ballCallId));
        playerData.automaticBoardPendingCheckBallCallId = 0;
        playerData.automaticBoardCheckDueTime = 0d;
        return true;
    }

    private static List<int> GetCalledNumberPrefix(
        GameSessionData gameSessionData,
        int ballCallId)
    {
        IReadOnlyList<int> calledNumbers =
            gameSessionData.gamePlayController?.BallController?.CalledNumbers;
        int count = Math.Min(Math.Max(0, ballCallId), calledNumbers?.Count ?? 0);
        List<int> result = new List<int>(count);

        for (int i = 0; i < count; i++)
        {
            result.Add(calledNumbers[i]);
        }

        return result;
    }

    private static int FindBoardCell(LobbyBoardData boardData, int number)
    {
        if (boardData?.cellNumbers == null)
        {
            return -1;
        }

        for (int i = 0; i < boardData.cellNumbers.Count; i++)
        {
            if (boardData.cellNumbers[i] == number)
            {
                return i;
            }
        }

        return -1;
    }

    private static double GetRandomDelay(float minimumSeconds, float maximumSeconds)
    {
        float minimum = Math.Max(0f, minimumSeconds);
        float maximum = Math.Max(minimum, maximumSeconds);
        return UnityEngine.Random.Range(minimum, maximum);
    }
}
