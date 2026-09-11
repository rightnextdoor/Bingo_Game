using System;

[Serializable]
public class GameSessionResult
{
    public bool success;
    public GameSessionOperationType operationType;
    public GameSessionFailureType failureType;
    public string failureMessage;
    public string gameId;
    public string lobbyId;
    public GameSessionData gameSessionData;
    public GameFinalScoreResultData finalScoreResult;

    public GameSessionResult()
    {
        success = false;
        operationType = GameSessionOperationType.None;
        failureType = GameSessionFailureType.Unknown;
        failureMessage = string.Empty;
        gameId = string.Empty;
        lobbyId = string.Empty;
        gameSessionData = null;
        finalScoreResult = null;
    }

    public GameSessionResult WithFinalScore(GameFinalScoreResultData scoreResult)
    {
        finalScoreResult = scoreResult;
        return this;
    }

    public static GameSessionResult Succeeded(GameSessionOperationType operationType, GameSessionData gameSessionData)
    {
        if (gameSessionData == null || string.IsNullOrWhiteSpace(gameSessionData.gameId))
        {
            return Failed(operationType, GameSessionFailureType.Unknown, "The Game session data is missing.");
        }

        return new GameSessionResult
        {
            success = true,
            operationType = operationType,
            failureType = GameSessionFailureType.None,
            failureMessage = string.Empty,
            gameId = gameSessionData.gameId,
            lobbyId = gameSessionData.lobbyId,
            gameSessionData = new GameSessionData(gameSessionData)
        };
    }

    public static GameSessionResult Acknowledged(GameSessionOperationType operationType, GameSessionData gameSessionData)
    {
        if (gameSessionData == null || string.IsNullOrWhiteSpace(gameSessionData.gameId))
        {
            return Failed(operationType, GameSessionFailureType.Unknown, "The Game session data is missing.");
        }

        return new GameSessionResult
        {
            success = true,
            operationType = operationType,
            failureType = GameSessionFailureType.None,
            failureMessage = string.Empty,
            gameId = gameSessionData.gameId,
            lobbyId = gameSessionData.lobbyId,
            gameSessionData = null
        };
    }

    public static GameSessionResult Failed(
        GameSessionOperationType operationType,
        GameSessionFailureType failureType,
        string failureMessage,
        string gameId = "",
        string lobbyId = "")
    {
        return new GameSessionResult
        {
            success = false,
            operationType = operationType,
            failureType = failureType,
            failureMessage = string.IsNullOrWhiteSpace(failureMessage)
                ? "The Game session operation failed."
                : failureMessage,
            gameId = gameId ?? string.Empty,
            lobbyId = lobbyId ?? string.Empty,
            gameSessionData = null
        };
    }
}

[Serializable]
public class GameFinalScoreResultData
{
    public string resultId;
    public string userId;
    public ScorePlayMode playMode;
    public BingoGameModeType gameModeType;
    public int scoreDelta;

    public GameFinalScoreResultData()
    {
        resultId = string.Empty;
        userId = string.Empty;
    }
}
