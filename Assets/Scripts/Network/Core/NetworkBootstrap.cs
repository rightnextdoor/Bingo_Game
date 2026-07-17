using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using UnityEngine;

[DefaultExecutionOrder(-1160)]
[DisallowMultipleComponent]
public class NetworkBootstrap : MonoBehaviour
{
    private const float ShutdownTimeoutSeconds = 10f;

    public static NetworkBootstrap instance;

    private bool isReady;
    private bool isManualShutdown;

    private NetworkRoot networkRoot;
    private NetworkManager networkManager;
    private UnityTransport unityTransport;

    private RelayConnectionService relayConnectionService;
    private NetworkConnectionApproval connectionApproval;
    private NetworkConnectionRegistry connectionRegistry;
    private NetworkRuntimeConfigData runtimeConfig;

    private Coroutine shutdownRoutine;

    private NetworkConnectionMode connectionMode =
        NetworkConnectionMode.Offline;

    private NetworkConnectionState connectionState =
        NetworkConnectionState.Offline;

    private string relayJoinCode = string.Empty;

    public bool IsReady => isReady;

    public NetworkConnectionMode ConnectionMode =>
        connectionMode;

    public NetworkConnectionState ConnectionState =>
        connectionState;

    public string RelayJoinCode => relayJoinCode;

    public bool IsConnected =>
        networkManager != null &&
        networkManager.IsListening &&
        connectionState ==
            NetworkConnectionState.Connected;

    public bool IsAuthority =>
        networkManager != null &&
        networkManager.IsListening &&
        networkManager.IsServer;

    public bool IsHost =>
        networkManager != null &&
        networkManager.IsListening &&
        networkManager.IsHost;

    public bool IsClient =>
        networkManager != null &&
        networkManager.IsListening &&
        networkManager.IsClient;

    public ulong LocalClientId
    {
        get
        {
            if (networkManager == null ||
                !networkManager.IsListening)
            {
                return ulong.MaxValue;
            }

            return networkManager.LocalClientId;
        }
    }

    public event Action<NetworkConnectionMode>
        ConnectionModeChanged;

    public event Action<NetworkConnectionState>
        ConnectionStateChanged;

    public event Action<string>
        RelayJoinCodeChanged;


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
    }

    private void OnDestroy()
    {
        UnregisterNetworkCallbacks();

        if (instance == this)
        {
            instance = null;
        }
    }

    public bool Initialize()
    {
        if (isReady)
        {
            return true;
        }

        networkRoot = NetworkRoot.instance;

        if (networkRoot == null)
        {
            Debug.LogError(
                "NetworkBootstrap could not initialize because NetworkRoot.instance is null.");

            return false;
        }

        runtimeConfig = networkRoot.RuntimeConfig;

        if (runtimeConfig == null)
        {
            Debug.LogError(
                "NetworkBootstrap could not initialize because NetworkRuntimeConfigData is missing.");

            return false;
        }

        networkManager =
            networkRoot.GetComponent<NetworkManager>();

        unityTransport =
            networkRoot.GetComponent<UnityTransport>();

        if (networkManager == null)
        {
            Debug.LogError(
                "NetworkBootstrap could not initialize because NetworkManager is missing.");

            return false;
        }

        if (unityTransport == null)
        {
            Debug.LogError(
                "NetworkBootstrap could not initialize because UnityTransport is missing.");

            return false;
        }

        relayConnectionService =
            RelayConnectionService.instance;

        connectionApproval =
            NetworkConnectionApproval.instance;

        connectionRegistry =
            NetworkConnectionRegistry.instance;

        if (relayConnectionService == null ||
            !relayConnectionService.IsReady)
        {
            Debug.LogError(
                "NetworkBootstrap could not initialize because RelayConnectionService is not ready.");

            return false;
        }

        if (connectionApproval == null ||
            !connectionApproval.IsReady)
        {
            Debug.LogError(
                "NetworkBootstrap could not initialize because NetworkConnectionApproval is not ready.");

            return false;
        }

        if (connectionRegistry == null ||
            !connectionRegistry.IsReady)
        {
            Debug.LogError(
                "NetworkBootstrap could not initialize because NetworkConnectionRegistry is not ready.");

            return false;
        }

        RegisterNetworkCallbacks();

        SetConnectionMode(
            NetworkConnectionMode.Offline);

        SetConnectionState(
            NetworkConnectionState.Offline);

        isReady = true;

        return true;
    }

    #region Offline

    public void StartOffline()
    {
        if (!isReady)
        {
            Debug.LogWarning(
                "Cannot start Offline mode because NetworkBootstrap is not ready.");

            return;
        }

        if (networkManager.IsListening)
        {
            Shutdown();
            return;
        }

        CompleteShutdown();
    }

    #endregion

    #region Direct Connection

    public bool StartDirectHost(
        string bingoUserId,
        string address = null,
        int port = -1)
    {
        if (!CanStartConnection())
        {
            return false;
        }

        if (!TryPrepareConnectionPayload(bingoUserId))
        {
            return false;
        }

        string resolvedAddress =
            ResolveDirectAddress(address);

        ushort resolvedPort =
            ResolvePort(port);

        unityTransport.SetConnectionData(
            resolvedAddress,
            resolvedPort,
            runtimeConfig.DefaultListenAddress);

        SetRelayJoinCode(string.Empty);

        SetConnectionMode(
            NetworkConnectionMode.DirectHost);

        SetConnectionState(
            NetworkConnectionState.Connecting);

        bool started =
            networkManager.StartHost();

        if (!started)
        {
            SetConnectionState(
                NetworkConnectionState.Failed);
        }

        return started;
    }

    public bool StartDirectClient(
        string bingoUserId,
        string address,
        int port = -1)
    {
        if (!CanStartConnection())
        {
            return false;
        }

        if (!TryPrepareConnectionPayload(bingoUserId))
        {
            return false;
        }

        string resolvedAddress =
            ResolveDirectAddress(address);

        ushort resolvedPort =
            ResolvePort(port);

        unityTransport.SetConnectionData(
            resolvedAddress,
            resolvedPort);

        SetRelayJoinCode(string.Empty);

        SetConnectionMode(
            NetworkConnectionMode.DirectClient);

        SetConnectionState(
            NetworkConnectionState.Connecting);

        bool started =
            networkManager.StartClient();

        if (!started)
        {
            SetConnectionState(
                NetworkConnectionState.Failed);
        }

        return started;
    }

    #endregion

    #region Relay Connection

    public async Task<bool> StartRelayHostAsync(
        string bingoUserId)
    {
        if (!CanStartConnection())
        {
            return false;
        }

        if (!TryPrepareConnectionPayload(bingoUserId))
        {
            return false;
        }

        SetConnectionMode(
            NetworkConnectionMode.RelayHost);

        SetConnectionState(
            NetworkConnectionState.Initializing);

        try
        {
            RelayHostConnectionData connectionData =
                await relayConnectionService
                    .CreateHostConnectionAsync();

            unityTransport.SetRelayServerData(
                connectionData.ServerData);

            SetRelayJoinCode(
                connectionData.JoinCode);

            SetConnectionState(
                NetworkConnectionState.Connecting);

            bool started =
                networkManager.StartHost();

            if (!started)
            {
                SetConnectionState(
                    NetworkConnectionState.Failed);
            }

            return started;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            SetRelayJoinCode(string.Empty);

            SetConnectionState(
                NetworkConnectionState.Failed);

            return false;
        }
    }

    public async Task<bool> StartRelayClientAsync(
        string bingoUserId,
        string joinCode)
    {
        if (!CanStartConnection())
        {
            return false;
        }

        if (!TryPrepareConnectionPayload(bingoUserId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogWarning(
                "Cannot start Relay Client because the join code is empty.");

            return false;
        }

        SetConnectionMode(
            NetworkConnectionMode.RelayClient);

        SetConnectionState(
            NetworkConnectionState.Initializing);

        try
        {
            RelayServerData relayServerData =
                await relayConnectionService
                    .JoinConnectionAsync(joinCode);

            unityTransport.SetRelayServerData(
                relayServerData);

            SetRelayJoinCode(
                joinCode.Trim().ToUpperInvariant());

            SetConnectionState(
                NetworkConnectionState.Connecting);

            bool started =
                networkManager.StartClient();

            if (!started)
            {
                SetConnectionState(
                    NetworkConnectionState.Failed);
            }

            return started;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            SetRelayJoinCode(string.Empty);

            SetConnectionState(
                NetworkConnectionState.Failed);

            return false;
        }
    }

    #endregion

    #region Shutdown

    public async Task<bool> ShutdownAsync()
    {
        if (!isReady)
        {
            return false;
        }

        Shutdown();

        float timeoutTime =
            Time.realtimeSinceStartup +
            ShutdownTimeoutSeconds;

        while (connectionState != NetworkConnectionState.Offline)
        {
            if (Time.realtimeSinceStartup >= timeoutTime)
            {
                return false;
            }

            await Task.Yield();
        }

        return true;
    }

    public void Shutdown()
    {
        if (!isReady)
        {
            return;
        }

        if (shutdownRoutine != null)
        {
            return;
        }

        if (!networkManager.IsListening)
        {
            CompleteShutdown();
            return;
        }

        isManualShutdown = true;

        SetConnectionState(
            NetworkConnectionState.Disconnecting);

        networkManager.Shutdown();

        shutdownRoutine =
            StartCoroutine(WaitForShutdown());
    }

    private IEnumerator WaitForShutdown()
    {
        while (networkManager != null &&
               networkManager.IsListening)
        {
            yield return null;
        }

        shutdownRoutine = null;

        CompleteShutdown();
    }

    private void CompleteShutdown()
    {
        isManualShutdown = false;

        if (connectionRegistry != null &&
            connectionRegistry.IsReady)
        {
            connectionRegistry.ClearConnections();
        }

        SetRelayJoinCode(string.Empty);

        SetConnectionMode(
            NetworkConnectionMode.Offline);

        SetConnectionState(
            NetworkConnectionState.Offline);
    }

    #endregion

    #region Network Callbacks

    private void RegisterNetworkCallbacks()
    {
        if (networkManager == null)
        {
            return;
        }

        networkManager.OnClientConnectedCallback -=
            OnClientConnected;

        networkManager.OnClientDisconnectCallback -=
            OnClientDisconnected;

        networkManager.OnClientConnectedCallback +=
            OnClientConnected;

        networkManager.OnClientDisconnectCallback +=
            OnClientDisconnected;
    }

    private void UnregisterNetworkCallbacks()
    {
        if (networkManager == null)
        {
            return;
        }

        networkManager.OnClientConnectedCallback -=
            OnClientConnected;

        networkManager.OnClientDisconnectCallback -=
            OnClientDisconnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (networkManager == null)
        {
            return;
        }

        if (networkManager.IsServer &&
            clientId != networkManager.LocalClientId)
        {
            return;
        }

        SetConnectionState(
            NetworkConnectionState.Connected);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (isManualShutdown)
        {
            return;
        }

        if (networkManager == null)
        {
            return;
        }

        if (networkManager.IsServer &&
            networkManager.IsClient &&
            clientId != networkManager.LocalClientId)
        {
            return;
        }

        if (connectionState ==
                NetworkConnectionState.Connecting ||
            connectionState ==
                NetworkConnectionState.Initializing)
        {
            SetConnectionState(
                NetworkConnectionState.Failed);

            return;
        }

        SetConnectionState(
            NetworkConnectionState.Disconnected);
    }

    #endregion

    #region Validation

    private bool CanStartConnection()
    {
        if (!isReady)
        {
            Debug.LogWarning(
                "Cannot start network connection because NetworkBootstrap is not ready.");

            return false;
        }

        if (networkManager == null)
        {
            Debug.LogWarning(
                "Cannot start network connection because NetworkManager is missing.");

            return false;
        }

        if (networkManager.IsListening)
        {
            Debug.LogWarning(
                "Cannot start another network connection while NetworkManager is already running.");

            return false;
        }

        if (connectionState ==
                NetworkConnectionState.Initializing ||
            connectionState ==
                NetworkConnectionState.Connecting ||
            connectionState ==
                NetworkConnectionState.Disconnecting)
        {
            Debug.LogWarning(
                "Cannot start another network connection while a connection operation is already running.");

            return false;
        }

        return true;
    }

    private bool TryPrepareConnectionPayload(
        string bingoUserId)
    {
        if (string.IsNullOrWhiteSpace(bingoUserId))
        {
            Debug.LogWarning(
                "Cannot start network connection because Bingo UserId is missing.");

            return false;
        }

        string normalizedBingoUserId =
            bingoUserId.Trim();

        if (normalizedBingoUserId.Length >
            runtimeConfig.MaximumBingoUserIdLength)
        {
            Debug.LogWarning(
                "Cannot start network connection because Bingo UserId is too long.");

            return false;
        }

        NetworkConnectionPayload payload =
            new NetworkConnectionPayload(
                runtimeConfig.ProtocolVersion,
                normalizedBingoUserId);

        byte[] payloadBytes =
            payload.ToBytes();

        if (payloadBytes.Length >
            runtimeConfig.MaximumApprovalPayloadBytes)
        {
            Debug.LogWarning(
                "Cannot start network connection because the connection payload is too large.");

            return false;
        }

        networkManager.NetworkConfig.ConnectionData =
            payloadBytes;

        return true;
    }

    #endregion

    #region Helpers

    private string ResolveDirectAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return runtimeConfig.DefaultDirectAddress;
        }

        return address.Trim();
    }

    private ushort ResolvePort(int port)
    {
        if (port < 1 || port > 65535)
        {
            return runtimeConfig.DefaultPort;
        }

        return (ushort)port;
    }

    private void SetConnectionMode(
        NetworkConnectionMode newMode)
    {
        if (connectionMode == newMode)
        {
            return;
        }

        connectionMode = newMode;

        ConnectionModeChanged?.Invoke(
            connectionMode);
    }

    private void SetConnectionState(
        NetworkConnectionState newState)
    {
        if (connectionState == newState)
        {
            return;
        }

        connectionState = newState;

        ConnectionStateChanged?.Invoke(
            connectionState);
    }

    private void SetRelayJoinCode(
        string newJoinCode)
    {
        newJoinCode ??= string.Empty;

        if (relayJoinCode == newJoinCode)
        {
            return;
        }

        relayJoinCode = newJoinCode;

        RelayJoinCodeChanged?.Invoke(
            relayJoinCode);
    }

    #endregion
}