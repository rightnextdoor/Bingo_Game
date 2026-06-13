using UnityEngine;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour, ISceneReadyCheck
{
    public static GameManager instance;

    private bool isReady;

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Managers")]
    [SerializeField] private SaveManager saveManager;
    [SerializeField] private UserManager userManager;

    public SaveManager SaveManager => saveManager;
    public UserManager UserManager => userManager;

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
            Debug.LogWarning($"Duplicate GameManager found on {gameObject.name}. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        instance = this;

        FindManagersOnGameObject();

        isReady = HasSaveManager() && HasUserManager();

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
        RegisterReadyCheck();
    }

    private void OnValidate()
    {
        FindManagersOnGameObject();
    }

    private void OnDestroy()
    {
        UnregisterReadyCheck();

        if (instance == this)
        {
            instance = null;
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

    private void FindManagersOnGameObject()
    {
        if (saveManager == null)
        {
            saveManager = GetComponent<SaveManager>();
        }

        if (userManager == null)
        {
            userManager = GetComponent<UserManager>();
        }
    }

    public bool HasSaveManager()
    {
        return saveManager != null;
    }

    public bool HasUserManager()
    {
        return userManager != null;
    }
}