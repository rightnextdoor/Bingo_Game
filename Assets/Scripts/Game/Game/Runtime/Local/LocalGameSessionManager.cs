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
    public static event Action<GameSessionData> LocalGameSessionUpdated;

    private readonly List<GameSessionData> gameSessions = new List<GameSessionData>();
    private bool isReady;

    public SessionRuntimeType RuntimeType => SessionRuntimeType.Local;
    public bool IsReady => isReady;
    public IReadOnlyList<GameSessionData> GameSessions => gameSessions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
        LocalGameSessionUpdated = null;
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

    private void Update()
    {
        if (!isReady)
        {
            return;
        }

        for (int i = 0; i < gameSessions.Count; i++)
        {
            GameSessionData gameSessionData = gameSessions[i];

            if (!GameBingoCheckAuthority.UpdateSessionLoop(gameSessionData))
            {
                continue;
            }

            GameScoreAuthority.PersistFinalizedLocalScores(gameSessionData);
            gameSessionData.revision++;
            LocalGameSessionUpdated?.Invoke(new GameSessionData(gameSessionData));
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

        bool sessionChanged = false;

        if (!playerData.isConnected || !playerData.isGameSceneReady)
        {
            playerData.isConnected = true;
            playerData.isGameSceneReady = true;
            sessionChanged = true;
        }

        if (gameSessionData.gamePlayController != null &&
            gameSessionData.gamePlayController.TryStartFirstBallCountdown())
        {
            gameSessionData.gameState = GameSessionState.InProgress;
            sessionChanged = true;
        }

        if (sessionChanged)
        {
            gameSessionData.revision++;
            LocalGameSessionUpdated?.Invoke(new GameSessionData(gameSessionData));
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

    public bool RemovePlayerFromAnyGame(string userId)
    {
        GameSessionData gameSessionData = FindGameByPlayerId(userId);
        GamePlayerData playerData = gameSessionData?.GetPlayer(userId);

        if (playerData == null || playerData.userTag == UserTag.Bot)
        {
            return false;
        }

        playerData.isConnected = false;
        playerData.isGameSceneReady = false;
        playerData.canRejoin = false;
        gameSessionData.RemovePlayer(userId);
        gameSessionData.revision++;
        LocalGameSessionUpdated?.Invoke(new GameSessionData(gameSessionData));
        return true;
    }

    public bool TrySetPlayerMarkedCell(
        string gameId,
        UserData userData,
        int cellIndex,
        bool isMarked,
        out GamePlayerMarkedCellChangedData updateData)
    {
        updateData = null;

        if (!isReady || userData == null || !userData.HasUser)
        {
            return false;
        }

        GameSessionData gameSessionData = FindGame(gameId);
        GamePlayerData playerData = gameSessionData?.GetPlayer(userData.userId);
        LobbyBoardData boardData = playerData?.boardData;

        if (playerData == null ||
            playerData.userTag == UserTag.Bot ||
            !playerData.isConnected ||
            !playerData.canRejoin ||
            gameSessionData.gameState == GameSessionState.Completed ||
            boardData?.cellNumbers == null ||
            cellIndex < 0 ||
            cellIndex >= boardData.cellNumbers.Count ||
            (boardData.usesFreeCell && cellIndex == 12 && !isMarked))
        {
            return false;
        }

        updateData = new GamePlayerMarkedCellChangedData(
            gameSessionData.gameId,
            playerData.userId,
            cellIndex,
            isMarked);
        return true;
    }

    public bool TrySubmitBingoCheck(
        string gameId,
        UserData userData,
        LobbyBoardData boardData,
        IReadOnlyList<int> markedCellIndices,
        out GameBingoCheckResolvedData resolvedData)
    {
        resolvedData = null;

        if (!isReady || userData == null || !userData.HasUser)
        {
            return false;
        }

        GameSessionData gameSessionData = FindGame(gameId);

        if (gameSessionData == null)
        {
            resolvedData = GameBingoCheckResolvedData.Rejected(
                gameId,
                userData.userId,
                0,
                "The local Game could not be found.");
            return true;
        }

        resolvedData = GameBingoCheckAuthority.ProcessCheck(
            gameSessionData,
            userData.userId,
            new GameBingoCheckRequestData(boardData, markedCellIndices));

        if (!resolvedData.wasAccepted)
        {
            return true;
        }

        GameScoreAuthority.PersistFinalizedLocalScores(gameSessionData);
        gameSessionData.revision++;
        resolvedData.revision = gameSessionData.revision;
        resolvedData.matchCompleted = gameSessionData.gameState == GameSessionState.Completed;
        LocalGameSessionUpdated?.Invoke(new GameSessionData(gameSessionData));
        return true;
    }

    public bool TryCompleteBingoCheckAnimation(string gameId, UserData userData)
    {
        if (!isReady || userData == null || !userData.HasUser)
        {
            return false;
        }

        GameSessionData gameSessionData = FindGame(gameId);
        GamePlayerData playerData = gameSessionData?.GetPlayer(userData.userId);

        if (gameSessionData == null ||
            playerData == null)
        {
            return false;
        }

        if (GameBingoCheckAuthority.CompleteBingoCheckAnimation(
                gameSessionData,
                userData.userId))
        {
            GameScoreAuthority.PersistFinalizedLocalScores(gameSessionData);
            gameSessionData.revision++;
            LocalGameSessionUpdated?.Invoke(new GameSessionData(gameSessionData));
            return true;
        }

        return false;
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

    public void ResetForFreshApplicationStart()
    {
        gameSessions.Clear();
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

    private GameSessionData FindGameByPlayerId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        for (int i = 0; i < gameSessions.Count; i++)
        {
            GameSessionData gameSessionData = gameSessions[i];

            if (gameSessionData?.GetPlayer(userId) != null)
            {
                return gameSessionData;
            }
        }

        return null;
    }
}
