using UnityEngine;

[DefaultExecutionOrder(-1080)]
[DisallowMultipleComponent]
public class OnlineServicesStartup : MonoBehaviour
{
    #region Fields

    private OnlineServicesRoot onlineServicesRoot;
    private OnlineConnectionManager connectionManager;
    private GameSceneManager gameSceneManager;

    private bool isReady;
    private bool hasAutomaticAttemptStarted;
    private bool isSubscribedToSceneManager;

    public bool IsReady => isReady;
    public bool HasAutomaticAttemptStarted => hasAutomaticAttemptStarted;

    #endregion

    #region Unity Methods

    private void OnDestroy()
    {
        UnsubscribeFromSceneManager();
    }

    #endregion

    #region Initialization

    public bool Initialize()
    {
        if (isReady)
        {
            return true;
        }

        onlineServicesRoot = OnlineServicesRoot.instance;

        if (onlineServicesRoot == null ||
            onlineServicesRoot != GetComponent<OnlineServicesRoot>() ||
            !onlineServicesRoot.IsPrimaryInstance)
        {
            return false;
        }

        connectionManager = OnlineConnectionManager.instance;
        gameSceneManager = GameSceneManager.instance;

        if (connectionManager == null || !connectionManager.IsReady || gameSceneManager == null)
        {
            return false;
        }

        SubscribeToSceneManager();

        isReady = true;
        return true;
    }

    #endregion

    #region Scene Events

    private void SubscribeToSceneManager()
    {
        if (isSubscribedToSceneManager || gameSceneManager == null)
        {
            return;
        }

        gameSceneManager.SceneReadyToStart += OnSceneReadyToStart;
        isSubscribedToSceneManager = true;
    }

    private void UnsubscribeFromSceneManager()
    {
        if (isSubscribedToSceneManager && gameSceneManager != null)
        {
            gameSceneManager.SceneReadyToStart -= OnSceneReadyToStart;
        }

        isSubscribedToSceneManager = false;
    }

    private async void OnSceneReadyToStart(GameSceneType _)
    {
        if (hasAutomaticAttemptStarted || connectionManager == null)
        {
            return;
        }

        hasAutomaticAttemptStarted = true;

        await connectionManager.EnsureConnectedAsync();
    }

    #endregion
}