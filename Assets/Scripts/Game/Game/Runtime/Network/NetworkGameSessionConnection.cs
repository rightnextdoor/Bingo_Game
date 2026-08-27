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

    public static event Action<GameSessionResult> LocalGameCreationResultReceived;
    public static event Action<string> LocalGameDeletedReceived;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        local = null;
        LocalGameCreationResultReceived = null;
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
            connection.ReceiveRejoinGameResultRpc(
                requestId,
                resultJson,
                connection.RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
        }
    }

    public static bool TrySendGameCreationResult(ulong clientId, GameSessionResult result)
    {
        if (result == null || !TryGetServerConnection(clientId, out NetworkGameSessionConnection connection))
        {
            return false;
        }

        string resultJson = JsonUtility.ToJson(result);
        connection.ReceiveGameCreationResultRpc(
            resultJson,
            connection.RpcTarget.Single(clientId, RpcTargetUse.Temp));
        return true;
    }

    public static bool TrySendGameDeleted(ulong clientId, string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId) || !TryGetServerConnection(clientId, out NetworkGameSessionConnection connection))
        {
            return false;
        }

        connection.ReceiveGameDeletedRpc(
            gameId,
            connection.RpcTarget.Single(clientId, RpcTargetUse.Temp));
        return true;
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
    private void ReceiveGameDeletedRpc(string gameId, RpcParams rpcParams = default)
    {
        LocalGameDeletedReceived?.Invoke(gameId);
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
    }
}
