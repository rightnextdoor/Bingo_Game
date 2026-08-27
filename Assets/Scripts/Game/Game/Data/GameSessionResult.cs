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

    public GameSessionResult()
    {
        success = false;
        operationType = GameSessionOperationType.None;
        failureType = GameSessionFailureType.Unknown;
        failureMessage = string.Empty;
        gameId = string.Empty;
        lobbyId = string.Empty;
        gameSessionData = null;
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
