using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

[DefaultExecutionOrder(-1200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
[RequireComponent(typeof(UnityTransport))]
public class NetworkRoot : MonoBehaviour
{
    #region Fields

    public static NetworkRoot instance;

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Configuration")]
    [SerializeField] private NetworkRuntimeConfigData runtimeConfig;

    private bool isPrimaryInstance;
    private bool isReady;
    private NetworkManager networkManager;
    private UnityTransport unityTransport;

    public bool IsPrimaryInstance => isPrimaryInstance;
    public bool IsReady => isReady;
    public NetworkRuntimeConfigData RuntimeConfig => runtimeConfig;

    public event Action Ready;

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
            isPrimaryInstance = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        instance = this;
        isPrimaryInstance = true;

        if (!dontDestroyOnLoad)
        {
            return;
        }

        if (transform.parent != null)
        {
            Debug.LogWarning("NetworkRoot must be a root GameObject for DontDestroyOnLoad to work.");
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (isPrimaryInstance)
        {
            InitializeNetworkRoot();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Initialization

    private void InitializeNetworkRoot()
    {
        networkManager = GetComponent<NetworkManager>();
        unityTransport = GetComponent<UnityTransport>();

        if (!ValidateRequiredConfiguration())
        {
            return;
        }

        if (!InitializeRelayConnectionService())
        {
            return;
        }

        if (!InitializeConnectionRegistry())
        {
            return;
        }

        if (!InitializeConnectionApproval())
        {
            return;
        }

        if (!InitializeNetworkBootstrap())
        {
            return;
        }

        isReady = true;
        Ready?.Invoke();
    }

    private bool ValidateRequiredConfiguration()
    {
        if (runtimeConfig == null)
        {
            Debug.LogError("NetworkRoot could not initialize because NetworkRuntimeConfigData is not assigned.");
            return false;
        }

        if (networkManager == null)
        {
            Debug.LogError("NetworkRoot could not initialize because NetworkManager is missing.");
            return false;
        }

        if (unityTransport == null)
        {
            Debug.LogError("NetworkRoot could not initialize because UnityTransport is missing.");
            return false;
        }

        return true;
    }

    private bool InitializeRelayConnectionService()
    {
        RelayConnectionService relayConnectionService = GetComponent<RelayConnectionService>();

        if (relayConnectionService == null)
        {
            return true;
        }

        if (relayConnectionService.Initialize())
        {
            return true;
        }

        Debug.LogError("NetworkRoot could not initialize RelayConnectionService.");
        return false;
    }

    private bool InitializeConnectionRegistry()
    {
        NetworkConnectionRegistry connectionRegistry = GetComponent<NetworkConnectionRegistry>();

        if (connectionRegistry == null)
        {
            Debug.LogError("NetworkRoot could not initialize because NetworkConnectionRegistry is missing.");
            return false;
        }

        return connectionRegistry.Initialize();
    }

    private bool InitializeConnectionApproval()
    {
        NetworkConnectionApproval connectionApproval = GetComponent<NetworkConnectionApproval>();

        if (connectionApproval == null)
        {
            Debug.LogError("NetworkRoot could not initialize because NetworkConnectionApproval is missing.");
            return false;
        }

        return connectionApproval.Initialize();
    }

    private bool InitializeNetworkBootstrap()
    {
        NetworkBootstrap networkBootstrap = GetComponent<NetworkBootstrap>();

        if (networkBootstrap == null)
        {
            Debug.LogError("NetworkRoot could not initialize because NetworkBootstrap is missing.");
            return false;
        }

        return networkBootstrap.Initialize();
    }

    #endregion
}
