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

        GameSessionData gameSessionData = new GameSessionData(gameId, setupData);

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

        playerData.isConnected = true;
        return GameSessionResult.Succeeded(GameSessionOperationType.Rejoin, gameSessionData);
    }

    public bool DeleteGame(string gameId, bool notifyPlayers = true)
    {
        GameSessionData gameSessionData = FindGame(gameId);

        if (gameSessionData == null || !gameSessions.Remove(gameSessionData))
        {
            return false;
        }

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
