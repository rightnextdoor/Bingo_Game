using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkSessionPlayer))]
public class NetworkLobbyConnection : NetworkBehaviour
{
    #region Fields

    public static NetworkLobbyConnection local;

    private const float RequestTimeoutSeconds = 30f;

    private readonly Dictionary<string, TaskCompletionSource<LobbyEntryResult>> pendingEntryRequests = new Dictionary<string, TaskCompletionSource<LobbyEntryResult>>();
    private readonly Dictionary<string, TaskCompletionSource<LobbyExitResult>> pendingExitRequests = new Dictionary<string, TaskCompletionSource<LobbyExitResult>>();
    private readonly Dictionary<string, TaskCompletionSource<bool>> pendingHostSettingsRequests = new Dictionary<string, TaskCompletionSource<bool>>();

    public static event Action<LobbyExitNotification> LocalLobbyExitReceived;
    public static event Action<LobbyViewData> LocalLobbyViewReceived;
    public static event Action<LobbyPlayerBoardUpdateData> LocalPlayerBoardUpdateReceived;
    public static event Action<LobbyBoardCollectionData> LocalPlayerBoardCollectionReceived;

    #endregion

    #region Unity Methods

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        local = null;
        LocalLobbyExitReceived = null;
        LocalLobbyViewReceived = null;
        LocalPlayerBoardUpdateReceived = null;
        LocalPlayerBoardCollectionReceived = null;
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

    #endregion

    #region Connection Lookup

    public static NetworkLobbyConnection GetLocalConnection()
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

        NetworkLobbyConnection connection = sessionPlayer.GetComponent<NetworkLobbyConnection>();

        if (connection == null || !connection.IsSpawned)
        {
            return null;
        }

        local = connection;
        return local;
    }

    private static bool TryGetServerConnection(ulong clientId, out NetworkLobbyConnection connection)
    {
        connection = null;

        if (!NetworkSessionPlayer.TryGetServerPlayer(clientId, out NetworkSessionPlayer sessionPlayer))
        {
            return false;
        }

        connection = sessionPlayer.GetComponent<NetworkLobbyConnection>();
        return connection != null && connection.IsSpawned && connection.IsServer;
    }

    #endregion

    #region Client Requests

    public async Task<LobbyEntryResult> RequestEnterLobbyAsync(LobbySetupData lobbySetupData)
    {
        if (!IsSpawned || !IsOwner)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.NetworkLobbyConnectionUnavailable, "The local network lobby connection is not ready.");
        }

        if (lobbySetupData == null)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.InvalidSetupData, "The lobby setup data is missing.");
        }

        string requestId = Guid.NewGuid().ToString("N");
        string setupDataJson = JsonUtility.ToJson(lobbySetupData);
        TaskCompletionSource<LobbyEntryResult> completionSource = new TaskCompletionSource<LobbyEntryResult>();

        pendingEntryRequests.Add(requestId, completionSource);
        RequestEnterLobbyRpc(requestId, setupDataJson);

        LobbyEntryResult timeoutResult = LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyJoinFailed, "The network lobby request timed out.");
        return await WaitForEntryResultAsync(requestId, completionSource, timeoutResult);
    }

    public async Task<LobbyExitResult> RequestLeaveLobbyAsync()
    {
        if (!IsSpawned || !IsOwner)
        {
            return LobbyExitResult.Failed(string.Empty, LobbyPlayerExitReason.VoluntaryLeave, "The local network lobby connection is not ready.");
        }

        string requestId = Guid.NewGuid().ToString("N");
        TaskCompletionSource<LobbyExitResult> completionSource = new TaskCompletionSource<LobbyExitResult>();

        pendingExitRequests.Add(requestId, completionSource);
        RequestLeaveLobbyRpc(requestId);

        LobbyExitResult timeoutResult = LobbyExitResult.Failed(string.Empty, LobbyPlayerExitReason.VoluntaryLeave, "The network lobby leave request timed out.");
        return await WaitForExitResultAsync(requestId, completionSource, timeoutResult);
    }

    public async Task<LobbyExitResult> RequestKickPlayerAsync(string targetUserId)
    {
        if (!IsSpawned || !IsOwner)
        {
            return LobbyExitResult.Failed(targetUserId, LobbyPlayerExitReason.Kicked, "The local network lobby connection is not ready.");
        }

        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            return LobbyExitResult.Failed(targetUserId, LobbyPlayerExitReason.Kicked, "The target player UserId is missing.");
        }

        string requestId = Guid.NewGuid().ToString("N");
        TaskCompletionSource<LobbyExitResult> completionSource = new TaskCompletionSource<LobbyExitResult>();

        pendingExitRequests.Add(requestId, completionSource);
        RequestKickPlayerRpc(requestId, targetUserId);

        LobbyExitResult timeoutResult = LobbyExitResult.Failed(targetUserId, LobbyPlayerExitReason.Kicked, "The network kick request timed out.");
        return await WaitForExitResultAsync(requestId, completionSource, timeoutResult);
    }

    public async Task<bool> RequestApplyHostSettingsAsync(LobbyHostSettingsData settingsData)
    {
        if (!IsSpawned || !IsOwner || settingsData == null)
        {
            return false;
        }

        string requestId = Guid.NewGuid().ToString("N");
        string settingsJson = JsonUtility.ToJson(settingsData);
        TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>();

        pendingHostSettingsRequests.Add(requestId, completionSource);
        RequestApplyHostSettingsRpc(requestId, settingsJson);

        return await WaitForHostSettingsResultAsync(requestId, completionSource);
    }

    public void NotifyLobbySceneReady()
    {
        if (IsSpawned && IsOwner)
        {
            NotifyLobbySceneReadyRpc();
        }
    }

    public void RequestSetPlayerReady(bool isReady)
    {
        if (IsSpawned && IsOwner)
        {
            RequestSetPlayerReadyRpc(isReady);
        }
    }

    public void RequestStartLobby()
    {
        if (IsSpawned && IsOwner)
        {
            RequestStartLobbyRpc();
        }
    }

    public void RequestRerollBoard()
    {
        if (IsSpawned && IsOwner)
        {
            RequestRerollBoardRpc();
        }
    }

    #endregion

    #region Authority Request RPCs

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestEnterLobbyRpc(string requestId, string setupDataJson, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        LobbyEntryResult result;
        LobbySetupData lobbySetupData = null;

        try
        {
            lobbySetupData = JsonUtility.FromJson<LobbySetupData>(setupDataJson);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        if (lobbySetupData == null)
        {
            result = LobbyEntryResult.Failed(LobbyEntryFailureType.InvalidSetupData, "The network lobby setup data could not be read.");
        }
        else if (NetworkLobbyManager.instance == null || !NetworkLobbyManager.instance.IsReady)
        {
            result = LobbyEntryResult.Failed(LobbyEntryFailureType.ServiceUnavailable, "The authoritative network lobby manager is not ready.");
        }
        else
        {
            result = NetworkLobbyManager.instance.ProcessAuthorityLobbyEntry(lobbySetupData, senderClientId);
        }

        string resultJson = JsonUtility.ToJson(result);

        ReceiveLobbyEntryResultRpc(requestId, resultJson, RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestLeaveLobbyRpc(string requestId, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        LobbyExitResult result;

        if (NetworkLobbyManager.instance == null || !NetworkLobbyManager.instance.IsReady)
        {
            result = LobbyExitResult.Failed(string.Empty, LobbyPlayerExitReason.VoluntaryLeave, "The authoritative network lobby manager is not ready.");
        }
        else
        {
            result = NetworkLobbyManager.instance.ProcessAuthorityLobbyExit(senderClientId);
        }

        string resultJson = JsonUtility.ToJson(result);
        ReceiveLobbyExitResultRpc(requestId, resultJson, RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestKickPlayerRpc(string requestId, string targetUserId, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        LobbyExitResult result;

        if (NetworkLobbyManager.instance == null || !NetworkLobbyManager.instance.IsReady)
        {
            result = LobbyExitResult.Failed(targetUserId, LobbyPlayerExitReason.Kicked, "The authoritative network lobby manager is not ready.");
        }
        else
        {
            result = NetworkLobbyManager.instance.ProcessAuthorityKickPlayer(senderClientId, targetUserId);
        }

        string resultJson = JsonUtility.ToJson(result);
        ReceiveLobbyExitResultRpc(requestId, resultJson, RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestApplyHostSettingsRpc(string requestId, string settingsJson, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        LobbyHostSettingsData settingsData = null;

        try
        {
            settingsData = JsonUtility.FromJson<LobbyHostSettingsData>(settingsJson);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        bool success = settingsData != null && NetworkLobbyManager.instance != null && NetworkLobbyManager.instance.IsReady && NetworkLobbyManager.instance.ProcessAuthorityApplyHostSettings(senderClientId, settingsData);
        ReceiveHostSettingsResultRpc(requestId, success, RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void NotifyLobbySceneReadyRpc(RpcParams rpcParams = default)
    {
        NetworkLobbyManager.instance?.ProcessAuthorityLobbySceneReady(rpcParams.Receive.SenderClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestSetPlayerReadyRpc(bool isReady, RpcParams rpcParams = default)
    {
        NetworkLobbyManager.instance?.ProcessAuthoritySetPlayerReady(rpcParams.Receive.SenderClientId, isReady);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestStartLobbyRpc(RpcParams rpcParams = default)
    {
        NetworkLobbyManager.instance?.ProcessAuthorityStartLobby(rpcParams.Receive.SenderClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestRerollBoardRpc(RpcParams rpcParams = default)
    {
        NetworkLobbyManager.instance?.ProcessAuthorityRerollBoard(rpcParams.Receive.SenderClientId);
    }

    #endregion

    #region Authority Sends

    public static bool TrySendLobbyView(ulong clientId, LobbyViewData lobbyViewData)
    {
        if (lobbyViewData == null || !TryGetServerConnection(clientId, out NetworkLobbyConnection connection))
        {
            return false;
        }

        string lobbyViewJson = JsonUtility.ToJson(lobbyViewData);
        connection.ReceiveLobbyViewRpc(lobbyViewJson, connection.RpcTarget.Single(clientId, RpcTargetUse.Temp));
        return true;
    }

    public static bool TrySendForcedLobbyExit(ulong clientId, LobbyExitNotification notification)
    {
        if (notification == null || !TryGetServerConnection(clientId, out NetworkLobbyConnection connection))
        {
            return false;
        }

        string notificationJson = JsonUtility.ToJson(notification);
        connection.ReceiveForcedLobbyExitRpc(notificationJson, connection.RpcTarget.Single(clientId, RpcTargetUse.Temp));
        return true;
    }

    public static bool TrySendPlayerBoardUpdate(ulong clientId, LobbyPlayerBoardUpdateData updateData)
    {
        if (updateData == null || !TryGetServerConnection(clientId, out NetworkLobbyConnection connection))
        {
            return false;
        }

        string updateJson = JsonUtility.ToJson(updateData);
        connection.ReceivePlayerBoardUpdateRpc(updateJson, connection.RpcTarget.Single(clientId, RpcTargetUse.Temp));
        return true;
    }

    public static bool TrySendPlayerBoardCollection(ulong clientId, LobbyBoardCollectionData boardCollectionData)
    {
        if (boardCollectionData == null || !TryGetServerConnection(clientId, out NetworkLobbyConnection connection))
        {
            return false;
        }

        string boardCollectionJson = JsonUtility.ToJson(boardCollectionData);
        connection.ReceivePlayerBoardCollectionRpc(boardCollectionJson, connection.RpcTarget.Single(clientId, RpcTargetUse.Temp));
        return true;
    }

    #endregion

    #region Client Receive RPCs

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveLobbyEntryResultRpc(string requestId, string resultJson, RpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(requestId) || !pendingEntryRequests.TryGetValue(requestId, out TaskCompletionSource<LobbyEntryResult> completionSource))
        {
            return;
        }

        LobbyEntryResult result = null;

        try
        {
            result = JsonUtility.FromJson<LobbyEntryResult>(resultJson);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        result ??= LobbyEntryResult.Failed(LobbyEntryFailureType.Unknown, "The network lobby result could not be read.");
        completionSource.TrySetResult(result);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveLobbyExitResultRpc(string requestId, string resultJson, RpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(requestId) || !pendingExitRequests.TryGetValue(requestId, out TaskCompletionSource<LobbyExitResult> completionSource))
        {
            return;
        }

        LobbyExitResult result = null;

        try
        {
            result = JsonUtility.FromJson<LobbyExitResult>(resultJson);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        result ??= LobbyExitResult.Failed(string.Empty, LobbyPlayerExitReason.VoluntaryLeave, "The network lobby exit result could not be read.");
        completionSource.TrySetResult(result);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveHostSettingsResultRpc(string requestId, bool success, RpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(requestId) || !pendingHostSettingsRequests.TryGetValue(requestId, out TaskCompletionSource<bool> completionSource))
        {
            return;
        }

        completionSource.TrySetResult(success);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveLobbyViewRpc(string lobbyViewJson, RpcParams rpcParams = default)
    {
        LobbyViewData lobbyViewData = null;

        try
        {
            lobbyViewData = JsonUtility.FromJson<LobbyViewData>(lobbyViewJson);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        if (lobbyViewData != null)
        {
            LocalLobbyViewReceived?.Invoke(lobbyViewData);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveForcedLobbyExitRpc(string notificationJson, RpcParams rpcParams = default)
    {
        LobbyExitNotification notification = null;

        try
        {
            notification = JsonUtility.FromJson<LobbyExitNotification>(notificationJson);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        if (notification != null)
        {
            LocalLobbyExitReceived?.Invoke(notification);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceivePlayerBoardUpdateRpc(string updateJson, RpcParams rpcParams = default)
    {
        LobbyPlayerBoardUpdateData updateData = null;

        try
        {
            updateData = JsonUtility.FromJson<LobbyPlayerBoardUpdateData>(updateJson);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        if (updateData != null)
        {
            LocalPlayerBoardUpdateReceived?.Invoke(updateData);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceivePlayerBoardCollectionRpc(string boardCollectionJson, RpcParams rpcParams = default)
    {
        LobbyBoardCollectionData boardCollectionData = null;

        try
        {
            boardCollectionData = JsonUtility.FromJson<LobbyBoardCollectionData>(boardCollectionJson);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        if (boardCollectionData != null)
        {
            LocalPlayerBoardCollectionReceived?.Invoke(boardCollectionData);
        }
    }

    #endregion

    #region Request Waiting

    private async Task<LobbyEntryResult> WaitForEntryResultAsync(string requestId, TaskCompletionSource<LobbyEntryResult> completionSource, LobbyEntryResult timeoutResult)
    {
        float timeoutTime = Time.realtimeSinceStartup + RequestTimeoutSeconds;

        while (!completionSource.Task.IsCompleted)
        {
            if (Time.realtimeSinceStartup >= timeoutTime)
            {
                pendingEntryRequests.Remove(requestId);
                return timeoutResult;
            }

            await Task.Yield();
        }

        pendingEntryRequests.Remove(requestId);
        return await completionSource.Task;
    }

    private async Task<LobbyExitResult> WaitForExitResultAsync(string requestId, TaskCompletionSource<LobbyExitResult> completionSource, LobbyExitResult timeoutResult)
    {
        float timeoutTime = Time.realtimeSinceStartup + RequestTimeoutSeconds;

        while (!completionSource.Task.IsCompleted)
        {
            if (Time.realtimeSinceStartup >= timeoutTime)
            {
                pendingExitRequests.Remove(requestId);
                return timeoutResult;
            }

            await Task.Yield();
        }

        pendingExitRequests.Remove(requestId);
        return await completionSource.Task;
    }

    private async Task<bool> WaitForHostSettingsResultAsync(string requestId, TaskCompletionSource<bool> completionSource)
    {
        float timeoutTime = Time.realtimeSinceStartup + RequestTimeoutSeconds;

        while (!completionSource.Task.IsCompleted)
        {
            if (Time.realtimeSinceStartup >= timeoutTime)
            {
                pendingHostSettingsRequests.Remove(requestId);
                return false;
            }

            await Task.Yield();
        }

        pendingHostSettingsRequests.Remove(requestId);
        return await completionSource.Task;
    }

    private void CompletePendingRequestsAsFailed()
    {
        foreach (KeyValuePair<string, TaskCompletionSource<LobbyEntryResult>> pendingRequest in pendingEntryRequests)
        {
            pendingRequest.Value.TrySetResult(LobbyEntryResult.Failed(LobbyEntryFailureType.NetworkConnectionFailed, "The network connection was closed."));
        }

        foreach (KeyValuePair<string, TaskCompletionSource<LobbyExitResult>> pendingRequest in pendingExitRequests)
        {
            pendingRequest.Value.TrySetResult(LobbyExitResult.Failed(string.Empty, LobbyPlayerExitReason.Disconnected, "The network connection was closed."));
        }

        foreach (KeyValuePair<string, TaskCompletionSource<bool>> pendingRequest in pendingHostSettingsRequests)
        {
            pendingRequest.Value.TrySetResult(false);
        }

        pendingEntryRequests.Clear();
        pendingExitRequests.Clear();
        pendingHostSettingsRequests.Clear();
    }

    #endregion
}
