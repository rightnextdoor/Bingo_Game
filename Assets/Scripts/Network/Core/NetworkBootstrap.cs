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
    #region Fields

    private const float ShutdownTimeoutSeconds = 10f;

    public static NetworkBootstrap instance;

    private bool isReady;
    private bool isManualShutdown;

    private NetworkRoot networkRoot;
    private NetworkManager networkManager;
    private UnityTransport unityTransport;
    private RelayConnectionService relayConnectionService;
    private NetworkConnectionRegistry connectionRegistry;
    private NetworkRuntimeConfigData runtimeConfig;

    private Coroutine shutdownRoutine;

    private NetworkConnectionMode connectionMode = NetworkConnectionMode.Offline;
    private NetworkConnectionState connectionState = NetworkConnectionState.Offline;
    private string relayJoinCode = string.Empty;

    public bool IsReady => isReady;
    public NetworkConnectionMode ConnectionMode => connectionMode;
    public NetworkConnectionState ConnectionState => connectionState;
    public string RelayJoinCode => relayJoinCode;
    public bool IsConnected => networkManager != null && networkManager.IsListening && connectionState == NetworkConnectionState.Connected;
    public bool IsAuthority => networkManager != null && networkManager.IsListening && networkManager.IsServer;
    public bool IsHost => networkManager != null && networkManager.IsListening && networkManager.IsHost;
    public bool IsClient => networkManager != null && networkManager.IsListening && networkManager.IsClient;

    public ulong LocalClientId
    {
        get
        {
            if (networkManager == null || !networkManager.IsListening)
            {
                return ulong.MaxValue;
            }

            return networkManager.LocalClientId;
        }
    }

    public event Action<NetworkConnectionMode> ConnectionModeChanged;
    public event Action<NetworkConnectionState> ConnectionStateChanged;
    public event Action<string> RelayJoinCodeChanged;

    #endregion

    #region Unity Methods

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

    #endregion

    #region Initialization

    public bool Initialize()
    {
        if (isReady)
        {
            return true;
        }

        networkRoot = NetworkRoot.instance;

        if (networkRoot == null)
        {
            Debug.LogError("NetworkBootstrap could not initialize because NetworkRoot.instance is null.");
            return false;
        }

        runtimeConfig = networkRoot.RuntimeConfig;
        networkManager = networkRoot.GetComponent<NetworkManager>();
        unityTransport = networkRoot.GetComponent<UnityTransport>();
        relayConnectionService = networkRoot.GetComponent<RelayConnectionService>();
        connectionRegistry = networkRoot.GetComponent<NetworkConnectionRegistry>();
        NetworkConnectionApproval connectionApproval = networkRoot.GetComponent<NetworkConnectionApproval>();

        if (runtimeConfig == null)
        {
            Debug.LogError("NetworkBootstrap could not initialize because NetworkRuntimeConfigData is missing.");
            return false;
        }

        if (networkManager == null)
        {
            Debug.LogError("NetworkBootstrap could not initialize because NetworkManager is missing.");
            return false;
        }

        if (unityTransport == null)
        {
            Debug.LogError("NetworkBootstrap could not initialize because UnityTransport is missing.");
            return false;
        }

        if (connectionApproval == null || !connectionApproval.IsReady)
        {
            Debug.LogError("NetworkBootstrap could not initialize because NetworkConnectionApproval is not ready.");
            return false;
        }

        if (connectionRegistry == null || !connectionRegistry.IsReady)
        {
            Debug.LogError("NetworkBootstrap could not initialize because NetworkConnectionRegistry is not ready.");
            return false;
        }

        RegisterNetworkCallbacks();

        SetConnectionMode(NetworkConnectionMode.Offline);
        SetConnectionState(NetworkConnectionState.Offline);

        isReady = true;
        return true;
    }

    #endregion

    #region Offline

    public void StartOffline()
    {
        if (!isReady)
        {
            Debug.LogWarning("Cannot start Offline mode because NetworkBootstrap is not ready.");
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

    public bool StartDirectHost(string bingoUserId, string address = null, int port = -1)
    {
        if (!CanStartConnection() || !TryPrepareConnectionPayload(bingoUserId))
        {
            return false;
        }

        string resolvedAddress = ResolveDirectAddress(address);
        ushort resolvedPort = ResolvePort(port);

        unityTransport.SetConnectionData(resolvedAddress, resolvedPort, runtimeConfig.DefaultListenAddress);

        SetRelayJoinCode(string.Empty);
        SetConnectionMode(NetworkConnectionMode.DirectHost);
        SetConnectionState(NetworkConnectionState.Connecting);

        bool started = networkManager.StartHost();

        if (!started)
        {
            SetConnectionState(NetworkConnectionState.Failed);
        }

        return started;
    }

    public bool StartDirectClient(string bingoUserId, string address, int port = -1)
    {
        if (!CanStartConnection() || !TryPrepareConnectionPayload(bingoUserId))
        {
            return false;
        }

        string resolvedAddress = ResolveDirectAddress(address);
        ushort resolvedPort = ResolvePort(port);

        unityTransport.SetConnectionData(resolvedAddress, resolvedPort);

        SetRelayJoinCode(string.Empty);
        SetConnectionMode(NetworkConnectionMode.DirectClient);
        SetConnectionState(NetworkConnectionState.Connecting);

        bool started = networkManager.StartClient();

        if (!started)
        {
            SetConnectionState(NetworkConnectionState.Failed);
        }

        return started;
    }

    #endregion

    #region Relay Connection

    public async Task<bool> StartRelayHostAsync(string bingoUserId)
    {
        if (!CanStartConnection() || !CanUseRelay() || !TryPrepareConnectionPayload(bingoUserId))
        {
            return false;
        }

        SetConnectionMode(NetworkConnectionMode.RelayHost);
        SetConnectionState(NetworkConnectionState.Initializing);

        try
        {
            RelayHostConnectionData connectionData = await relayConnectionService.CreateHostConnectionAsync();

            unityTransport.SetRelayServerData(connectionData.ServerData);

            SetRelayJoinCode(connectionData.JoinCode);
            SetConnectionState(NetworkConnectionState.Connecting);

            bool started = networkManager.StartHost();

            if (!started)
            {
                SetConnectionState(NetworkConnectionState.Failed);
            }

            return started;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SetRelayJoinCode(string.Empty);
            SetConnectionState(NetworkConnectionState.Failed);
            return false;
        }
    }

    public async Task<bool> StartRelayClientAsync(string bingoUserId, string joinCode)
    {
        if (!CanStartConnection() || !CanUseRelay() || !TryPrepareConnectionPayload(bingoUserId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogWarning("Cannot start Relay Client because the join code is empty.");
            return false;
        }

        SetConnectionMode(NetworkConnectionMode.RelayClient);
        SetConnectionState(NetworkConnectionState.Initializing);

        try
        {
            RelayServerData relayServerData = await relayConnectionService.JoinConnectionAsync(joinCode);

            unityTransport.SetRelayServerData(relayServerData);

            SetRelayJoinCode(joinCode.Trim().ToUpperInvariant());
            SetConnectionState(NetworkConnectionState.Connecting);

            bool started = networkManager.StartClient();

            if (!started)
            {
                SetConnectionState(NetworkConnectionState.Failed);
            }

            return started;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SetRelayJoinCode(string.Empty);
            SetConnectionState(NetworkConnectionState.Failed);
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

        float timeoutTime = Time.realtimeSinceStartup + ShutdownTimeoutSeconds;

        while (connectionState != NetworkConnectionState.Offline ||
               (networkManager != null && (networkManager.IsListening || networkManager.ShutdownInProgress)))
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
        if (!isReady || shutdownRoutine != null)
        {
            return;
        }

        if (networkManager == null)
        {
            CompleteShutdown();
            return;
        }

        if (networkManager.ShutdownInProgress)
        {
            isManualShutdown = true;
            SetConnectionState(NetworkConnectionState.Disconnecting);
            shutdownRoutine = StartCoroutine(WaitForShutdown());
            return;
        }

        if (!networkManager.IsListening)
        {
            CompleteShutdown();
            return;
        }

        isManualShutdown = true;
        SetConnectionState(NetworkConnectionState.Disconnecting);

        networkManager.Shutdown();
        shutdownRoutine = StartCoroutine(WaitForShutdown());
    }

    private IEnumerator WaitForShutdown()
    {
        while (networkManager != null && (networkManager.IsListening || networkManager.ShutdownInProgress))
        {
            yield return null;
        }

        shutdownRoutine = null;
        CompleteShutdown();
    }

    private void CompleteShutdown()
    {
        isManualShutdown = false;

        if (connectionRegistry != null && connectionRegistry.IsReady)
        {
            connectionRegistry.ClearConnections();
        }

        SetRelayJoinCode(string.Empty);
        SetConnectionMode(NetworkConnectionMode.Offline);
        SetConnectionState(NetworkConnectionState.Offline);
    }

    #endregion

    #region Network Callbacks

    private void RegisterNetworkCallbacks()
    {
        if (networkManager == null)
        {
            return;
        }

        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnected;

        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void UnregisterNetworkCallbacks()
    {
        if (networkManager == null)
        {
            return;
        }

        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (networkManager == null)
        {
            return;
        }

        if (networkManager.IsServer && clientId != networkManager.LocalClientId)
        {
            return;
        }

        SetConnectionState(NetworkConnectionState.Connected);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (isManualShutdown || networkManager == null)
        {
            return;
        }

        if (networkManager.IsServer && networkManager.IsClient && clientId != networkManager.LocalClientId)
        {
            return;
        }

        if (connectionState == NetworkConnectionState.Connecting || connectionState == NetworkConnectionState.Initializing)
        {
            SetConnectionState(NetworkConnectionState.Failed);
            return;
        }

        SetConnectionState(NetworkConnectionState.Disconnected);
    }

    #endregion

    #region Development Testing

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool SimulateUnexpectedClientDisconnectForTesting()
    {
        if (!isReady || networkManager == null || !networkManager.IsListening || !networkManager.IsClient || networkManager.IsHost)
        {
            return false;
        }

        networkManager.Shutdown(true);
        return true;
    }
#endif

    #endregion

    #region Validation

    private bool CanStartConnection()
    {
        if (!isReady)
        {
            Debug.LogWarning("Cannot start network connection because NetworkBootstrap is not ready.");
            return false;
        }

        if (networkManager == null)
        {
            Debug.LogWarning("Cannot start network connection because NetworkManager is missing.");
            return false;
        }

        if (networkManager.IsListening)
        {
            Debug.LogWarning("Cannot start another network connection while NetworkManager is already running.");
            return false;
        }

        if (networkManager.ShutdownInProgress ||
            connectionState == NetworkConnectionState.Initializing ||
            connectionState == NetworkConnectionState.Connecting ||
            connectionState == NetworkConnectionState.Disconnecting)
        {
            Debug.LogWarning("Cannot start another network connection while a connection operation is already running.");
            return false;
        }

        return true;
    }

    private bool CanUseRelay()
    {
        if (relayConnectionService != null && relayConnectionService.IsReady)
        {
            return true;
        }

        Debug.LogWarning("Cannot start Relay connection because RelayConnectionService is not available or ready.");
        return false;
    }

    private bool TryPrepareConnectionPayload(string bingoUserId)
    {
        if (string.IsNullOrWhiteSpace(bingoUserId))
        {
            Debug.LogWarning("Cannot start network connection because Bingo UserId is missing.");
            return false;
        }

        string normalizedBingoUserId = bingoUserId.Trim();

        if (normalizedBingoUserId.Length > runtimeConfig.MaximumBingoUserIdLength)
        {
            Debug.LogWarning("Cannot start network connection because Bingo UserId is too long.");
            return false;
        }

        NetworkConnectionPayload payload = new NetworkConnectionPayload(runtimeConfig.ProtocolVersion, normalizedBingoUserId);
        byte[] payloadBytes = payload.ToBytes();

        if (payloadBytes.Length > runtimeConfig.MaximumApprovalPayloadBytes)
        {
            Debug.LogWarning("Cannot start network connection because the connection payload is too large.");
            return false;
        }

        networkManager.NetworkConfig.ConnectionData = payloadBytes;
        return true;
    }

    #endregion

    #region Helpers

    private string ResolveDirectAddress(string address)
    {
        return string.IsNullOrWhiteSpace(address) ? runtimeConfig.DefaultDirectAddress : address.Trim();
    }

    private ushort ResolvePort(int port)
    {
        return port < 1 || port > 65535 ? runtimeConfig.DefaultPort : (ushort)port;
    }

    private void SetConnectionMode(NetworkConnectionMode newMode)
    {
        if (connectionMode == newMode)
        {
            return;
        }

        connectionMode = newMode;
        ConnectionModeChanged?.Invoke(connectionMode);
    }

    private void SetConnectionState(NetworkConnectionState newState)
    {
        if (connectionState == newState)
        {
            return;
        }

        connectionState = newState;
        ConnectionStateChanged?.Invoke(connectionState);
    }

    private void SetRelayJoinCode(string newJoinCode)
    {
        newJoinCode ??= string.Empty;

        if (relayJoinCode == newJoinCode)
        {
            return;
        }

        relayJoinCode = newJoinCode;
        RelayJoinCodeChanged?.Invoke(relayJoinCode);
    }

    #endregion
}
