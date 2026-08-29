using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkGameSessionManager : MonoBehaviour
{
    public const string GameIdPrefix = "network_";

    public static NetworkGameSessionManager instance;

    private readonly List<GameSessionData> gameSessions = new List<GameSessionData>();

    private bool isReady;
    private NetworkRoot networkRoot;
    private NetworkBootstrap networkBootstrap;
    private NetworkConnectionRegistry connectionRegistry;

    public bool IsReady => isReady;
    public IReadOnlyList<GameSessionData> GameSessions => gameSessions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        networkRoot = GetComponentInParent<NetworkRoot>();

        if (networkRoot == null || !networkRoot.IsPrimaryInstance)
        {
            enabled = false;
            return;
        }

        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }

        instance = this;
        isReady = false;
    }

    private IEnumerator Start()
    {
        while (!CanInitialize())
        {
            yield return null;
        }

        networkRoot = NetworkRoot.instance;
        networkBootstrap = NetworkBootstrap.instance;
        connectionRegistry = NetworkConnectionRegistry.instance;
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
        if (!CanProcessAuthorityOperation())
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Create,
                GameSessionFailureType.ServiceUnavailable,
                "The authoritative network Game session manager is not ready.",
                lobbyId: setupData?.lobbyId);
        }

        string failureMessage = string.Empty;

        if (setupData == null || setupData.runtimeType != SessionRuntimeType.Network || !setupData.IsValid(out failureMessage))
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Create,
                GameSessionFailureType.InvalidSetupData,
                string.IsNullOrWhiteSpace(failureMessage) ? "The network Game setup data is invalid." : failureMessage,
                lobbyId: setupData?.lobbyId);
        }

        GameSessionData existingSession = FindGameByLobbyId(setupData.lobbyId);
        string gameId = existingSession != null
            ? existingSession.gameId
            : GameIdPrefix + Guid.NewGuid().ToString("N");

        if (existingSession != null)
        {
            MultiplayerNetworkScheduler.instance?.ClearSession(gameId);
        }

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

    public GameSessionResult ProcessAuthorityRejoin(ulong clientId, string gameId)
    {
        if (!CanProcessAuthorityOperation())
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.ServiceUnavailable,
                "The authoritative network Game session manager is not ready.",
                gameId);
        }

        if (!connectionRegistry.TryGetBingoUserId(clientId, out string userId))
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.PlayerNotFound,
                "The connected player could not be resolved.",
                gameId);
        }

        GameSessionData gameSessionData = FindGame(gameId);

        if (gameSessionData == null)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.GameNotFound,
                "The saved network Game no longer exists.",
                gameId);
        }

        GamePlayerData playerData = gameSessionData.GetPlayer(userId);

        if (playerData == null)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.PlayerNotFound,
                "The player is not part of this Game.",
                gameId,
                gameSessionData.lobbyId);
        }

        if (!playerData.canRejoin || gameSessionData.gameState == GameSessionState.Completed)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.PlayerNotEligible,
                "The player can no longer return to this Game.",
                gameId,
                gameSessionData.lobbyId);
        }

        if (!playerData.isConnected || playerData.isGameSceneReady)
        {
            playerData.isConnected = true;
            playerData.isGameSceneReady = false;
            gameSessionData.revision++;
            BroadcastGamePlayerStateChanged(gameSessionData, playerData);
        }

        return GameSessionResult.Succeeded(GameSessionOperationType.Rejoin, gameSessionData);
    }

    public GameSessionResult ProcessAuthorityGameSessionSync(ulong clientId, string gameId, string lobbyId)
    {
        if (!CanProcessAuthorityOperation())
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.ServiceUnavailable,
                "The authoritative network Game session manager is not ready.",
                gameId,
                lobbyId);
        }

        if (!connectionRegistry.TryGetBingoUserId(clientId, out string userId))
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.PlayerNotFound,
                "The connected player could not be resolved.",
                gameId,
                lobbyId);
        }

        GameSessionData gameSessionData = !string.IsNullOrWhiteSpace(gameId)
            ? FindGame(gameId)
            : FindGameByLobbyId(lobbyId);

        if (gameSessionData == null)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.GameNotFound,
                "The saved network Game no longer exists.",
                gameId,
                lobbyId);
        }

        GamePlayerData playerData = gameSessionData.GetPlayer(userId);

        if (playerData == null || playerData.userTag == UserTag.Bot)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.PlayerNotFound,
                "The player is not part of this Game.",
                gameSessionData.gameId,
                gameSessionData.lobbyId);
        }

        return GameSessionResult.Succeeded(GameSessionOperationType.Sync, gameSessionData);
    }

    public GameSessionResult ProcessAuthorityGameSceneReady(ulong clientId, string gameId)
    {
        if (!CanProcessAuthorityOperation())
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.ServiceUnavailable,
                "The authoritative network Game session manager is not ready.",
                gameId);
        }

        if (!connectionRegistry.TryGetBingoUserId(clientId, out string userId))
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.PlayerNotFound,
                "The connected player could not be resolved.",
                gameId);
        }

        GameSessionData gameSessionData = FindGame(gameId);

        if (gameSessionData == null)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.GameNotFound,
                "The saved network Game no longer exists.",
                gameId);
        }

        GamePlayerData playerData = gameSessionData.GetPlayer(userId);

        if (playerData == null || playerData.userTag == UserTag.Bot)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.PlayerNotFound,
                "The player is not part of this Game.",
                gameId,
                gameSessionData.lobbyId);
        }

        if (!playerData.canRejoin || gameSessionData.gameState == GameSessionState.Completed)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.PlayerNotEligible,
                "The player can no longer enter this Game scene.",
                gameId,
                gameSessionData.lobbyId);
        }

        if (!playerData.isConnected || !playerData.isGameSceneReady)
        {
            playerData.isConnected = true;
            playerData.isGameSceneReady = true;
            gameSessionData.revision++;
            BroadcastGamePlayerStateChanged(gameSessionData, playerData);
        }

        return GameSessionResult.Acknowledged(GameSessionOperationType.SceneReady, gameSessionData);
    }

    public GameSessionResult ProcessAuthorityLeave(ulong clientId, string gameId)
    {
        if (!CanProcessAuthorityOperation())
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.ServiceUnavailable,
                "The authoritative network Game session manager is not ready.",
                gameId);
        }

        if (!connectionRegistry.TryGetBingoUserId(clientId, out string userId))
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.PlayerNotFound,
                "The connected player could not be resolved.",
                gameId);
        }

        GameSessionData gameSessionData = FindGame(gameId);

        if (gameSessionData == null)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.GameNotFound,
                "The saved network Game no longer exists.",
                gameId);
        }

        GamePlayerData playerData = gameSessionData.GetPlayer(userId);

        if (playerData == null || playerData.userTag == UserTag.Bot)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.PlayerNotFound,
                "The player is not part of this Game.",
                gameId,
                gameSessionData.lobbyId);
        }

        playerData.isConnected = false;
        playerData.isGameSceneReady = false;
        playerData.canRejoin = false;
        gameSessionData.RemovePlayer(userId);
        gameSessionData.revision++;
        BroadcastGamePlayerLeft(gameSessionData, userId);

        return GameSessionResult.Acknowledged(GameSessionOperationType.Leave, gameSessionData);
    }

    public void ProcessAuthorityPlayerMarkedCell(
        ulong clientId,
        string gameId,
        int cellIndex,
        bool isMarked)
    {
        if (!CanProcessAuthorityOperation() ||
            !connectionRegistry.TryGetBingoUserId(clientId, out string userId))
        {
            return;
        }

        GameSessionData gameSessionData = FindGame(gameId);
        GamePlayerData playerData = gameSessionData?.GetPlayer(userId);
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
            return;
        }

        BroadcastGamePlayerMarkedCellChanged(
            gameSessionData,
            new GamePlayerMarkedCellChangedData(
                gameSessionData.gameId,
                userId,
                cellIndex,
                isMarked));
    }

    public bool DeleteGame(string gameId, bool notifyPlayers = true)
    {
        GameSessionData gameSessionData = FindGame(gameId);

        if (gameSessionData == null || !gameSessions.Remove(gameSessionData))
        {
            return false;
        }

        MultiplayerNetworkScheduler.instance?.ClearSession(gameSessionData.gameId);

        if (notifyPlayers)
        {
            NotifyGameDeleted(gameSessionData);
        }

        return true;
    }

    public bool DeleteGameForLobby(string lobbyId, bool notifyPlayers = true)
    {
        GameSessionData gameSessionData = FindGameByLobbyId(lobbyId);
        return gameSessionData != null && DeleteGame(gameSessionData.gameId, notifyPlayers);
    }

    public bool HasGameForLobby(string lobbyId)
    {
        return FindGameByLobbyId(lobbyId) != null;
    }

    private void NotifyGameDeleted(GameSessionData gameSessionData)
    {
        if (gameSessionData?.players == null || connectionRegistry == null || !connectionRegistry.IsReady)
        {
            return;
        }

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null || playerData.userTag == UserTag.Bot ||
                !connectionRegistry.TryGetClientId(playerData.userId, out ulong clientId))
            {
                continue;
            }

            NetworkGameSessionConnection.TrySendGameDeleted(clientId, gameSessionData.gameId);
        }
    }

    private void BroadcastGamePlayerStateChanged(GameSessionData gameSessionData, GamePlayerData changedPlayerData)
    {
        if (gameSessionData?.players == null || changedPlayerData == null ||
            connectionRegistry == null || !connectionRegistry.IsReady)
        {
            return;
        }

        GamePlayerStateChangedData updateData = new GamePlayerStateChangedData(gameSessionData, changedPlayerData);

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null || playerData.userTag == UserTag.Bot ||
                !connectionRegistry.TryGetClientId(playerData.userId, out ulong clientId))
            {
                continue;
            }

            NetworkGameSessionConnection.TrySendGamePlayerStateChanged(clientId, updateData);
        }
    }

    private void BroadcastGamePlayerMarkedCellChanged(
        GameSessionData gameSessionData,
        GamePlayerMarkedCellChangedData updateData)
    {
        if (gameSessionData?.players == null || updateData == null ||
            connectionRegistry == null || !connectionRegistry.IsReady)
        {
            return;
        }

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null || playerData.userTag == UserTag.Bot ||
                !connectionRegistry.TryGetClientId(playerData.userId, out ulong clientId))
            {
                continue;
            }

            NetworkGameSessionConnection.TrySendGamePlayerMarkedCellChanged(
                clientId,
                updateData);
        }
    }

    private void BroadcastGamePlayerLeft(GameSessionData gameSessionData, string userId)
    {
        if (gameSessionData?.players == null || string.IsNullOrWhiteSpace(userId) ||
            connectionRegistry == null || !connectionRegistry.IsReady)
        {
            return;
        }

        GamePlayerLeftData updateData = new GamePlayerLeftData(gameSessionData, userId);

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData == null || playerData.userTag == UserTag.Bot ||
                !connectionRegistry.TryGetClientId(playerData.userId, out ulong clientId))
            {
                continue;
            }

            NetworkGameSessionConnection.TrySendGamePlayerLeft(clientId, updateData);
        }
    }

    private bool CanInitialize()
    {
        return NetworkRoot.instance != null && NetworkRoot.instance.IsReady &&
               NetworkBootstrap.instance != null && NetworkBootstrap.instance.IsReady &&
               NetworkConnectionRegistry.instance != null && NetworkConnectionRegistry.instance.IsReady;
    }

    private bool CanProcessAuthorityOperation()
    {
        return isReady && networkBootstrap != null && networkBootstrap.IsAuthority &&
               connectionRegistry != null && connectionRegistry.IsReady;
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
