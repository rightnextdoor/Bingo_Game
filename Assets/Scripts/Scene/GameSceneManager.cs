using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

public enum GameSceneType
{
    Main,
    Lobby,
    Game
}

public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager instance;

    public event Action<GameSceneType> SceneReadyToStart;
    public event Action<GameSceneType> SceneReadyForFadeOut;

    #region Inspector Fields

    private SceneReadyController sceneReadyController;

    [Header("Startup")]
    [SerializeField] private bool showLoadingOnStart = true;
    [SerializeField] private GameSceneType startingSceneType = GameSceneType.Main;

    [Header("Scenes")]
    [SerializeField] private SceneField mainScene;
    [SerializeField] private SceneField lobbyScene;
    [SerializeField] private SceneField gameScene;

    #endregion

    #region Private Fields

    private Coroutine loadingRoutine;
    private GameSceneType currentSceneType;
    private bool isLoadingScene;

    private bool hasPendingLoadingRedirect;
    private GameSceneType pendingLoadingRedirectSceneType;

    #endregion

    #region Properties

    public bool IsLoadingScene => isLoadingScene;
    public GameSceneType CurrentSceneType => currentSceneType;

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
        DontDestroyOnLoad(gameObject);

        ResolveReferences();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Start()
    {
        if (!showLoadingOnStart)
        {
            return;
        }

        BeginCurrentSceneLoading(ResolveStartingSceneType());
    }

    #endregion

    #region Scene Loading Entry

    public void LoadMainScene()
    {
        LoadScene(GameSceneType.Main);
    }

    public void LoadLobbyScene()
    {
        LoadScene(GameSceneType.Lobby);
    }

    public void LoadGameScene()
    {
        LoadScene(GameSceneType.Game);
    }

    public void LoadScene(GameSceneType sceneType)
    {
        if (isLoadingScene)
        {
            return;
        }

        hasPendingLoadingRedirect = false;
        loadingRoutine = StartCoroutine(LoadSceneRoutine(sceneType));
    }

    public void BeginCurrentSceneLoading(GameSceneType sceneType)
    {
        if (isLoadingScene)
        {
            return;
        }

        hasPendingLoadingRedirect = false;
        loadingRoutine = StartCoroutine(CurrentSceneLoadingRoutine(sceneType));
    }

    public void ReturnToMainSceneAfterFailure()
    {
        if (isLoadingScene)
        {
            pendingLoadingRedirectSceneType = GameSceneType.Main;
            hasPendingLoadingRedirect = true;
            return;
        }

        LoadMainScene();
    }

    #endregion

    #region Scene Loading Routines

    private IEnumerator LoadSceneRoutine(GameSceneType sceneType)
    {
        isLoadingScene = true;

        LoadingFaderManager loader = CurrentLoader;

        if (loader != null && !loader.IsShowing)
        {
            loader.ShowLoading();
        }

        GameSceneType targetSceneType = sceneType;

        while (true)
        {
            currentSceneType = targetSceneType;

            ResolveReferences();

            if (sceneReadyController != null)
            {
                sceneReadyController.ClearSceneReadyChecks();
            }

            yield return null;

            string sceneName = GetSceneName(targetSceneType);

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning($"Cannot load scene because no scene name was found for {targetSceneType}.");

                if (loader != null)
                {
                    loader.HideInstant();
                }

                isLoadingScene = false;
                loadingRoutine = null;
                yield break;
            }

            AsyncOperation loadOperation = UnitySceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            while (loadOperation != null && !loadOperation.isDone)
            {
                yield return null;
            }

            sceneReadyController = null;
            ResolveReferences();

            yield return null;

            if (TryConsumeLoadingRedirect(out targetSceneType))
            {
                continue;
            }

            while (!CanStartScene())
            {
                if (hasPendingLoadingRedirect)
                {
                    break;
                }

                yield return null;
            }

            if (TryConsumeLoadingRedirect(out targetSceneType))
            {
                continue;
            }

            break;
        }

        SceneReadyForFadeOut?.Invoke(currentSceneType);

        loader = CurrentLoader;

        if (loader != null)
        {
            yield return loader.FadeOut();
        }

        isLoadingScene = false;
        loadingRoutine = null;

        SceneReadyToStart?.Invoke(currentSceneType);
    }

    private IEnumerator CurrentSceneLoadingRoutine(GameSceneType sceneType)
    {
        isLoadingScene = true;
        currentSceneType = sceneType;

        ResolveReferences();

        LoadingFaderManager loader = CurrentLoader;

        if (loader != null)
        {
            loader.ShowLoading();
        }

        yield return WaitUntilSceneCanStart();

        if (TryConsumeLoadingRedirect(out GameSceneType redirectSceneType))
        {
            isLoadingScene = false;
            loadingRoutine = null;
            LoadScene(redirectSceneType);
            yield break;
        }

        SceneReadyForFadeOut?.Invoke(currentSceneType);

        loader = CurrentLoader;

        if (loader != null)
        {
            yield return loader.FadeOut();
        }

        isLoadingScene = false;
        loadingRoutine = null;

        SceneReadyToStart?.Invoke(currentSceneType);
    }

    private IEnumerator WaitUntilSceneCanStart()
    {
        yield return null;

        while (!CanStartScene())
        {
            if (hasPendingLoadingRedirect)
            {
                yield break;
            }

            yield return null;
        }
    }

    #endregion

    #region Ready Checks

    private bool CanStartScene()
    {
        LoadingFaderManager loader = CurrentLoader;

        bool minimumTimeDone = loader == null || loader.HasMinimumShowTimePassed;
        bool sceneReady = sceneReadyController == null || sceneReadyController.AreAllReady();
        bool lobbyEntryFinished = IsLobbyEntryFinished();
        bool gameEntryFinished = IsGameEntryFinished();
        bool sessionChatPreparationFinished = IsSessionChatPreparationFinished();

        return minimumTimeDone && sceneReady && lobbyEntryFinished && gameEntryFinished && sessionChatPreparationFinished;
    }

    #endregion

    #region Scene Setup

    private string GetSceneName(GameSceneType sceneType)
    {
        switch (sceneType)
        {
            case GameSceneType.Main:
                return mainScene != null ? mainScene.SceneName : string.Empty;

            case GameSceneType.Lobby:
                return lobbyScene != null ? lobbyScene.SceneName : string.Empty;

            case GameSceneType.Game:
                return gameScene != null ? gameScene.SceneName : string.Empty;

            default:
                return string.Empty;
        }
    }

    private GameSceneType ResolveStartingSceneType()
    {
        string activeSceneName = UnitySceneManager.GetActiveScene().name;

        if (string.Equals(activeSceneName, GetSceneName(GameSceneType.Lobby), StringComparison.Ordinal))
        {
            return GameSceneType.Lobby;
        }

        if (string.Equals(activeSceneName, GetSceneName(GameSceneType.Game), StringComparison.Ordinal))
        {
            return GameSceneType.Game;
        }

        if (string.Equals(activeSceneName, GetSceneName(GameSceneType.Main), StringComparison.Ordinal))
        {
            return GameSceneType.Main;
        }

        return startingSceneType;
    }

    #endregion

    #region Helpers

    private void ResolveReferences()
    {
        if (sceneReadyController == null)
        {
            sceneReadyController = SceneReadyController.instance;
        }
    }

    private LoadingFaderManager CurrentLoader
    {
        get
        {
            return LoadingFaderManager.instance;
        }
    }

    private bool IsSessionChatPreparationFinished()
    {
        bool requiresNetworkSessionChat = false;

        if (currentSceneType == GameSceneType.Lobby)
        {
            LobbyManager lobbyManager = LobbyManager.instance;

            requiresNetworkSessionChat =
                lobbyManager != null &&
                lobbyManager.EntryState != LobbyEntryState.Failed &&
                lobbyManager.RuntimeType == SessionRuntimeType.Network;
        }
        else if (currentSceneType == GameSceneType.Game)
        {
            GameSessionManager gameSessionManager = GameSessionManager.instance;

            requiresNetworkSessionChat =
                gameSessionManager != null &&
                gameSessionManager.EntryState == GameSessionEntryState.Completed &&
                gameSessionManager.RuntimeType == SessionRuntimeType.Network;
        }

        if (!requiresNetworkSessionChat)
        {
            return true;
        }

        ChatManager chatManager = ChatManager.instance;
        return chatManager == null || !chatManager.IsReady || chatManager.IsSessionPreparationResolved;
    }

    private bool IsLobbyEntryFinished()
    {
        if (currentSceneType != GameSceneType.Lobby)
        {
            return true;
        }

        if (LobbyManager.instance == null)
        {
            return false;
        }

        return LobbyManager.instance.EntryState == LobbyEntryState.Completed || LobbyManager.instance.EntryState == LobbyEntryState.Failed;
    }

    private bool IsGameEntryFinished()
    {
        if (currentSceneType != GameSceneType.Game)
        {
            return true;
        }

        if (GameSessionManager.instance == null)
        {
            return false;
        }

        return GameSessionManager.instance.EntryState == GameSessionEntryState.Completed ||
               GameSessionManager.instance.EntryState == GameSessionEntryState.Failed;
    }

    private bool TryConsumeLoadingRedirect(out GameSceneType sceneType)
    {
        sceneType = currentSceneType;

        if (!hasPendingLoadingRedirect)
        {
            return false;
        }

        sceneType = pendingLoadingRedirectSceneType;
        hasPendingLoadingRedirect = false;

        return true;
    }

    #endregion

}
