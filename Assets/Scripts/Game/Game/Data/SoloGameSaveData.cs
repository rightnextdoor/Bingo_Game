using System;
using System.Collections.Generic;

[Serializable]
public class BingoCheckHistoryEntryData
{
    public List<BingoPatternIdentity> patterns = new List<BingoPatternIdentity>();

    public BingoCheckHistoryEntryData()
    {
    }

    public BingoCheckHistoryEntryData(BingoCheckResult checkResult)
    {
        if (checkResult?.patterns == null)
        {
            return;
        }

        for (int i = 0; i < checkResult.patterns.Count; i++)
        {
            BingoPatternIdentity identity =
                BingoPatternIdentity.FromResult(checkResult.patterns[i]);

            if (identity != null)
            {
                patterns.Add(identity);
            }
        }
    }

    public BingoCheckHistoryEntryData(BingoCheckHistoryEntryData entryData)
    {
        if (entryData?.patterns == null)
        {
            return;
        }

        for (int i = 0; i < entryData.patterns.Count; i++)
        {
            BingoPatternIdentity identity = entryData.patterns[i];

            if (identity != null)
            {
                patterns.Add(new BingoPatternIdentity(identity));
            }
        }
    }
}

[Serializable]
public class BingoPlayerCheckHistoryData
{
    public string playerId = string.Empty;
    public List<BingoCheckHistoryEntryData> checks =
        new List<BingoCheckHistoryEntryData>();

    public BingoPlayerCheckHistoryData()
    {
    }

    public BingoPlayerCheckHistoryData(BingoPlayerCheckHistoryData historyData)
    {
        if (historyData == null)
        {
            return;
        }

        playerId = historyData.playerId ?? string.Empty;

        if (historyData.checks == null)
        {
            return;
        }

        for (int i = 0; i < historyData.checks.Count; i++)
        {
            checks.Add(new BingoCheckHistoryEntryData(historyData.checks[i]));
        }
    }
}

[Serializable]
public class SoloGameCheckpointData
{
    public GameSessionData gameSessionData = new GameSessionData();
    public bool ballTimerWasActive;
    public float ballTimerRemainingSeconds;
    public bool riskTimerWasActive;
    public float riskTimerRemainingSeconds;
    public bool replayCurrentBall;
    public List<BingoPlayerCheckHistoryData> checkerHistory =
        new List<BingoPlayerCheckHistoryData>();

    public SoloGameCheckpointData()
    {
    }

    public SoloGameCheckpointData(GameSessionData sessionData)
    {
        if (sessionData == null)
        {
            return;
        }

        gameSessionData = new GameSessionData(sessionData);

        GamePlayController playController = sessionData.gamePlayController;
        ballTimerWasActive = playController?.BallTimer?.IsActive == true;
        ballTimerRemainingSeconds = playController?.BallTimer?.GetRemainingSeconds() ?? 0f;
        riskTimerWasActive = playController?.RiskTimer?.IsActive == true;
        riskTimerRemainingSeconds = playController?.RiskTimer?.GetRemainingSeconds() ?? 0f;
        replayCurrentBall = playController?.BallController?.CalledCount > 0;
        checkerHistory = playController?.CaptureBingoCheckHistory() ??
                         new List<BingoPlayerCheckHistoryData>();
    }

    public SoloGameCheckpointData(SoloGameCheckpointData checkpointData)
    {
        if (checkpointData == null)
        {
            return;
        }

        gameSessionData = checkpointData.gameSessionData != null
            ? new GameSessionData(checkpointData.gameSessionData)
            : new GameSessionData();
        ballTimerWasActive = checkpointData.ballTimerWasActive;
        ballTimerRemainingSeconds = checkpointData.ballTimerRemainingSeconds;
        riskTimerWasActive = checkpointData.riskTimerWasActive;
        riskTimerRemainingSeconds = checkpointData.riskTimerRemainingSeconds;
        replayCurrentBall = checkpointData.replayCurrentBall;

        if (checkpointData.checkerHistory == null)
        {
            return;
        }

        for (int i = 0; i < checkpointData.checkerHistory.Count; i++)
        {
            checkerHistory.Add(
                new BingoPlayerCheckHistoryData(checkpointData.checkerHistory[i]));
        }
    }

    public bool IsValidFor(string ownerUserId)
    {
        if (gameSessionData == null ||
            gameSessionData.playMode != MainMenuPlayMode.Solo ||
            gameSessionData.runtimeType != SessionRuntimeType.Local ||
            gameSessionData.gameState == GameSessionState.Completed ||
            string.IsNullOrWhiteSpace(gameSessionData.gameId) ||
            string.IsNullOrWhiteSpace(ownerUserId))
        {
            return false;
        }

        GamePlayerData playerData = gameSessionData.GetPlayer(ownerUserId);
        return playerData != null && playerData.userTag == UserTag.Player;
    }
}

[Serializable]
public class SoloGameSaveData
{
    public int dataVersion = 1;
    public bool hasSavedGame;
    public string ownerUserId = string.Empty;
    public string displayTitle = string.Empty;
    public SoloGameCheckpointData checkpoint = new SoloGameCheckpointData();

    public SoloGameSaveData()
    {
    }

    public SoloGameSaveData(SoloGameSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        dataVersion = saveData.dataVersion;
        hasSavedGame = saveData.hasSavedGame;
        ownerUserId = saveData.ownerUserId ?? string.Empty;
        displayTitle = saveData.displayTitle ?? string.Empty;
        checkpoint = saveData.checkpoint != null
            ? new SoloGameCheckpointData(saveData.checkpoint)
            : new SoloGameCheckpointData();
    }

    public bool IsValidFor(string userId)
    {
        return hasSavedGame &&
               !string.IsNullOrWhiteSpace(userId) &&
               string.Equals(ownerUserId, userId, StringComparison.Ordinal) &&
               checkpoint?.IsValidFor(userId) == true;
    }
}
