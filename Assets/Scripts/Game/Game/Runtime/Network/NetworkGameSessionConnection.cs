using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkSessionPlayer))]
public class NetworkGameSessionConnection : NetworkBehaviour
{
    public static NetworkGameSessionConnection local;

    private const float RequestTimeoutSeconds = 30f;

    private readonly Dictionary<string, TaskCompletionSource<GameSessionResult>> pendingRejoinRequests =
        new Dictionary<string, TaskCompletionSource<GameSessionResult>>();
    private readonly Dictionary<string, TaskCompletionSource<GameSessionResult>> pendingSceneReadyRequests =
        new Dictionary<string, TaskCompletionSource<GameSessionResult>>();
    private readonly Dictionary<string, TaskCompletionSource<GameSessionResult>> pendingSyncRequests =
        new Dictionary<string, TaskCompletionSource<GameSessionResult>>();
    private readonly Dictionary<string, TaskCompletionSource<GameSessionResult>> pendingLeaveRequests =
        new Dictionary<string, TaskCompletionSource<GameSessionResult>>();

    public static event Action<GameSessionResult> LocalGameCreationResultReceived;
    public static event Action<GameSessionData> LocalGameSessionUpdatedReceived;
    public static event Action<GamePlayerStateChangedData> LocalGamePlayerStateChangedReceived;
    public static event Action<GamePlayerMarkedCellChangedData> LocalGamePlayerMarkedCellChangedReceived;
    public static event Action<GamePlayerLeftData> LocalGamePlayerLeftReceived;
    public static event Action<string> LocalGameDeletedReceived;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        local = null;
        LocalGameCreationResultReceived = null;
        LocalGameSessionUpdatedReceived = null;
        LocalGamePlayerStateChangedReceived = null;
        LocalGamePlayerMarkedCellChangedReceived = null;
        LocalGamePlayerLeftReceived = null;
        LocalGameDeletedReceived = null;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            local = this;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (local == this)
        {
            local = null;
        }

        CompletePendingRequestsAsFailed();
        base.OnNetworkDespawn();
    }

    public static NetworkGameSessionConnection GetLocalConnection()
    {
        if (local != null && local.IsSpawned)
        {
            return local;
        }

        NetworkSessionPlayer sessionPlayer = NetworkSessionPlayer.GetLocalPlayer();

        if (sessionPlayer == null)
        {
            return null;
        }

        NetworkGameSessionConnection connection = sessionPlayer.GetComponent<NetworkGameSessionConnection>();

        if (connection == null || !connection.IsSpawned)
        {
            return null;
        }

        local = connection;
        return local;
    }

    private static bool TryGetServerConnection(ulong clientId, out NetworkGameSessionConnection connection)
    {
        connection = null;

        if (!NetworkSessionPlayer.TryGetServerPlayer(clientId, out NetworkSessionPlayer sessionPlayer))
        {
            return false;
        }

        connection = sessionPlayer.GetComponent<NetworkGameSessionConnection>();
        return connection != null && connection.IsSpawned && connection.IsServer;
    }

    public async Task<GameSessionResult> RequestRejoinGameAsync(string gameId)
    {
        if (!IsSpawned || !IsOwner)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.NetworkGameConnectionUnavailable,
                "The local network Game connection is not ready.",
                gameId);
        }

        if (string.IsNullOrWhiteSpace(gameId))
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.GameNotFound,
                "The saved GameId is missing.");
        }

        string requestId = Guid.NewGuid().ToString("N");
        TaskCompletionSource<GameSessionResult> completionSource = new TaskCompletionSource<GameSessionResult>();

        pendingRejoinRequests.Add(requestId, completionSource);
        RequestRejoinGameRpc(requestId, gameId);

        float timeoutTime = Time.realtimeSinceStartup + RequestTimeoutSeconds;

        while (!completionSource.Task.IsCompleted && Time.realtimeSinceStartup < timeoutTime)
        {
            await Task.Yield();
        }

        pendingRejoinRequests.Remove(requestId);

        if (!completionSource.Task.IsCompleted)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.NetworkConnectionFailed,
                "The network Game rejoin request timed out.",
                gameId);
        }

        return await completionSource.Task;
    }

    public async Task<GameSessionResult> RequestGameSceneReadyAsync(string gameId)
    {
        if (!IsSpawned || !IsOwner)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.NetworkGameConnectionUnavailable,
                "The local network Game connection is not ready.",
                gameId);
        }

        if (string.IsNullOrWhiteSpace(gameId))
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.GameNotFound,
                "The current GameId is missing.");
        }

        string requestId = Guid.NewGuid().ToString("N");
        TaskCompletionSource<GameSessionResult> completionSource = new TaskCompletionSource<GameSessionResult>();

        pendingSceneReadyRequests.Add(requestId, completionSource);
        RequestGameSceneReadyRpc(requestId, gameId);

        float timeoutTime = Time.realtimeSinceStartup + RequestTimeoutSeconds;

        while (!completionSource.Task.IsCompleted && Time.realtimeSinceStartup < timeoutTime)
        {
            await Task.Yield();
        }

        pendingSceneReadyRequests.Remove(requestId);

        if (!completionSource.Task.IsCompleted)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.NetworkConnectionFailed,
                "The Game scene-ready request timed out.",
                gameId);
        }

        return await completionSource.Task;
    }

    public async Task<GameSessionResult> RequestGameSessionSyncAsync(string gameId, string lobbyId)
    {
        if (!IsSpawned || !IsOwner)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.NetworkGameConnectionUnavailable,
                "The local network Game connection is not ready.",
                gameId,
                lobbyId);
        }

        if (string.IsNullOrWhiteSpace(gameId) && string.IsNullOrWhiteSpace(lobbyId))
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.GameNotFound,
                "The current GameId and LobbyId are missing.");
        }

        string requestId = Guid.NewGuid().ToString("N");
        TaskCompletionSource<GameSessionResult> completionSource = new TaskCompletionSource<GameSessionResult>();

        pendingSyncRequests.Add(requestId, completionSource);
        RequestGameSessionSyncRpc(requestId, gameId ?? string.Empty, lobbyId ?? string.Empty);

        float timeoutTime = Time.realtimeSinceStartup + RequestTimeoutSeconds;

        while (!completionSource.Task.IsCompleted && Time.realtimeSinceStartup < timeoutTime)
        {
            await Task.Yield();
        }

        pendingSyncRequests.Remove(requestId);

        if (!completionSource.Task.IsCompleted)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.NetworkConnectionFailed,
                "The network Game synchronization request timed out.",
                gameId,
                lobbyId);
        }

        return await completionSource.Task;
    }

    public async Task<GameSessionResult> RequestLeaveGameAsync(string gameId)
    {
        if (!IsSpawned || !IsOwner)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.NetworkGameConnectionUnavailable,
                "The local network Game connection is not ready.",
                gameId);
        }

        if (string.IsNullOrWhiteSpace(gameId))
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.GameNotFound,
                "The current GameId is missing.");
        }

        string requestId = Guid.NewGuid().ToString("N");
        TaskCompletionSource<GameSessionResult> completionSource = new TaskCompletionSource<GameSessionResult>();

        pendingLeaveRequests.Add(requestId, completionSource);
        RequestLeaveGameRpc(requestId, gameId);

        float timeoutTime = Time.realtimeSinceStartup + RequestTimeoutSeconds;

        while (!completionSource.Task.IsCompleted && Time.realtimeSinceStartup < timeoutTime)
        {
            await Task.Yield();
        }

        pendingLeaveRequests.Remove(requestId);

        if (!completionSource.Task.IsCompleted)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.NetworkConnectionFailed,
                "The network Game leave request timed out.",
                gameId);
        }

        return await completionSource.Task;
    }

    public bool RequestPlayerMarkedCell(
        string gameId,
        int cellIndex,
        bool isMarked)
    {
        if (!IsSpawned || !IsOwner || string.IsNullOrWhiteSpace(gameId))
        {
            return false;
        }

        RequestPlayerMarkedCellRpc(gameId, cellIndex, isMarked);
        return true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestRejoinGameRpc(string requestId, string gameId, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        GameSessionResult result;

        if (NetworkGameSessionManager.instance == null || !NetworkGameSessionManager.instance.IsReady)
        {
            result = GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.ServiceUnavailable,
                "The authoritative network Game session manager is not ready.",
                gameId);
        }
        else
        {
            result = NetworkGameSessionManager.instance.ProcessAuthorityRejoin(senderClientId, gameId);
        }

        if (TryGetServerConnection(senderClientId, out NetworkGameSessionConnection connection))
        {
            string resultJson = JsonUtility.ToJson(result);
            ScheduleAuthoritySend(
                result?.gameId ?? gameId,
                resultJson,
                MultiplayerNetworkPriority.Critical,
                MultiplayerNetworkWorkType.Event,
                string.Empty,
                () => TrySend(connection, () => connection.ReceiveRejoinGameResultRpc(
                    requestId,
                    resultJson,
                    connection.RpcTarget.Single(senderClientId, RpcTargetUse.Temp))));
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestGameSceneReadyRpc(string requestId, string gameId, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        GameSessionResult result;

        if (NetworkGameSessionManager.instance == null || !NetworkGameSessionManager.instance.IsReady)
        {
            result = GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.ServiceUnavailable,
                "The authoritative network Game session manager is not ready.",
                gameId);
        }
        else
        {
            result = NetworkGameSessionManager.instance.ProcessAuthorityGameSceneReady(senderClientId, gameId);
        }

        if (TryGetServerConnection(senderClientId, out NetworkGameSessionConnection connection))
        {
            string resultJson = JsonUtility.ToJson(result);
            ScheduleAuthoritySend(
                result?.gameId ?? gameId,
                resultJson,
                MultiplayerNetworkPriority.Critical,
                MultiplayerNetworkWorkType.Event,
                string.Empty,
                () => TrySend(connection, () => connection.ReceiveGameSceneReadyResultRpc(
                    requestId,
                    resultJson,
                    connection.RpcTarget.Single(senderClientId, RpcTargetUse.Temp))));
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestGameSessionSyncRpc(string requestId, string gameId, string lobbyId, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        GameSessionResult result;

        if (NetworkGameSessionManager.instance == null || !NetworkGameSessionManager.instance.IsReady)
        {
            result = GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.ServiceUnavailable,
                "The authoritative network Game session manager is not ready.",
                gameId,
                lobbyId);
        }
        else
        {
            result = NetworkGameSessionManager.instance.ProcessAuthorityGameSessionSync(senderClientId, gameId, lobbyId);
        }

        if (TryGetServerConnection(senderClientId, out NetworkGameSessionConnection connection))
        {
            string resultJson = JsonUtility.ToJson(result);
            ScheduleAuthoritySend(
                result?.gameId ?? gameId,
                resultJson,
                MultiplayerNetworkPriority.Critical,
                MultiplayerNetworkWorkType.Event,
                string.Empty,
                () => TrySend(connection, () => connection.ReceiveGameSessionSyncResultRpc(
                    requestId,
                    resultJson,
                    connection.RpcTarget.Single(senderClientId, RpcTargetUse.Temp))));
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestLeaveGameRpc(string requestId, string gameId, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        GameSessionResult result;

        if (NetworkGameSessionManager.instance == null || !NetworkGameSessionManager.instance.IsReady)
        {
            result = GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.ServiceUnavailable,
                "The authoritative network Game session manager is not ready.",
                gameId);
        }
        else
        {
            result = NetworkGameSessionManager.instance.ProcessAuthorityLeave(senderClientId, gameId);
        }

        if (TryGetServerConnection(senderClientId, out NetworkGameSessionConnection connection))
        {
            string resultJson = JsonUtility.ToJson(result);
            ScheduleAuthoritySend(
                result?.gameId ?? gameId,
                resultJson,
                MultiplayerNetworkPriority.Critical,
                MultiplayerNetworkWorkType.Event,
                string.Empty,
                () => TrySend(connection, () => connection.ReceiveLeaveGameResultRpc(
                    requestId,
                    resultJson,
                    connection.RpcTarget.Single(senderClientId, RpcTargetUse.Temp))));
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestPlayerMarkedCellRpc(
        string gameId,
        int cellIndex,
        bool isMarked,
        RpcParams rpcParams = default)
    {
        NetworkGameSessionManager.instance?.ProcessAuthorityPlayerMarkedCell(
            rpcParams.Receive.SenderClientId,
            gameId,
            cellIndex,
            isMarked);
    }

    public static bool TrySendGameCreationResult(ulong clientId, GameSessionResult result)
    {
        if (result == null || !TryGetServerConnection(clientId, out NetworkGameSessionConnection connection))
        {
            return false;
        }

        string resultJson = JsonUtility.ToJson(result);
        string sessionId = !string.IsNullOrWhiteSpace(result.gameId) ? result.gameId : result.lobbyId;

        return ScheduleAuthoritySend(
            sessionId,
            resultJson,
            MultiplayerNetworkPriority.Critical,
            MultiplayerNetworkWorkType.Event,
            string.Empty,
            () => TrySend(connection, () => connection.ReceiveGameCreationResultRpc(
                resultJson,
                connection.RpcTarget.Single(clientId, RpcTargetUse.Temp))));
    }

    public static bool TrySendGameSessionUpdated(ulong clientId, GameSessionData gameSessionData)
    {
        if (gameSessionData == null || !TryGetServerConnection(clientId, out NetworkGameSessionConnection connection))
        {
            return false;
        }

        string gameSessionJson = JsonUtility.ToJson(gameSessionData);

        return ScheduleAuthoritySend(
            gameSessionData.gameId,
            gameSessionJson,
            MultiplayerNetworkPriority.High,
            MultiplayerNetworkWorkType.State,
            $"game-session:{clientId}",
            () => TrySend(connection, () => connection.ReceiveGameSessionUpdatedRpc(
                gameSessionJson,
                connection.RpcTarget.Single(clientId, RpcTargetUse.Temp))));
    }

    public static bool TrySendGamePlayerStateChanged(ulong clientId, GamePlayerStateChangedData updateData)
    {
        if (updateData == null || !TryGetServerConnection(clientId, out NetworkGameSessionConnection connection))
        {
            return false;
        }

        string updateJson = JsonUtility.ToJson(updateData);

        return ScheduleAuthoritySend(
            updateData.gameId,
            updateJson,
            MultiplayerNetworkPriority.High,
            MultiplayerNetworkWorkType.Event,
            string.Empty,
            () => TrySend(connection, () => connection.ReceiveGamePlayerStateChangedRpc(
                updateJson,
                connection.RpcTarget.Single(clientId, RpcTargetUse.Temp))));
    }

    public static bool TrySendGamePlayerMarkedCellChanged(
        ulong clientId,
        GamePlayerMarkedCellChangedData updateData)
    {
        if (updateData == null ||
            !TryGetServerConnection(clientId, out NetworkGameSessionConnection connection))
        {
            return false;
        }

        string updateJson = JsonUtility.ToJson(updateData);

        return ScheduleAuthoritySend(
            updateData.gameId,
            updateJson,
            MultiplayerNetworkPriority.Normal,
            MultiplayerNetworkWorkType.Event,
            string.Empty,
            () => TrySend(connection, () => connection.ReceiveGamePlayerMarkedCellChangedRpc(
                updateJson,
                connection.RpcTarget.Single(clientId, RpcTargetUse.Temp))));
    }

    public static bool TrySendGamePlayerLeft(ulong clientId, GamePlayerLeftData updateData)
    {
        if (updateData == null || !TryGetServerConnection(clientId, out NetworkGameSessionConnection connection))
        {
            return false;
        }

        string updateJson = JsonUtility.ToJson(updateData);

        return ScheduleAuthoritySend(
            updateData.gameId,
            updateJson,
            MultiplayerNetworkPriority.High,
            MultiplayerNetworkWorkType.Event,
            string.Empty,
            () => TrySend(connection, () => connection.ReceiveGamePlayerLeftRpc(
                updateJson,
                connection.RpcTarget.Single(clientId, RpcTargetUse.Temp))));
    }

    public static bool TrySendGameDeleted(ulong clientId, string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId) || !TryGetServerConnection(clientId, out NetworkGameSessionConnection connection))
        {
            return false;
        }

        return ScheduleAuthoritySend(
            gameId,
            gameId.Length,
            MultiplayerNetworkPriority.Critical,
            MultiplayerNetworkWorkType.Event,
            string.Empty,
            () => TrySend(connection, () => connection.ReceiveGameDeletedRpc(
                gameId,
                connection.RpcTarget.Single(clientId, RpcTargetUse.Temp))));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveGameCreationResultRpc(string resultJson, RpcParams rpcParams = default)
    {
        GameSessionResult result = DeserializeResult(resultJson, GameSessionOperationType.Create);
        LocalGameCreationResultReceived?.Invoke(result);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveRejoinGameResultRpc(string requestId, string resultJson, RpcParams rpcParams = default)
    {
        GameSessionResult result = DeserializeResult(resultJson, GameSessionOperationType.Rejoin);

        if (pendingRejoinRequests.TryGetValue(requestId, out TaskCompletionSource<GameSessionResult> completionSource))
        {
            completionSource.TrySetResult(result);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveGameSceneReadyResultRpc(string requestId, string resultJson, RpcParams rpcParams = default)
    {
        GameSessionResult result = DeserializeResult(resultJson, GameSessionOperationType.SceneReady);

        if (pendingSceneReadyRequests.TryGetValue(requestId, out TaskCompletionSource<GameSessionResult> completionSource))
        {
            completionSource.TrySetResult(result);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveGameSessionSyncResultRpc(string requestId, string resultJson, RpcParams rpcParams = default)
    {
        GameSessionResult result = DeserializeResult(resultJson, GameSessionOperationType.Sync);

        if (pendingSyncRequests.TryGetValue(requestId, out TaskCompletionSource<GameSessionResult> completionSource))
        {
            completionSource.TrySetResult(result);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveLeaveGameResultRpc(string requestId, string resultJson, RpcParams rpcParams = default)
    {
        GameSessionResult result = DeserializeResult(resultJson, GameSessionOperationType.Leave);

        if (pendingLeaveRequests.TryGetValue(requestId, out TaskCompletionSource<GameSessionResult> completionSource))
        {
            completionSource.TrySetResult(result);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveGameSessionUpdatedRpc(string gameSessionJson, RpcParams rpcParams = default)
    {
        try
        {
            GameSessionData gameSessionData = JsonUtility.FromJson<GameSessionData>(gameSessionJson);

            if (gameSessionData != null)
            {
                LocalGameSessionUpdatedReceived?.Invoke(gameSessionData);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveGamePlayerStateChangedRpc(string updateJson, RpcParams rpcParams = default)
    {
        try
        {
            GamePlayerStateChangedData updateData = JsonUtility.FromJson<GamePlayerStateChangedData>(updateJson);

            if (updateData != null)
            {
                LocalGamePlayerStateChangedReceived?.Invoke(updateData);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveGamePlayerMarkedCellChangedRpc(
        string updateJson,
        RpcParams rpcParams = default)
    {
        try
        {
            GamePlayerMarkedCellChangedData updateData =
                JsonUtility.FromJson<GamePlayerMarkedCellChangedData>(updateJson);

            if (updateData != null)
            {
                LocalGamePlayerMarkedCellChangedReceived?.Invoke(updateData);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveGamePlayerLeftRpc(string updateJson, RpcParams rpcParams = default)
    {
        try
        {
            GamePlayerLeftData updateData = JsonUtility.FromJson<GamePlayerLeftData>(updateJson);

            if (updateData != null)
            {
                LocalGamePlayerLeftReceived?.Invoke(updateData);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveGameDeletedRpc(string gameId, RpcParams rpcParams = default)
    {
        LocalGameDeletedReceived?.Invoke(gameId);
    }

    private static bool ScheduleAuthoritySend(
        string sessionId,
        int estimatedBytes,
        MultiplayerNetworkPriority priority,
        MultiplayerNetworkWorkType workType,
        string coalesceKey,
        Func<bool> sendAction)
    {
        MultiplayerNetworkScheduler scheduler = MultiplayerNetworkScheduler.instance;

        if (scheduler == null || !scheduler.IsReady)
        {
            return sendAction();
        }

        return scheduler.Enqueue(sessionId, estimatedBytes, priority, workType, coalesceKey, sendAction);
    }

    private static bool ScheduleAuthoritySend(
        string sessionId,
        string serializedPayload,
        MultiplayerNetworkPriority priority,
        MultiplayerNetworkWorkType workType,
        string coalesceKey,
        Func<bool> sendAction)
    {
        return ScheduleAuthoritySend(
            sessionId,
            MultiplayerNetworkScheduler.EstimateUtf8Bytes(serializedPayload),
            priority,
            workType,
            coalesceKey,
            sendAction);
    }

    private static bool TrySend(NetworkGameSessionConnection connection, Action sendAction)
    {
        if (connection == null || !connection.IsSpawned || !connection.IsServer || sendAction == null)
        {
            return false;
        }

        sendAction();
        return true;
    }

    private static GameSessionResult DeserializeResult(string resultJson, GameSessionOperationType operationType)
    {
        try
        {
            GameSessionResult result = JsonUtility.FromJson<GameSessionResult>(resultJson);

            if (result != null)
            {
                return result;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        return GameSessionResult.Failed(
            operationType,
            GameSessionFailureType.Unknown,
            "The network Game result could not be read.");
    }

    private void CompletePendingRequestsAsFailed()
    {
        foreach (KeyValuePair<string, TaskCompletionSource<GameSessionResult>> request in pendingRejoinRequests)
        {
            request.Value.TrySetResult(GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.NetworkConnectionFailed,
                "The network connection was lost while rejoining the Game."));
        }

        pendingRejoinRequests.Clear();

        foreach (KeyValuePair<string, TaskCompletionSource<GameSessionResult>> request in pendingSceneReadyRequests)
        {
            request.Value.TrySetResult(GameSessionResult.Failed(
                GameSessionOperationType.SceneReady,
                GameSessionFailureType.NetworkConnectionFailed,
                "The network connection was lost before the Game scene became ready."));
        }

        pendingSceneReadyRequests.Clear();

        foreach (KeyValuePair<string, TaskCompletionSource<GameSessionResult>> request in pendingSyncRequests)
        {
            request.Value.TrySetResult(GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.NetworkConnectionFailed,
                "The network connection was lost while synchronizing the Game."));
        }

        pendingSyncRequests.Clear();

        foreach (KeyValuePair<string, TaskCompletionSource<GameSessionResult>> request in pendingLeaveRequests)
        {
            request.Value.TrySetResult(GameSessionResult.Failed(
                GameSessionOperationType.Leave,
                GameSessionFailureType.NetworkConnectionFailed,
                "The network connection was lost while leaving the Game."));
        }

        pendingLeaveRequests.Clear();
    }
}
