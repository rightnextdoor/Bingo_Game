using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkLobbyService : MonoBehaviour, ILobbyService
{
    #region Fields

    public static NetworkLobbyService instance;

    private const float ConnectionTimeoutSeconds = 30f;
    private const float LobbyConnectionTimeoutSeconds = 15f;
    private const float ExitNotificationDeliverySeconds = 0.15f;

    private bool isReady;
    private NetworkBootstrap networkBootstrap;
    private readonly SemaphoreSlim operationGate = new SemaphoreSlim(1, 1);

    public SessionRuntimeType RuntimeType => SessionRuntimeType.Network;
    public bool IsReady => isReady;

    #endregion

    #region Unity Methods

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
        while (!CanInitialize())
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

    #endregion

    #region Lobby Entry

    public async Task<LobbyEntryResult> EnterLobbyAsync(LobbySetupData lobbySetupData)
    {
        return await EnterLobbyAsync(lobbySetupData, CancellationToken.None);
    }

    public async Task<LobbyEntryResult> EnterLobbyAsync(LobbySetupData _lobbySetupData, CancellationToken _cancellationToken)
    {
        await operationGate.WaitAsync(_cancellationToken);
        try
        {
            LobbyEntryResult preparation = await PrepareConnectionCoreAsync(_lobbySetupData, _cancellationToken);
            if (!preparation.success)
            {
                return preparation;
            }

            _cancellationToken.ThrowIfCancellationRequested();
            NetworkLobbyConnection connection = NetworkLobbyConnection.GetLocalConnection();
            if (connection == null)
            {
                return LobbyEntryResult.Failed(LobbyEntryFailureType.NetworkLobbyConnectionUnavailable, "The network lobby connection was not available.");
            }

            LobbyEntryResult result;
            try
            {
                result = await connection.RequestEnterLobbyAsync(_lobbySetupData);
            }
            catch
            {
                await TryRollbackFailedLobbyEntryAsync(connection);
                throw;
            }
            if (_cancellationToken.IsCancellationRequested || result == null || !result.success)
            {
                await TryRollbackFailedLobbyEntryAsync(connection);
            }

            _cancellationToken.ThrowIfCancellationRequested();
            return result ?? LobbyEntryResult.Failed(LobbyEntryFailureType.Unknown, "The network lobby did not return a result.");
        }
        finally
        {
            operationGate.Release();
        }
    }

    private async Task<LobbyEntryResult> PrepareConnectionCoreAsync(LobbySetupData lobbySetupData, CancellationToken _cancellationToken)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (!isReady)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.ServiceUnavailable, "The network lobby service is not ready.");
        }

        if (!IsValidNetworkSetup(lobbySetupData))
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.InvalidSetupData, "The network lobby setup data is invalid.");
        }

        if (!TryResolveConnectionTarget(lobbySetupData, out ConnectionTarget target, out LobbyEntryResult lookupFailure))
        {
            return lookupFailure;
        }

        if (!await EnsureNetworkConnectionAsync(target, _cancellationToken))
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.NetworkConnectionFailed, "The network connection could not be created.");
        }

        _cancellationToken.ThrowIfCancellationRequested();
        NetworkLobbyConnection lobbyConnection = await WaitForLocalLobbyConnectionAsync(_cancellationToken);

        if (lobbyConnection == null)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.NetworkLobbyConnectionUnavailable, "The network lobby connection was not available.");
        }

        return new LobbyEntryResult { success = true, failureType = LobbyEntryFailureType.None };
    }

    private bool TryResolveConnectionTarget(LobbySetupData _setupData, out ConnectionTarget _target, out LobbyEntryResult _failureResult)
    {
        _target = default;
        _failureResult = null;
        string userId = _setupData.userData.userId;

        if (_setupData.isGameSimulation || MultiplayerPlayModeTestContext.IsActive)
        {
            bool shouldHost = _setupData.isGameSimulation
                ? _setupData.gameSimulationPlayerNumber == 1
                : MultiplayerPlayModeTestContext.IsHost;
            _target = new ConnectionTarget(shouldHost ? NetworkConnectionMode.DirectHost : NetworkConnectionMode.DirectClient,
                userId, string.Empty, MultiplayerPlayModeTestContext.DirectAddress);
            return true;
        }

        if (_setupData.playMode == MainMenuPlayMode.Online ||
            _setupData.customSetupData.actionType == CustomLobbyActionType.HostLobby)
        {
            _target = new ConnectionTarget(NetworkConnectionMode.RelayHost, userId);
            return true;
        }

        string relayJoinCode = string.Empty;
        if (!HasUsableNetworkConnection() && IsCustomLobbySearch(_setupData))
        {
            NetworkLobbyManager lobbyManager = NetworkLobbyManager.instance;
            if (lobbyManager == null || !lobbyManager.IsReady)
            {
                _failureResult = LobbyEntryResult.Failed(LobbyEntryFailureType.ServiceUnavailable, "The authoritative network lobby manager is not ready.");
                return false;
            }

            if (!lobbyManager.TryResolveCustomLobbyConnection(_setupData.customSetupData.searchSetupData, out relayJoinCode, out _failureResult))
            {
                return false;
            }
        }

        _target = new ConnectionTarget(NetworkConnectionMode.RelayClient, userId, relayJoinCode);
        return true;
    }

    private async Task TryRollbackFailedLobbyEntryAsync(NetworkLobbyConnection _connection)
    {
        if (_connection == null || _connection != NetworkLobbyConnection.GetLocalConnection() ||
            networkBootstrap == null || !networkBootstrap.IsConnected)
        {
            return;
        }

        try
        {
            await _connection.RequestLeaveLobbyAsync();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NetworkLobbyService] Failed lobby entry rollback could not complete: {exception.Message}");
        }
    }

    #endregion

    #region Lobby Exit

    public async Task<LobbyExitResult> LeaveLobbyAsync(string userId)
    {
        return await LeaveLobbyAsync(userId, false);
    }

    public async Task<LobbyExitResult> LeaveLobbyAsync(string _userId, bool _keepConnection)
    {
        await operationGate.WaitAsync();
        try
        {
            return await LeaveLobbyCoreAsync(_userId, _keepConnection);
        }
        finally
        {
            operationGate.Release();
        }
    }

    private async Task<LobbyExitResult> LeaveLobbyCoreAsync(string userId, bool _keepConnection)
    {
        if (!isReady)
        {
            return LobbyExitResult.Failed(userId, LobbyPlayerExitReason.VoluntaryLeave, "The network lobby service is not ready.");
        }

        NetworkLobbyConnection lobbyConnection = NetworkLobbyConnection.GetLocalConnection();

        if (lobbyConnection == null)
        {
            return LobbyExitResult.Failed(userId, LobbyPlayerExitReason.VoluntaryLeave, "The network lobby connection was not available.");
        }

        LobbyExitResult result = await lobbyConnection.RequestLeaveLobbyAsync();

        if (result != null && result.success && !_keepConnection)
        {
            await ShutdownLocalNetworkAfterExitIfPossible(lobbyConnection);
        }

        return result ?? LobbyExitResult.Failed(userId, LobbyPlayerExitReason.VoluntaryLeave, "The network lobby did not return a leave result.");
    }

    public async Task<LobbyExitResult> KickPlayerAsync(string targetUserId)
    {
        NetworkLobbyConnection lobbyConnection = NetworkLobbyConnection.GetLocalConnection();

        if (lobbyConnection == null)
        {
            return LobbyExitResult.Failed(targetUserId, LobbyPlayerExitReason.Kicked, "The network lobby connection was not available.");
        }

        return await lobbyConnection.RequestKickPlayerAsync(targetUserId);
    }

    private async Task ShutdownLocalNetworkAfterExitIfPossible(NetworkLobbyConnection _connection)
    {
        if (MultiplayerPlayModeTestContext.IsActive || networkBootstrap == null || !networkBootstrap.IsConnected ||
            _connection == null || _connection != NetworkLobbyConnection.GetLocalConnection())
        {
            return;
        }

        if (HasAuthorityWork())
        {
            return;
        }

        float deliveryTime = Time.realtimeSinceStartup + ExitNotificationDeliverySeconds;

        while (Time.realtimeSinceStartup < deliveryTime)
        {
            await Task.Yield();
        }

        if (networkBootstrap == null || !networkBootstrap.IsConnected ||
            _connection == null || _connection != NetworkLobbyConnection.GetLocalConnection() ||
            HasAuthorityWork())
        {
            return;
        }

        await networkBootstrap.ShutdownAsync();
    }

    private bool HasAuthorityWork()
    {
        if (networkBootstrap == null || !networkBootstrap.IsAuthority)
        {
            return false;
        }

        return NetworkLobbyManager.instance?.HasActiveLobbies == true ||
               NetworkGameSessionManager.instance?.GameSessions.Count > 0 ||
               (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.ConnectedClientsIds.Count > (networkBootstrap.IsHost ? 1 : 0));
    }

    #endregion

    #region Lobby Commands

    public void SetPlayerReady(bool isReady)
    {
        NetworkLobbyConnection.GetLocalConnection()?.RequestSetPlayerReady(isReady);
    }

    public void RerollBoard()
    {
        NetworkLobbyConnection.GetLocalConnection()?.RequestRerollBoard();
    }

    public void StartLobby()
    {
        NetworkLobbyConnection.GetLocalConnection()?.RequestStartLobby();
    }

    public void NotifyLobbySceneReady()
    {
        NetworkLobbyConnection.GetLocalConnection()?.NotifyLobbySceneReady();
    }

    public void RequestLobbyInitialSync()
    {
        NetworkLobbyConnection.GetLocalConnection()?.RequestLobbyInitialSync();
    }

    public async Task<bool> ApplyHostSettingsAsync(LobbyHostSettingsData settingsData)
    {
        NetworkLobbyConnection lobbyConnection = NetworkLobbyConnection.GetLocalConnection();
        return lobbyConnection != null && await lobbyConnection.RequestApplyHostSettingsAsync(settingsData);
    }

    public void RequestLobbyResync()
    {
        NetworkLobbyConnection.GetLocalConnection()?.RequestLobbyResync();
    }

    #endregion

    #region Network Connection

    public async Task<bool> PrepareConnectionForEntryAsync(LobbySetupData lobbySetupData)
    {
        await operationGate.WaitAsync();
        try
        {
            if (!isReady || !IsValidNetworkSetup(lobbySetupData))
            {
                return false;
            }

            if (!TryResolveConnectionTarget(lobbySetupData, out ConnectionTarget target, out _))
            {
                return false;
            }

            return await EnsureNetworkConnectionAsync(target, CancellationToken.None);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<LobbyEntryResult> PrepareConnectionForEntryResultAsync(
        LobbySetupData _lobbySetupData, CancellationToken _cancellationToken)
    {
        await operationGate.WaitAsync(_cancellationToken);
        try
        {
            return await PrepareConnectionCoreAsync(_lobbySetupData, _cancellationToken);
        }
        finally
        {
            operationGate.Release();
        }
    }

    private bool HasUsableNetworkConnection()
    {
        return networkBootstrap != null && networkBootstrap.IsConnected && NetworkLobbyConnection.GetLocalConnection() != null;
    }

    private async Task<bool> EnsureNetworkConnectionAsync(ConnectionTarget _target, CancellationToken _cancellationToken)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (networkBootstrap == null || !networkBootstrap.IsReady)
        {
            return false;
        }

        if (_target.Mode == NetworkConnectionMode.DirectHost || _target.Mode == NetworkConnectionMode.DirectClient)
        {
            return await EnsureDirectTestConnectionAsync(
                _target.UserId, _target.Mode == NetworkConnectionMode.DirectHost, _target.DirectAddress, _cancellationToken);
        }

        if (networkBootstrap.IsConnected)
        {
            if (NetworkLobbyConnection.GetLocalConnection() != null)
            {
                return true;
            }

            if (HasAuthorityWork())
            {
                Debug.LogWarning("[NetworkLobbyService] The network is connected, but the local lobby connection is missing while authority lobbies are still active.");
                return false;
            }

            if (!await networkBootstrap.ShutdownAsync())
            {
                return false;
            }
        }

        _cancellationToken.ThrowIfCancellationRequested();
        bool started;

        switch (_target.Mode)
        {
            case NetworkConnectionMode.RelayHost:
                started = await networkBootstrap.StartRelayHostAsync(_target.UserId);
                break;

            case NetworkConnectionMode.RelayClient:
                if (string.IsNullOrWhiteSpace(_target.RelayJoinCode))
                {
                    return false;
                }

                started = await networkBootstrap.StartRelayClientAsync(_target.UserId, _target.RelayJoinCode);
                break;

            default:
                return false;
        }

        if (!started)
        {
            return false;
        }

        float connectionTimeoutTime = Time.realtimeSinceStartup + ConnectionTimeoutSeconds;

        while (!networkBootstrap.IsConnected)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (networkBootstrap.ConnectionState == NetworkConnectionState.Failed || networkBootstrap.ConnectionState == NetworkConnectionState.Disconnected)
            {
                return false;
            }

            if (Time.realtimeSinceStartup >= connectionTimeoutTime)
            {
                return false;
            }

            await Task.Yield();
        }

        return true;
    }

    private async Task<bool> EnsureDirectTestConnectionAsync(string userId, bool shouldHost, string _directAddress, CancellationToken _cancellationToken)
    {
        if (HasUsableNetworkConnection())
        {
            return true;
        }

        if (networkBootstrap.ConnectionState == NetworkConnectionState.Initializing ||
            networkBootstrap.ConnectionState == NetworkConnectionState.Connecting ||
            networkBootstrap.ConnectionState == NetworkConnectionState.Connected)
        {
            float existingConnectionTimeout = Time.realtimeSinceStartup + LobbyConnectionTimeoutSeconds;

            while (!HasUsableNetworkConnection() && Time.realtimeSinceStartup < existingConnectionTimeout)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (networkBootstrap.ConnectionState == NetworkConnectionState.Failed || networkBootstrap.ConnectionState == NetworkConnectionState.Disconnected)
                {
                    break;
                }

                await Task.Yield();
            }

            if (HasUsableNetworkConnection())
            {
                return true;
            }
        }

        if (shouldHost && HasAuthorityWork())
        {
            return false;
        }

        if (networkBootstrap.ConnectionState != NetworkConnectionState.Offline || networkBootstrap.IsClient || networkBootstrap.IsAuthority)
        {
            if (!await networkBootstrap.ShutdownAsync())
            {
                return false;
            }

            await Task.Yield();
        }

        _cancellationToken.ThrowIfCancellationRequested();
        bool started = shouldHost
            ? networkBootstrap.StartDirectHost(userId)
            : networkBootstrap.StartDirectClient(userId, _directAddress);

        if (!started)
        {
            return false;
        }

        float timeoutTime = Time.realtimeSinceStartup + ConnectionTimeoutSeconds;

        while (!HasUsableNetworkConnection())
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (networkBootstrap.ConnectionState == NetworkConnectionState.Failed || networkBootstrap.ConnectionState == NetworkConnectionState.Disconnected)
            {
                return false;
            }

            if (Time.realtimeSinceStartup >= timeoutTime)
            {
                return false;
            }

            await Task.Yield();
        }

        return true;
    }

    private async Task<NetworkLobbyConnection> WaitForLocalLobbyConnectionAsync(CancellationToken _cancellationToken)
    {
        float timeoutTime = Time.realtimeSinceStartup + LobbyConnectionTimeoutSeconds;

        while (Time.realtimeSinceStartup < timeoutTime)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            NetworkLobbyConnection lobbyConnection = NetworkLobbyConnection.GetLocalConnection();

            if (lobbyConnection != null)
            {
                return lobbyConnection;
            }

            await Task.Yield();
        }

        return null;
    }

    #endregion

    #region Validation

    private readonly struct ConnectionTarget
    {
        public NetworkConnectionMode Mode { get; }
        public string UserId { get; }
        public string RelayJoinCode { get; }
        public string DirectAddress { get; }

        public ConnectionTarget(NetworkConnectionMode _mode, string _userId, string _relayJoinCode = "", string _directAddress = "")
        {
            Mode = _mode;
            UserId = _userId;
            RelayJoinCode = _relayJoinCode;
            DirectAddress = _directAddress;
        }
    }

    private bool CanInitialize()
    {
        return NetworkRoot.instance != null &&
               NetworkRoot.instance.IsReady &&
               NetworkBootstrap.instance != null &&
               NetworkBootstrap.instance.IsReady &&
               NetworkLobbyManager.instance != null &&
               NetworkLobbyManager.instance.IsReady;
    }

    private bool IsValidNetworkSetup(LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null || lobbySetupData.userData == null || !lobbySetupData.userData.HasUser)
        {
            return false;
        }

        switch (lobbySetupData.playMode)
        {
            case MainMenuPlayMode.Online:
                return lobbySetupData.onlineSetupData != null;

            case MainMenuPlayMode.Custom:
                return lobbySetupData.customSetupData != null;

            default:
                return false;
        }
    }

    private bool IsCustomLobbySearch(LobbySetupData lobbySetupData)
    {
        return lobbySetupData != null &&
               lobbySetupData.playMode == MainMenuPlayMode.Custom &&
               lobbySetupData.customSetupData != null &&
               lobbySetupData.customSetupData.actionType == CustomLobbyActionType.SearchLobby;
    }

    #endregion
}
