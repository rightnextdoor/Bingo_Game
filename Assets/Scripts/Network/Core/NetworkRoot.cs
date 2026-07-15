using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

[DefaultExecutionOrder(-1200)]
[DisallowMultipleComponent]
public class NetworkRoot : MonoBehaviour
{
    public static NetworkRoot instance;

    private bool isPrimaryInstance;
    private bool isReady;

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Configuration")]
    [SerializeField]
    private NetworkRuntimeConfigData runtimeConfig;

    private NetworkManager networkManager;
    private UnityTransport unityTransport;

    public bool IsPrimaryInstance => isPrimaryInstance;
    public bool IsReady => isReady;

    public NetworkRuntimeConfigData RuntimeConfig =>
        runtimeConfig;

    public event Action Ready;


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

        if (dontDestroyOnLoad)
        {
            if (transform.parent != null)
            {
                Debug.LogWarning(
                    "NetworkRoot must be a root GameObject for DontDestroyOnLoad to work.");
            }
            else
            {
                DontDestroyOnLoad(gameObject);
            }
        }
    }

    private void Start()
    {
        if (!isPrimaryInstance)
        {
            return;
        }

        InitializeNetworkRoot();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void InitializeNetworkRoot()
    {
        networkManager =
            GetComponent<NetworkManager>();

        unityTransport =
            GetComponent<UnityTransport>();

        if (runtimeConfig == null)
        {
            Debug.LogError(
                "NetworkRoot could not initialize because NetworkRuntimeConfigData is not assigned.");

            return;
        }

        if (networkManager == null)
        {
            Debug.LogError(
                "NetworkRoot could not initialize because NetworkManager is missing.");

            return;
        }

        if (unityTransport == null)
        {
            Debug.LogError(
                "NetworkRoot could not initialize because UnityTransport is missing.");

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

        if (!InitializeAuthorityRuntime())
        {
            return;
        }

        isReady = true;

        Ready?.Invoke();
    }

    private bool InitializeRelayConnectionService()
    {
        RelayConnectionService relayConnectionService =
            RelayConnectionService.instance;

        if (relayConnectionService == null)
        {
            Debug.LogError(
                "NetworkRoot could not initialize because RelayConnectionService.instance is null.");

            return false;
        }

        return relayConnectionService.Initialize();
    }

    private bool InitializeConnectionRegistry()
    {
        NetworkConnectionRegistry connectionRegistry =
            NetworkConnectionRegistry.instance;

        if (connectionRegistry == null)
        {
            Debug.LogError(
                "NetworkRoot could not initialize because NetworkConnectionRegistry.instance is null.");

            return false;
        }

        return connectionRegistry.Initialize();
    }

    private bool InitializeConnectionApproval()
    {
        NetworkConnectionApproval connectionApproval =
            NetworkConnectionApproval.instance;

        if (connectionApproval == null)
        {
            Debug.LogError(
                "NetworkRoot could not initialize because NetworkConnectionApproval.instance is null.");

            return false;
        }

        return connectionApproval.Initialize();
    }

    private bool InitializeNetworkBootstrap()
    {
        NetworkBootstrap networkBootstrap =
            NetworkBootstrap.instance;

        if (networkBootstrap == null)
        {
            Debug.LogError(
                "NetworkRoot could not initialize because NetworkBootstrap.instance is null.");

            return false;
        }

        return networkBootstrap.Initialize();
    }

    private bool InitializeAuthorityRuntime()
    {
        NetworkAuthorityRuntime authorityRuntime =
            NetworkAuthorityRuntime.instance;

        if (authorityRuntime == null)
        {
            Debug.LogError(
                "NetworkRoot could not initialize because NetworkAuthorityRuntime.instance is null.");

            return false;
        }

        return authorityRuntime.Initialize();
    }
}