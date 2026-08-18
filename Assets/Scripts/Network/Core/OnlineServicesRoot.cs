using System;
using UnityEngine;

[DefaultExecutionOrder(-1100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(OnlineConnectionManager))]
[RequireComponent(typeof(OnlineServicesStartup))]
[RequireComponent(typeof(OnlineServicesLifecycle))]
public class OnlineServicesRoot : MonoBehaviour
{
    #region Fields

    public static OnlineServicesRoot instance;

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    private OnlineConnectionManager connectionManager;
    private OnlineServicesStartup startup;
    private OnlineServicesLifecycle lifecycle;

    private bool isPrimaryInstance;
    private bool isReady;

    public bool IsPrimaryInstance => isPrimaryInstance;
    public bool IsReady => isReady;

    public OnlineConnectionManager ConnectionManager => connectionManager;

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
            Debug.LogWarning("OnlineServicesRoot must be a root GameObject for DontDestroyOnLoad to work.");
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (isPrimaryInstance)
        {
            InitializeOnlineServicesRoot();
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

    private void InitializeOnlineServicesRoot()
    {
        connectionManager = GetComponent<OnlineConnectionManager>();
        startup = GetComponent<OnlineServicesStartup>();
        lifecycle = GetComponent<OnlineServicesLifecycle>();

        if (connectionManager == null || !connectionManager.Initialize())
        {
            Debug.LogError("OnlineServicesRoot could not initialize OnlineConnectionManager.");
            return;
        }

        if (lifecycle == null || !lifecycle.Initialize())
        {
            Debug.LogError("OnlineServicesRoot could not initialize OnlineServicesLifecycle.");
            return;
        }

        if (startup == null || !startup.Initialize())
        {
            Debug.LogError("OnlineServicesRoot could not initialize OnlineServicesStartup.");
            return;
        }

        isReady = true;
        Ready?.Invoke();
    }

    #endregion
}