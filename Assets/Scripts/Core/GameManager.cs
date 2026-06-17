using UnityEngine;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour, ISceneReadyCheck
{
    public static GameManager instance;

    private bool isReady;

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    private SaveManager saveManager;
    private UserManager userManager;

    public SaveManager Save => saveManager;
    public UserManager User => userManager;

    public string ReadyName => "Game Manager";
    public bool IsReady => isReady;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (dontDestroyOnLoad)
        {
            if (transform.parent != null)
            {
                Debug.LogWarning("GameManager must be a root object for DontDestroyOnLoad to work.");
            }
            else
            {
                DontDestroyOnLoad(gameObject);
            }
        }
    }

    private void Start()
    {
        CacheManagerInstances();

        isReady = HasSaveManager() && HasUserManager();

        RegisterReadyCheck();
    }

    private void OnDestroy()
    {
        UnregisterReadyCheck();

        if (instance == this)
        {
            instance = null;
        }
    }

    private void CacheManagerInstances()
    {
        if (saveManager == null)
        {
            saveManager = SaveManager.instance;
        }

        if (userManager == null)
        {
            userManager = UserManager.instance;
        }
    }

    #region Ready Check

    private void RegisterReadyCheck()
    {
        if (SceneReadyController.instance == null)
        {
            return;
        }

        SceneReadyController.instance.RegisterReadyCheck(this, true);
    }

    private void UnregisterReadyCheck()
    {
        if (SceneReadyController.instance == null)
        {
            return;
        }

        SceneReadyController.instance.UnregisterReadyCheck(this);
    }

    #endregion

    public bool HasSaveManager()
    {
        if (saveManager == null)
        {
            saveManager = SaveManager.instance;
        }

        return saveManager != null;
    }

    public bool HasUserManager()
    {
        if (userManager == null)
        {
            userManager = UserManager.instance;
        }

        return userManager != null;
    }
}