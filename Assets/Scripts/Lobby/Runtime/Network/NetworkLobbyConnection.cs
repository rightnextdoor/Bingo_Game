using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkLobbyConnection :
    NetworkBehaviour
{
    public static NetworkLobbyConnection local;

    private const float RequestTimeoutSeconds = 30f;

    private readonly Dictionary<
        string,
        TaskCompletionSource<LobbyEntryResult>>
        pendingRequests =
            new Dictionary<
                string,
                TaskCompletionSource<LobbyEntryResult>>();


    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        local = null;
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

    public async Task<LobbyEntryResult>
        RequestEnterLobbyAsync(
            LobbySetupData lobbySetupData)
    {
        if (!IsSpawned || !IsOwner)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType
                    .NetworkLobbyConnectionUnavailable,
                "The local network lobby connection is not ready.");
        }

        if (lobbySetupData == null)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.InvalidSetupData,
                "The lobby setup data is missing.");
        }

        string requestId =
            Guid.NewGuid().ToString("N");

        string setupDataJson =
            JsonUtility.ToJson(lobbySetupData);

        TaskCompletionSource<LobbyEntryResult>
            completionSource =
                new TaskCompletionSource<
                    LobbyEntryResult>();

        pendingRequests.Add(
            requestId,
            completionSource);

        RequestEnterLobbyRpc(
            requestId,
            setupDataJson);

        float timeoutTime =
            Time.realtimeSinceStartup +
            RequestTimeoutSeconds;

        while (!completionSource.Task.IsCompleted)
        {
            if (Time.realtimeSinceStartup >=
                timeoutTime)
            {
                pendingRequests.Remove(requestId);

                return LobbyEntryResult.Failed(
                    LobbyEntryFailureType
                        .LobbyJoinFailed,
                    "The network lobby request timed out.");
            }

            await Task.Yield();
        }

        pendingRequests.Remove(requestId);

        return await completionSource.Task;
    }

    [Rpc(
        SendTo.Server,
        InvokePermission = RpcInvokePermission.Owner)]
    private void RequestEnterLobbyRpc(
        string requestId,
        string setupDataJson,
        RpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        LobbyEntryResult result;

        LobbySetupData lobbySetupData = null;

        try
        {
            lobbySetupData =
                JsonUtility
                    .FromJson<LobbySetupData>(
                        setupDataJson);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        if (lobbySetupData == null)
        {
            result =
                LobbyEntryResult.Failed(
                    LobbyEntryFailureType
                        .InvalidSetupData,
                    "The network lobby setup data could not be read.");
        }
        else if (NetworkLobbyManager.instance == null ||
                 !NetworkLobbyManager.instance.IsReady)
        {
            result =
                LobbyEntryResult.Failed(
                    LobbyEntryFailureType
                        .ServiceUnavailable,
                    "The authoritative network lobby manager is not ready.");
        }
        else
        {
            result =
                NetworkLobbyManager.instance
                    .ProcessAuthorityLobbyEntry(
                        lobbySetupData,
                        senderClientId);
        }

        string resultJson =
            JsonUtility.ToJson(result);

        ReceiveLobbyEntryResultRpc(
            requestId,
            resultJson,
            RpcTarget.Single(
                senderClientId,
                RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveLobbyEntryResultRpc(
        string requestId,
        string resultJson,
        RpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return;
        }

        if (!pendingRequests.TryGetValue(
                requestId,
                out TaskCompletionSource<LobbyEntryResult>
                    completionSource))
        {
            return;
        }

        LobbyEntryResult result = null;

        try
        {
            result =
                JsonUtility
                    .FromJson<LobbyEntryResult>(
                        resultJson);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        result ??=
            LobbyEntryResult.Failed(
                LobbyEntryFailureType.Unknown,
                "The network lobby result could not be read.");

        completionSource.TrySetResult(result);
    }

    private void CompletePendingRequestsAsFailed()
    {
        foreach (
            KeyValuePair<
                string,
                TaskCompletionSource<LobbyEntryResult>>
            pendingRequest in pendingRequests)
        {
            pendingRequest.Value.TrySetResult(
                LobbyEntryResult.Failed(
                    LobbyEntryFailureType
                        .NetworkConnectionFailed,
                    "The network connection was closed."));
        }

        pendingRequests.Clear();
    }
}