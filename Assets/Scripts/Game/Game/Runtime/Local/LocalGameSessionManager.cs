using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class LocalGameSessionManager : MonoBehaviour, IGameSessionService
{
    public const string GameIdPrefix = "local_";

    public static LocalGameSessionManager instance;

    private readonly List<GameSessionData> gameSessions = new List<GameSessionData>();
    private bool isReady;

    public SessionRuntimeType RuntimeType => SessionRuntimeType.Local;
    public bool IsReady => isReady;
    public IReadOnlyList<GameSessionData> GameSessions => gameSessions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        isReady = false;
    }

    private IEnumerator Start()
    {
        while (GameModeManager.instance == null || !GameModeManager.instance.IsReady)
        {
            yield return null;
        }

        isReady = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public GameSessionResult CreateGame(GameSessionSetupData setupData)
    {
        if (!isReady)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Create,
                GameSessionFailureType.ServiceUnavailable,
                "The local Game session manager is not ready.",
                lobbyId: setupData?.lobbyId);
        }

        string failureMessage = string.Empty;

        if (setupData == null || setupData.runtimeType != SessionRuntimeType.Local || !setupData.IsValid(out failureMessage))
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Create,
                GameSessionFailureType.InvalidSetupData,
                string.IsNullOrWhiteSpace(failureMessage) ? "The local Game setup data is invalid." : failureMessage,
                lobbyId: setupData?.lobbyId);
        }

        GameSessionData existingSession = FindGameByLobbyId(setupData.lobbyId);
        string gameId = existingSession != null
            ? existingSession.gameId
            : GameIdPrefix + Guid.NewGuid().ToString("N");

        GameSessionData gameSessionData = new GameSessionData(gameId, setupData);

        if (existingSession != null)
        {
            gameSessionData.revision = existingSession.revision + 1;
        }

        if (existingSession != null)
        {
            int existingIndex = gameSessions.IndexOf(existingSession);
            gameSessions[existingIndex] = gameSessionData;
        }
        else
        {
            gameSessions.Add(gameSessionData);
        }

        return GameSessionResult.Succeeded(GameSessionOperationType.Create, gameSessionData);
    }

    public Task<GameSessionResult> RejoinGameAsync(string gameId, UserData userData)
    {
        if (!isReady)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.ServiceUnavailable,
                "The local Game session manager is not ready.",
                gameId));
        }

        if (userData == null || !userData.HasUser)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.PlayerNotFound,
                "The current player could not be resolved.",
                gameId));
        }

        GameSessionData gameSessionData = FindGame(gameId);

        if (gameSessionData == null)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.GameNotFound,
                "The saved local Game no longer exists.",
                gameId));
        }

        GamePlayerData playerData = gameSessionData.GetPlayer(userData.userId);

        if (playerData == null)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.PlayerNotFound,
                "The player is not part of this Game.",
                gameId,
                gameSessionData.lobbyId));
        }

        if (!playerData.canRejoin || gameSessionData.gameState == GameSessionState.Completed)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.PlayerNotEligible,
                "The player can no longer return to this Game.",
                gameId,
                gameSessionData.lobbyId));
        }

        playerData.isConnected = true;
        playerData.isGameSceneReady = false;
        gameSessionData.revision++;
        return Task.FromResult(GameSessionResult.Succeeded(GameSessionOperationType.Rejoin, gameSessionData));
    }

    public Task<GameSessionResult> SyncGameSessionAsync(string gameId, string lobbyId, UserData userData)
    {
        if (!isReady)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.ServiceUnavailable,
                "The local Game session manager is not ready.",
                gameId,
                lobbyId));
        }

        if (userData == null || !userData.HasUser)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.PlayerNotFound,
                "The current player could not be resolved.",
                gameId,
                lobbyId));
        }

        GameSessionData gameSessionData = !string.IsNullOrWhiteSpace(gameId)
            ? FindGame(gameId)
            : FindGameByLobbyId(lobbyId);

        if (gameSessionData == null)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.GameNotFound,
                "The saved local Game no longer exists.",
                gameId,
                lobbyId));
        }

        GamePlayerData playerData = gameSessionData.GetPlayer(userData.userId);

        if (playerData == null || playerData.userTag == UserTag.Bot)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.PlayerNotFound,
                "The player is not part of this Game.",
                gameSessionData.gameId,
                gameSessionData.lobbyId));
        }

        return Task.FromResult(GameSessionResult.Succeeded(GameSessionOperationType.Sync, gameSessionData));
    }

    public Task<GameSessionResult> SetGameSceneReadyAsync(string gameId, UserData userData)
    {
        if (!isReady)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.ServiceUnavailable,
                "The local Game session manager is not ready.",
                gameId));
        }

        if (userData == null || !userData.HasUser)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.PlayerNotFound,
                "The current player could not be resolved.",
                gameId));
        }

        GameSessionData gameSessionData = FindGame(gameId);

        if (gameSessionData == null)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.GameNotFound,
                "The saved local Game no longer exists.",
                gameId));
        }

        GamePlayerData playerData = gameSessionData.GetPlayer(userData.userId);

        if (playerData == null || playerData.userTag == UserTag.Bot)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.PlayerNotFound,
                "The player is not part of this Game.",
                gameId,
                gameSessionData.lobbyId));
        }

        if (!playerData.canRejoin || gameSessionData.gameState == GameSessionState.Completed)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.PlayerNotEligible,
                "The player can no longer enter this Game scene.",
                gameId,
                gameSessionData.lobbyId));
        }

        if (!playerData.isConnected || !playerData.isGameSceneReady)
        {
            playerData.isConnected = true;
            playerData.isGameSceneReady = true;
            gameSessionData.revision++;
        }

        return Task.FromResult(GameSessionResult.Succeeded(GameSessionOperationType.SceneReady, gameSessionData));
    }

    public Task<GameSessionResult> LeaveGameAsync(string gameId, UserData userData)
    {
        if (!isReady)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.ServiceUnavailable,
                "The local Game session manager is not ready.",
                gameId));
        }

        if (userData == null || !userData.HasUser)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.PlayerNotFound,
                "The current player could not be resolved.",
                gameId));
        }

        GameSessionData gameSessionData = FindGame(gameId);

        if (gameSessionData == null)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.GameNotFound,
                "The saved local Game no longer exists.",
                gameId));
        }

        GamePlayerData playerData = gameSessionData.GetPlayer(userData.userId);

        if (playerData == null || playerData.userTag == UserTag.Bot)
        {
            return Task.FromResult(GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.PlayerNotFound,
                "The player is not part of this Game.",
                gameId,
                gameSessionData.lobbyId));
        }

        playerData.isConnected = false;
        playerData.isGameSceneReady = false;
        playerData.canRejoin = false;
        gameSessionData.RemovePlayer(userData.userId);
        gameSessionData.revision++;

        return Task.FromResult(GameSessionResult.Succeeded(GameSessionOperationType.Leave, gameSessionData));
    }

    public bool DeleteGame(string gameId)
    {
        GameSessionData gameSessionData = FindGame(gameId);
        return gameSessionData != null && gameSessions.Remove(gameSessionData);
    }

    public bool DeleteGameForLobby(string lobbyId)
    {
        GameSessionData gameSessionData = FindGameByLobbyId(lobbyId);
        return gameSessionData != null && gameSessions.Remove(gameSessionData);
    }

    public bool HasGameForLobby(string lobbyId)
    {
        return FindGameByLobbyId(lobbyId) != null;
    }

    public bool HasGame(string gameId)
    {
        return FindGame(gameId) != null;
    }

    private GameSessionData FindGame(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return null;
        }

        for (int i = 0; i < gameSessions.Count; i++)
        {
            GameSessionData gameSessionData = gameSessions[i];

            if (gameSessionData != null && string.Equals(gameSessionData.gameId, gameId, StringComparison.Ordinal))
            {
                return gameSessionData;
            }
        }

        return null;
    }

    private GameSessionData FindGameByLobbyId(string lobbyId)
    {
        if (string.IsNullOrWhiteSpace(lobbyId))
        {
            return null;
        }

        for (int i = 0; i < gameSessions.Count; i++)
        {
            GameSessionData gameSessionData = gameSessions[i];

            if (gameSessionData != null && string.Equals(gameSessionData.lobbyId, lobbyId, StringComparison.Ordinal))
            {
                return gameSessionData;
            }
        }

        return null;
    }
}
