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

    #region Inspector Fields

    [Header("Managers")]
    [SerializeField] private LoadingFaderManager loadingFaderManager;
    [SerializeField] private SceneReadyController sceneReadyController;

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

    #endregion

    #region Properties

    public bool IsLoadingScene => isLoadingScene;
    public GameSceneType CurrentSceneType => currentSceneType;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        ResolveReferences();
    }

    private void Start()
    {
        if (!showLoadingOnStart)
        {
            return;
        }

        BeginCurrentSceneLoading(startingSceneType);
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

        loadingRoutine = StartCoroutine(LoadSceneRoutine(sceneType));
    }

    public void BeginCurrentSceneLoading(GameSceneType sceneType)
    {
        if (isLoadingScene)
        {
            return;
        }

        loadingRoutine = StartCoroutine(CurrentSceneLoadingRoutine(sceneType));
    }

    #endregion

    #region Scene Loading Routines

    private IEnumerator LoadSceneRoutine(GameSceneType sceneType)
    {
        isLoadingScene = true;
        currentSceneType = sceneType;

        ResolveReferences();


        if (sceneReadyController != null)
        {
            sceneReadyController.ClearSceneReadyChecks();
        }

        if (loadingFaderManager != null)
        {
            loadingFaderManager.ShowLoading();
        }

        yield return null;

        string sceneName = GetSceneName(sceneType);

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning($"Cannot load scene because no scene name was found for {sceneType}.");
            isLoadingScene = false;
            yield break;
        }

        AsyncOperation loadOperation = UnitySceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        while (loadOperation != null && !loadOperation.isDone)
        {
            yield return null;
        }

        yield return WaitUntilSceneCanStart();

        if (loadingFaderManager != null)
        {
            yield return loadingFaderManager.FadeOut();
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


        if (loadingFaderManager != null)
        {
            loadingFaderManager.ShowLoading();
        }

        yield return WaitUntilSceneCanStart();

        if (loadingFaderManager != null)
        {
            yield return loadingFaderManager.FadeOut();
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
            yield return null;
        }
    }

    #endregion

    #region Ready Checks

    private bool CanStartScene()
    {
        bool minimumTimeDone = loadingFaderManager == null || loadingFaderManager.HasMinimumShowTimePassed;
        bool sceneReady = sceneReadyController == null || sceneReadyController.AreAllReady();

        return minimumTimeDone && sceneReady;
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

    #endregion

    #region Helpers

    private void ResolveReferences()
    {
        if (loadingFaderManager == null)
        {
            loadingFaderManager = LoadingFaderManager.instance;
        }

        if (sceneReadyController == null)
        {
            sceneReadyController = SceneReadyController.instance;
        }
    }

    #endregion

}