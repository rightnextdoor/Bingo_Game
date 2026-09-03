using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkGameSessionService : MonoBehaviour, IGameSessionService
{
    public static NetworkGameSessionService instance;

    private const float GameConnectionTimeoutSeconds = 15f;

    private bool isReady;
    private NetworkBootstrap networkBootstrap;

    public SessionRuntimeType RuntimeType => SessionRuntimeType.Network;
    public bool IsReady => isReady;

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
        while (NetworkBootstrap.instance == null || !NetworkBootstrap.instance.IsReady)
        {
            yield return null;
        }

        networkBootstrap = NetworkBootstrap.instance;
        isReady = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public async Task<GameSessionResult> RejoinGameAsync(string gameId, UserData userData)
    {
        if (!isReady)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.ServiceUnavailable,
                "The network Game service is not ready.",
                gameId);
        }

        if (networkBootstrap == null || !networkBootstrap.IsConnected)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.NetworkConnectionFailed,
                "A network connection is required to rejoin this Game.",
                gameId);
        }

        NetworkGameSessionConnection connection = await WaitForLocalGameConnectionAsync();

        if (connection == null)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.NetworkGameConnectionUnavailable,
                "The network Game connection was not available.",
                gameId);
        }

        return await connection.RequestRejoinGameAsync(gameId);
    }

    public async Task<GameSessionResult> SetGameSceneReadyAsync(string gameId, UserData userData)
    {
        if (!isReady)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.ServiceUnavailable,
                "The network Game service is not ready.",
                gameId);
        }

        if (networkBootstrap == null || !networkBootstrap.IsConnected)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.NetworkConnectionFailed,
                "A network connection is required to enter this Game scene.",
                gameId);
        }

        NetworkGameSessionConnection connection = await WaitForLocalGameConnectionAsync();

        if (connection == null)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.NetworkGameConnectionUnavailable,
                "The network Game connection was not available.",
                gameId);
        }

        return await connection.RequestGameSceneReadyAsync(gameId);
    }

    public async Task<GameSessionResult> SyncGameSessionAsync(string gameId, string lobbyId, UserData userData)
    {
        if (!isReady)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.ServiceUnavailable,
                "The network Game service is not ready.",
                gameId,
                lobbyId);
        }

        if (networkBootstrap == null || !networkBootstrap.IsConnected)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.NetworkConnectionFailed,
                "A network connection is required to synchronize this Game.",
                gameId,
                lobbyId);
        }

        NetworkGameSessionConnection connection = await WaitForLocalGameConnectionAsync();

        if (connection == null)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.NetworkGameConnectionUnavailable,
                "The network Game connection was not available.",
                gameId,
                lobbyId);
        }

        return await connection.RequestGameSessionSyncAsync(gameId, lobbyId);
    }

    public async Task<GameSessionResult> LeaveGameAsync(string gameId, UserData userData)
    {
        if (!isReady)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.ServiceUnavailable,
                "The network Game service is not ready.",
                gameId);
        }

        if (networkBootstrap == null || !networkBootstrap.IsConnected)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.NetworkConnectionFailed,
                "A network connection is required to leave this Game session.",
                gameId);
        }

        NetworkGameSessionConnection connection = await WaitForLocalGameConnectionAsync();

        if (connection == null)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.NetworkGameConnectionUnavailable,
                "The network Game connection was not available.",
                gameId);
        }

        return await connection.RequestLeaveGameAsync(gameId);
    }

    public bool TrySetPlayerMarkedCell(
        string gameId,
        UserData userData,
        int cellIndex,
        bool isMarked,
        out GamePlayerMarkedCellChangedData updateData)
    {
        updateData = null;

        if (!isReady ||
            networkBootstrap == null ||
            !networkBootstrap.IsConnected ||
            userData == null ||
            !userData.HasUser ||
            string.IsNullOrWhiteSpace(gameId))
        {
            return false;
        }

        NetworkGameSessionConnection connection =
            NetworkGameSessionConnection.GetLocalConnection();

        return connection != null &&
               connection.RequestPlayerMarkedCell(gameId, cellIndex, isMarked);
    }

    public bool TrySubmitBingoCheck(
        string gameId,
        UserData userData,
        LobbyBoardData boardData,
        IReadOnlyList<int> markedCellIndices,
        out GameBingoCheckResolvedData resolvedData)
    {
        resolvedData = null;

        if (!isReady ||
            networkBootstrap == null ||
            !networkBootstrap.IsConnected ||
            userData == null ||
            !userData.HasUser ||
            string.IsNullOrWhiteSpace(gameId) ||
            boardData == null)
        {
            return false;
        }

        NetworkGameSessionConnection connection =
            NetworkGameSessionConnection.GetLocalConnection();

        return connection != null &&
               connection.RequestBingoCheck(gameId, boardData, markedCellIndices);
    }

    public bool TryCompleteBingoCheckAnimation(string gameId, UserData userData)
    {
        if (!isReady ||
            networkBootstrap == null ||
            !networkBootstrap.IsConnected ||
            userData == null ||
            !userData.HasUser ||
            string.IsNullOrWhiteSpace(gameId))
        {
            return false;
        }

        NetworkGameSessionConnection connection =
            NetworkGameSessionConnection.GetLocalConnection();

        return connection != null &&
               connection.RequestBingoCheckAnimationCompleted(gameId);
    }

    private async Task<NetworkGameSessionConnection> WaitForLocalGameConnectionAsync()
    {
        float timeoutTime = Time.realtimeSinceStartup + GameConnectionTimeoutSeconds;

        while (Time.realtimeSinceStartup < timeoutTime)
        {
            NetworkGameSessionConnection connection = NetworkGameSessionConnection.GetLocalConnection();

            if (connection != null)
            {
                return connection;
            }

            await Task.Yield();
        }

        return null;
    }
}
