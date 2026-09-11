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
    private const int MaximumSerializedBatchBytes = 4096;

    private readonly Dictionary<string, TaskCompletionSource<GameSessionResult>> pendingRejoinRequests =
        new Dictionary<string, TaskCompletionSource<GameSessionResult>>();
    private readonly Dictionary<string, TaskCompletionSource<GameSessionResult>> pendingSceneReadyRequests =
        new Dictionary<string, TaskCompletionSource<GameSessionResult>>();
    private readonly Dictionary<string, TaskCompletionSource<GameSessionResult>> pendingSyncRequests =
        new Dictionary<string, TaskCompletionSource<GameSessionResult>>();
    private readonly Dictionary<string, TaskCompletionSource<GameSessionResult>> pendingLeaveRequests =
        new Dictionary<string, TaskCompletionSource<GameSessionResult>>();
    private readonly Dictionary<string, GameSessionBatchAssembly> incomingGameSessionBatches =
        new Dictionary<string, GameSessionBatchAssembly>(StringComparer.Ordinal);
    private readonly Dictionary<string, GamePlayStateBatchAssembly> incomingGamePlayStateBatches =
        new Dictionary<string, GamePlayStateBatchAssembly>(StringComparer.Ordinal);

    public static event Action<GameSessionResult> LocalGameCreationResultReceived;
    public static event Action<GameSessionData> LocalGameSessionUpdatedReceived;
    public static event Action<GamePlayStateChangedData> LocalGamePlayStateChangedReceived;
    public static event Action<GamePlayerStateChangedData> LocalGamePlayerStateChangedReceived;
    public static event Action<GamePlayerMarkedCellChangedData> LocalGamePlayerMarkedCellChangedReceived;
    public static event Action<GameBingoCheckResolvedData> LocalBingoCheckResolvedReceived;
    public static event Action<GamePlayerLeftData> LocalGamePlayerLeftReceived;
    public static event Action<string> LocalGameDeletedReceived;

    private sealed class GameSessionBatchAssembly
    {
        public string requestId = string.Empty;
        public GameSessionOperationType operationType = GameSessionOperationType.None;
        public int nextBatchIndex;
        public GameSessionData gameSessionData;
    }

    private sealed class GamePlayStateBatchAssembly
    {
        public int nextBatchIndex;
        public GamePlayStateChangedData gamePlayState;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        local = null;
        LocalGameCreationResultReceived = null;
        LocalGameSessionUpdatedReceived = null;
        LocalGamePlayStateChangedReceived = null;
        LocalGamePlayerStateChangedReceived = null;
        LocalGamePlayerMarkedCellChangedReceived = null;
        LocalBingoCheckResolvedReceived = null;
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
        incomingGameSessionBatches.Clear();
        incomingGamePlayStateBatches.Clear();
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

    public bool RequestBingoCheck(
        string gameId,
        LobbyBoardData boardData,
        IReadOnlyList<int> markedCellIndices)
    {
        if (!IsSpawned ||
            !IsOwner ||
            string.IsNullOrWhiteSpace(gameId) ||
            boardData == null)
        {
            return false;
        }

        GameBingoCheckRequestData requestData =
            new GameBingoCheckRequestData(boardData, markedCellIndices);
        RequestBingoCheckRpc(gameId, JsonUtility.ToJson(requestData));
        return true;
    }

    public bool RequestBingoCheckAnimationCompleted(string gameId)
    {
        if (!IsSpawned || !IsOwner || string.IsNullOrWhiteSpace(gameId))
        {
            return false;
        }

        RequestBingoCheckAnimationCompletedRpc(gameId);
        return true;
    }

    public bool RequestRiskDecision(string gameId, bool endPlayerGame)
    {
        if (!IsSpawned || !IsOwner || string.IsNullOrWhiteSpace(gameId))
        {
            return false;
        }

        RequestRiskDecisionRpc(gameId, endPlayerGame);
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
            if (result?.success == true && result.gameSessionData != null)
            {
                TrySendGameSessionBatches(
                    connection,
                    senderClientId,
                    requestId,
                    result,
                    MultiplayerNetworkPriority.Critical);
            }
            else
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
            if (result?.success == true && result.gameSessionData != null)
            {
                TrySendGameSessionBatches(
                    connection,
                    senderClientId,
                    requestId,
                    result,
                    MultiplayerNetworkPriority.Critical);
            }
            else
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestBingoCheckRpc(
        string gameId,
        string requestJson,
        RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        GameBingoCheckResolvedData resolvedData;

        try
        {
            GameBingoCheckRequestData requestData =
                JsonUtility.FromJson<GameBingoCheckRequestData>(requestJson);

            resolvedData = NetworkGameSessionManager.instance != null &&
                           NetworkGameSessionManager.instance.IsReady
                ? NetworkGameSessionManager.instance.ProcessAuthorityBingoCheck(
                    senderClientId,
                    gameId,
                    requestData)
                : GameBingoCheckResolvedData.Rejected(
                    gameId,
                    string.Empty,
                    0,
                    "The authoritative network Game session manager is not ready.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            resolvedData = GameBingoCheckResolvedData.Rejected(
                gameId,
                string.Empty,
                0,
                "The Bingo check request could not be read by the authority.");
        }

        if (!TryGetServerConnection(
                senderClientId,
                out NetworkGameSessionConnection connection))
        {
            return;
        }

        string resultJson = JsonUtility.ToJson(resolvedData);
        ScheduleAuthoritySend(
            resolvedData?.gameId ?? gameId,
            resultJson,
            MultiplayerNetworkPriority.Critical,
            MultiplayerNetworkWorkType.Event,
            string.Empty,
            () => TrySend(connection, () => connection.ReceiveBingoCheckResolvedRpc(
                resultJson,
                connection.RpcTarget.Single(senderClientId, RpcTargetUse.Temp))));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestBingoCheckAnimationCompletedRpc(
        string gameId,
        RpcParams rpcParams = default)
    {
        NetworkGameSessionManager.instance?.ProcessAuthorityBingoCheckAnimationCompleted(
            rpcParams.Receive.SenderClientId,
            gameId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestRiskDecisionRpc(
        string gameId,
        bool endPlayerGame,
        RpcParams rpcParams = default)
    {
        NetworkGameSessionManager.instance?.ProcessAuthorityRiskDecision(
            rpcParams.Receive.SenderClientId,
            gameId,
            endPlayerGame);
    }

    public static bool TrySendGameCreationResult(ulong clientId, GameSessionResult result)
    {
        if (result == null || !TryGetServerConnection(clientId, out NetworkGameSessionConnection connection))
        {
            return false;
        }

        if (result.success && result.gameSessionData != null)
        {
            return TrySendGameSessionBatches(
                connection,
                clientId,
                string.Empty,
                result,
                MultiplayerNetworkPriority.Critical);
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

        GameSessionResult result = GameSessionResult.Succeeded(
            GameSessionOperationType.None,
            gameSessionData);

        return TrySendGameSessionBatches(
            connection,
            clientId,
            string.Empty,
            result,
            MultiplayerNetworkPriority.High);
    }

    public static bool TrySendGamePlayStateChanged(
        ulong clientId,
        GamePlayStateChangedData updateData)
    {
        if (updateData == null ||
            !TryGetServerConnection(clientId, out NetworkGameSessionConnection connection))
        {
            return false;
        }

        List<GamePlayStateChangedBatchData> batches =
            BuildGamePlayStateBatches(updateData);

        if (batches == null || batches.Count == 0)
        {
            return false;
        }

        bool allScheduled = true;

        for (int i = 0; i < batches.Count; i++)
        {
            GamePlayStateChangedBatchData batch = batches[i];
            string batchJson = JsonUtility.ToJson(batch);

            allScheduled &= ScheduleAuthoritySend(
                updateData.gameId,
                batchJson,
                MultiplayerNetworkPriority.High,
                MultiplayerNetworkWorkType.Event,
                string.Empty,
                () => TrySend(connection, () => connection.ReceiveGamePlayStateBatchRpc(
                    batchJson,
                    connection.RpcTarget.Single(clientId, RpcTargetUse.Temp))));
        }

        return allScheduled;
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

    public static bool TrySendBingoCheckResolved(
        ulong clientId,
        GameBingoCheckResolvedData resolvedData)
    {
        if (resolvedData == null ||
            !TryGetServerConnection(clientId, out NetworkGameSessionConnection connection))
        {
            return false;
        }

        string resultJson = JsonUtility.ToJson(resolvedData);

        return ScheduleAuthoritySend(
            resolvedData.gameId,
            resultJson,
            MultiplayerNetworkPriority.Critical,
            MultiplayerNetworkWorkType.Event,
            string.Empty,
            () => TrySend(connection, () => connection.ReceiveBingoCheckResolvedRpc(
                resultJson,
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
    private void ReceiveGameSessionBatchRpc(
        string batchJson,
        RpcParams rpcParams = default)
    {
        try
        {
            GameSessionSyncBatchData batch =
                JsonUtility.FromJson<GameSessionSyncBatchData>(batchJson);
            ApplyIncomingGameSessionBatch(batch);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveGamePlayStateBatchRpc(
        string batchJson,
        RpcParams rpcParams = default)
    {
        try
        {
            GamePlayStateChangedBatchData batch =
                JsonUtility.FromJson<GamePlayStateChangedBatchData>(batchJson);
            ApplyIncomingGamePlayStateBatch(batch);
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
    private void ReceiveBingoCheckResolvedRpc(
        string resultJson,
        RpcParams rpcParams = default)
    {
        try
        {
            GameBingoCheckResolvedData resolvedData =
                JsonUtility.FromJson<GameBingoCheckResolvedData>(resultJson);

            if (resolvedData != null)
            {
                LocalBingoCheckResolvedReceived?.Invoke(resolvedData);
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

    private static bool TrySendGameSessionBatches(
        NetworkGameSessionConnection connection,
        ulong clientId,
        string requestId,
        GameSessionResult result,
        MultiplayerNetworkPriority priority)
    {
        if (connection == null || result?.gameSessionData == null)
        {
            return false;
        }

        List<GameSessionSyncBatchData> batches =
            BuildGameSessionBatches(requestId, result);

        if (batches.Count == 0)
        {
            return false;
        }

        List<string> serializedBatches = new List<string>(batches.Count);

        for (int i = 0; i < batches.Count; i++)
        {
            serializedBatches.Add(JsonUtility.ToJson(batches[i]));
        }

        bool allScheduled = true;

        for (int i = 0; i < serializedBatches.Count; i++)
        {
            string batchJson = serializedBatches[i];

            allScheduled &= ScheduleAuthoritySend(
                result.gameId,
                batchJson,
                priority,
                MultiplayerNetworkWorkType.Event,
                string.Empty,
                () => TrySend(connection, () => connection.ReceiveGameSessionBatchRpc(
                    batchJson,
                    connection.RpcTarget.Single(clientId, RpcTargetUse.Temp))));
        }

        return allScheduled;
    }

    private static List<GameSessionSyncBatchData> BuildGameSessionBatches(
        string requestId,
        GameSessionResult result)
    {
        List<GameSessionSyncBatchData> batches =
            new List<GameSessionSyncBatchData>();
        GameSessionData source = result?.gameSessionData;

        if (source == null)
        {
            return batches;
        }

        string transferId = Guid.NewGuid().ToString("N");
        GameSessionData header = new GameSessionData(source);
        header.players.Clear();

        GameSessionSyncBatchData currentBatch = new GameSessionSyncBatchData(
            transferId,
            requestId,
            result.operationType,
            0,
            true,
            false,
            header,
            null);

        IReadOnlyList<GamePlayerData> sourcePlayers = source.players;

        if (sourcePlayers != null)
        {
            for (int i = 0; i < sourcePlayers.Count; i++)
            {
                GamePlayerData playerData = sourcePlayers[i];

                if (playerData == null)
                {
                    continue;
                }

                currentBatch.players.Add(new GamePlayerData(playerData));

                if (FitsBatchBudget(currentBatch))
                {
                    continue;
                }

                currentBatch.players.RemoveAt(currentBatch.players.Count - 1);

                if (currentBatch.gameSessionData != null ||
                    currentBatch.players.Count > 0)
                {
                    batches.Add(currentBatch);
                    currentBatch = new GameSessionSyncBatchData(
                        transferId,
                        requestId,
                        result.operationType,
                        batches.Count,
                        false,
                        false,
                        null,
                        new[] { playerData });
                }
                else
                {
                    currentBatch.players.Add(new GamePlayerData(playerData));
                }
            }
        }

        batches.Add(currentBatch);

        for (int i = 0; i < batches.Count; i++)
        {
            batches[i].batchIndex = i;
            batches[i].isFinalBatch = i == batches.Count - 1;
        }

        return batches;
    }

    private static List<GamePlayStateChangedBatchData> BuildGamePlayStateBatches(
        GamePlayStateChangedData updateData)
    {
        List<GamePlayStateChangedBatchData> batches =
            new List<GamePlayStateChangedBatchData>();

        if (updateData == null)
        {
            return batches;
        }

        string transferId = Guid.NewGuid().ToString("N");
        GamePlayStateChangedBatchData currentBatch =
            CreateGamePlayStateBatch(transferId, 0, true, updateData);

        if (updateData.playerStates != null)
        {
            for (int i = 0; i < updateData.playerStates.Count; i++)
            {
                GamePlayerMatchStateData playerState = updateData.playerStates[i];

                if (playerState == null)
                {
                    continue;
                }

                currentBatch.playerStates.Add(playerState);

                if (FitsBatchBudget(currentBatch))
                {
                    continue;
                }

                currentBatch.playerStates.RemoveAt(currentBatch.playerStates.Count - 1);

                if (currentBatch.gamePlayState != null ||
                    currentBatch.playerStates.Count > 0)
                {
                    batches.Add(currentBatch);
                    currentBatch = CreateGamePlayStateBatch(
                        transferId,
                        batches.Count,
                        false,
                        updateData);
                    currentBatch.playerStates.Add(playerState);
                }
                else
                {
                    currentBatch.playerStates.Add(playerState);
                }
            }
        }

        batches.Add(currentBatch);

        for (int i = 0; i < batches.Count; i++)
        {
            batches[i].batchIndex = i;
            batches[i].isFinalBatch = i == batches.Count - 1;
        }

        return batches;
    }

    private static GamePlayStateChangedBatchData CreateGamePlayStateBatch(
        string transferId,
        int batchIndex,
        bool resetState,
        GamePlayStateChangedData updateData)
    {
        GamePlayStateChangedBatchData batch =
            new GamePlayStateChangedBatchData(
                transferId,
                batchIndex,
                resetState,
                false,
                updateData,
                null);

        if (!resetState)
        {
            batch.gamePlayState = null;
        }

        return batch;
    }

    private static bool FitsBatchBudget(object batch)
    {
        return batch != null &&
               MultiplayerNetworkScheduler.EstimateUtf8Bytes(
                   JsonUtility.ToJson(batch)) <= MaximumSerializedBatchBytes;
    }

    private void ApplyIncomingGameSessionBatch(GameSessionSyncBatchData batch)
    {
        if (batch == null || string.IsNullOrWhiteSpace(batch.transferId))
        {
            return;
        }

        if (batch.resetState)
        {
            if (batch.batchIndex != 0 || batch.gameSessionData == null)
            {
                return;
            }

            GameSessionData gameSessionData = new GameSessionData(batch.gameSessionData);
            gameSessionData.players.Clear();
            incomingGameSessionBatches[batch.transferId] =
                new GameSessionBatchAssembly
                {
                    requestId = batch.requestId ?? string.Empty,
                    operationType = batch.operationType,
                    nextBatchIndex = 0,
                    gameSessionData = gameSessionData
                };
        }

        if (!incomingGameSessionBatches.TryGetValue(
                batch.transferId,
                out GameSessionBatchAssembly assembly) ||
            assembly.gameSessionData == null ||
            batch.batchIndex != assembly.nextBatchIndex ||
            batch.operationType != assembly.operationType ||
            !string.Equals(
                batch.requestId ?? string.Empty,
                assembly.requestId,
                StringComparison.Ordinal))
        {
            incomingGameSessionBatches.Remove(batch.transferId);
            return;
        }

        if (batch.players != null)
        {
            for (int i = 0; i < batch.players.Count; i++)
            {
                GamePlayerData playerData = batch.players[i];

                if (playerData != null)
                {
                    assembly.gameSessionData.players.Add(
                        new GamePlayerData(playerData));
                }
            }
        }

        assembly.nextBatchIndex++;

        if (!batch.isFinalBatch)
        {
            return;
        }

        incomingGameSessionBatches.Remove(batch.transferId);
        CompleteGameSessionBatch(assembly);
    }

    private void CompleteGameSessionBatch(GameSessionBatchAssembly assembly)
    {
        if (assembly?.gameSessionData == null)
        {
            return;
        }

        GameSessionResult result = GameSessionResult.Succeeded(
            assembly.operationType,
            assembly.gameSessionData);

        switch (assembly.operationType)
        {
            case GameSessionOperationType.Create:
                LocalGameCreationResultReceived?.Invoke(result);
                break;

            case GameSessionOperationType.Rejoin:
                if (pendingRejoinRequests.TryGetValue(
                        assembly.requestId,
                        out TaskCompletionSource<GameSessionResult> rejoinCompletion))
                {
                    rejoinCompletion.TrySetResult(result);
                }
                break;

            case GameSessionOperationType.Sync:
                if (pendingSyncRequests.TryGetValue(
                        assembly.requestId,
                        out TaskCompletionSource<GameSessionResult> syncCompletion))
                {
                    syncCompletion.TrySetResult(result);
                }
                break;

            default:
                LocalGameSessionUpdatedReceived?.Invoke(
                    new GameSessionData(assembly.gameSessionData));
                break;
        }
    }

    private void ApplyIncomingGamePlayStateBatch(
        GamePlayStateChangedBatchData batch)
    {
        if (batch == null ||
            string.IsNullOrWhiteSpace(batch.transferId) ||
            string.IsNullOrWhiteSpace(batch.gameId))
        {
            return;
        }

        if (batch.resetState)
        {
            if (batch.batchIndex != 0 || batch.gamePlayState == null)
            {
                return;
            }

            batch.gamePlayState.playerStates ??=
                new List<GamePlayerMatchStateData>();
            batch.gamePlayState.playerStates.Clear();
            incomingGamePlayStateBatches[batch.transferId] =
                new GamePlayStateBatchAssembly
                {
                    nextBatchIndex = 0,
                    gamePlayState = batch.gamePlayState
                };
        }

        if (!incomingGamePlayStateBatches.TryGetValue(
                batch.transferId,
                out GamePlayStateBatchAssembly assembly) ||
            assembly.gamePlayState == null ||
            batch.batchIndex != assembly.nextBatchIndex ||
            batch.revision != assembly.gamePlayState.revision ||
            !string.Equals(
                batch.gameId,
                assembly.gamePlayState.gameId,
                StringComparison.Ordinal))
        {
            incomingGamePlayStateBatches.Remove(batch.transferId);
            return;
        }

        if (batch.playerStates != null)
        {
            for (int i = 0; i < batch.playerStates.Count; i++)
            {
                GamePlayerMatchStateData playerState = batch.playerStates[i];

                if (playerState != null)
                {
                    assembly.gamePlayState.playerStates.Add(playerState);
                }
            }
        }

        assembly.nextBatchIndex++;

        if (!batch.isFinalBatch)
        {
            return;
        }

        incomingGamePlayStateBatches.Remove(batch.transferId);
        LocalGamePlayStateChangedReceived?.Invoke(assembly.gamePlayState);
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

        try
        {
            sendAction();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
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
