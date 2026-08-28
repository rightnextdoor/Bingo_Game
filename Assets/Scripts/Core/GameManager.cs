using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerProfileRegistry))]
public class GameManager : MonoBehaviour, ISceneReadyCheck
{
    public static GameManager instance;

    private bool isReady;
    private bool hasCompletedSessionStartupCleanup;

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    private SaveManager saveManager;
    private UserManager userManager;
    private PlayerProfileRegistry playerProfileRegistry;

    public SaveManager Save => saveManager;
    public UserManager User => userManager;
    public PlayerProfileRegistry PlayerProfiles => playerProfileRegistry;
    public bool HasCompletedSessionStartupCleanup => hasCompletedSessionStartupCleanup;

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

    private IEnumerator Start()
    {
        isReady = false;
        hasCompletedSessionStartupCleanup = false;
        RegisterReadyCheck();

        CacheManagerInstances();

        while (!HasSaveManager() ||
               !HasUserManager() ||
               !HasPlayerProfileRegistry() ||
               UserDatabase.instance == null ||
               LobbyManager.instance == null ||
               LocalLobbyManager.instance == null ||
               GameSessionManager.instance == null ||
               LocalGameSessionManager.instance == null ||
               !saveManager.HasLoadedData ||
               !userManager.IsReady)
        {
            yield return null;
            CacheManagerInstances();
        }

        ClearSessionDataForFreshApplicationStart();

        hasCompletedSessionStartupCleanup = true;
        isReady = true;
    }

    private void ClearSessionDataForFreshApplicationStart()
    {
        LobbyManager.instance.ResetForFreshApplicationStart();
        LocalLobbyManager.instance.ResetForFreshApplicationStart();
        GameSessionManager.instance.ResetForFreshApplicationStart();
        LocalGameSessionManager.instance.ResetForFreshApplicationStart();
        MultiplayerNetworkScheduler.instance?.ClearAll();

        bool clearedSavedGameIds =
            UserDatabase.instance.ClearAllLastGameIds(false);

        if (!string.IsNullOrWhiteSpace(userManager.CurrentUser.lastGameId))
        {
            userManager.ClearLastGameId();
            clearedSavedGameIds = true;
        }

        if (clearedSavedGameIds)
        {
            saveManager.SaveGameImmediate();
        }
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
        saveManager ??= SaveManager.instance;
        userManager ??= UserManager.instance;
        playerProfileRegistry ??= PlayerProfileRegistry.instance;
    }

    #region Ready Check

    private void RegisterReadyCheck()
    {
        if (SceneReadyController.instance != null)
        {
            SceneReadyController.instance.RegisterReadyCheck(this, true);
        }
    }

    private void UnregisterReadyCheck()
    {
        if (SceneReadyController.instance != null)
        {
            SceneReadyController.instance.UnregisterReadyCheck(this);
        }
    }

    #endregion

    public bool HasSaveManager()
    {
        saveManager ??= SaveManager.instance;
        return saveManager != null;
    }

    public bool HasUserManager()
    {
        userManager ??= UserManager.instance;
        return userManager != null;
    }

    public bool HasPlayerProfileRegistry()
    {
        playerProfileRegistry ??= PlayerProfileRegistry.instance;
        return playerProfileRegistry != null;
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
