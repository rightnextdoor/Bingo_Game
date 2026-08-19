using System;
using UnityEngine;

[DefaultExecutionOrder(-1100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(OnlineConnectionManager))]
[RequireComponent(typeof(OnlineServicesStartup))]
[RequireComponent(typeof(OnlineServicesLifecycle))]
[RequireComponent(typeof(VivoxChatService))]
[RequireComponent(typeof(ChatCommandCatalog))]
[RequireComponent(typeof(ChatSettingsManager))]
[RequireComponent(typeof(ChatCommandProcessor))]
[RequireComponent(typeof(ChatManager))]
public class OnlineServicesRoot : MonoBehaviour
{
    #region Fields

    public static OnlineServicesRoot instance;

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    private OnlineConnectionManager connectionManager;
    private OnlineServicesStartup startup;
    private OnlineServicesLifecycle lifecycle;
    private VivoxChatService vivoxChatService;
    private ChatCommandCatalog chatCommandCatalog;
    private ChatSettingsManager chatSettingsManager;
    private ChatCommandProcessor chatCommandProcessor;
    private ChatManager chatManager;

    private bool isPrimaryInstance;
    private bool isReady;

    public bool IsPrimaryInstance => isPrimaryInstance;
    public bool IsReady => isReady;

    public OnlineConnectionManager ConnectionManager => connectionManager;
    public ChatSettingsManager ChatSettingsManager => chatSettingsManager;
    public ChatManager ChatManager => chatManager;

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
        vivoxChatService = GetComponent<VivoxChatService>();
        chatCommandCatalog = GetComponent<ChatCommandCatalog>();
        chatSettingsManager = GetComponent<ChatSettingsManager>();
        chatCommandProcessor = GetComponent<ChatCommandProcessor>();
        chatManager = GetComponent<ChatManager>();

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

        if (vivoxChatService == null || !vivoxChatService.Initialize())
        {
            Debug.LogError("OnlineServicesRoot could not initialize VivoxChatService.");
            return;
        }

        if (chatCommandCatalog == null || !chatCommandCatalog.Initialize())
        {
            Debug.LogError("OnlineServicesRoot could not initialize ChatCommandCatalog.");
            return;
        }

        if (chatSettingsManager == null)
        {
            Debug.LogError("OnlineServicesRoot could not initialize because ChatSettingsManager is missing.");
            return;
        }

        if (chatCommandProcessor == null || !chatCommandProcessor.Initialize())
        {
            Debug.LogError("OnlineServicesRoot could not initialize ChatCommandProcessor.");
            return;
        }

        if (chatManager == null || !chatManager.Initialize())
        {
            Debug.LogError("OnlineServicesRoot could not initialize ChatManager.");
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
