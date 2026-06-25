using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager instance;

    #region Private Fields

    private LobbySetupData pendingLobbySetupData;
    private GameSceneManager gameSceneManager;
    private bool isSubscribedToSceneReady;

    #endregion

    #region Properties

    public bool HasPendingLobbySetupData => pendingLobbySetupData != null;
    public LobbySetupData PendingLobbySetupData => pendingLobbySetupData;

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
    }

    private void OnEnable()
    {
        SubscribeToSceneReady();
    }

    private void Start()
    {
        SubscribeToSceneReady();
    }

    private void OnDisable()
    {
        UnsubscribeFromSceneReady();
    }

    private void OnDestroy()
    {
        UnsubscribeFromSceneReady();

        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Setup Data Entry Point

    public void SetPendingLobbySetupData(LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null)
        {
            Debug.LogWarning("[LobbyManager] Cannot set pending lobby setup data because the data is null.");
            return;
        }

        pendingLobbySetupData = lobbySetupData;

        Debug.Log($"[LobbyManager] Pending lobby setup data received from Main Menu. Mode: {pendingLobbySetupData.playMode}");
    }

    public void ClearPendingLobbySetupData()
    {
        pendingLobbySetupData = null;
    }

    #endregion

    #region Scene Ready Listener

    private void SubscribeToSceneReady()
    {
        if (isSubscribedToSceneReady)
        {
            return;
        }

        if (gameSceneManager == null)
        {
            gameSceneManager = GameSceneManager.instance;
        }

        if (gameSceneManager == null)
        {
            return;
        }

        gameSceneManager.SceneReadyToStart += OnSceneReadyToStart;
        isSubscribedToSceneReady = true;
    }

    private void UnsubscribeFromSceneReady()
    {
        if (!isSubscribedToSceneReady)
        {
            return;
        }

        if (gameSceneManager != null)
        {
            gameSceneManager.SceneReadyToStart -= OnSceneReadyToStart;
        }

        isSubscribedToSceneReady = false;
    }

    private void OnSceneReadyToStart(GameSceneType sceneType)
    {
        if (sceneType != GameSceneType.Lobby)
        {
            return;
        }

        LogPendingLobbySetupData();
    }

    #endregion

    #region Debug Logging

    private void LogPendingLobbySetupData()
    {
        if (pendingLobbySetupData == null)
        {
            Debug.LogWarning("[LobbyManager] Lobby scene is ready, but no pending lobby setup data was found.");
            return;
        }

        switch (pendingLobbySetupData.playMode)
        {
            case MainMenuPlayMode.Solo:
                LogSoloSetupData(pendingLobbySetupData.soloSetupData);
                break;

            case MainMenuPlayMode.Online:
                LogOnlineSetupData(pendingLobbySetupData.onlineSetupData);
                break;

            case MainMenuPlayMode.Custom:
                LogCustomSetupData(pendingLobbySetupData.customSetupData);
                break;

            default:
                Debug.LogWarning($"[LobbyManager] Lobby scene is ready, but play mode is not valid: {pendingLobbySetupData.playMode}");
                break;
        }
    }

    private void LogSoloSetupData(SoloLobbySetupData soloSetupData)
    {
        if (soloSetupData == null)
        {
            Debug.LogWarning("[LobbyManager] Solo setup data is missing.");
            return;
        }

        Debug.Log(
            "[LobbyManager] Lobby scene ready with Solo setup data.\n" +
            $"Play Mode: {MainMenuPlayMode.Solo}\n" +
            $"Unlimited Players: {soloSetupData.unlimitedPlayers}\n" +
            $"Max Players: {soloSetupData.maxPlayers}"
        );
    }

    private void LogOnlineSetupData(OnlineLobbySetupData onlineSetupData)
    {
        if (onlineSetupData == null)
        {
            Debug.LogWarning("[LobbyManager] Online setup data is missing.");
            return;
        }

        Debug.Log(
            "[LobbyManager] Lobby scene ready with Online setup data.\n" +
            $"Play Mode: {MainMenuPlayMode.Online}\n" +
            $"Game Mode: {onlineSetupData.gameModeType}\n" +
            $"Search Type: {onlineSetupData.searchType}\n" +
            $"Ball Count: {onlineSetupData.ballCountType}"
        );
    }

    private void LogCustomSetupData(CustomLobbySetupData customSetupData)
    {
        if (customSetupData == null)
        {
            Debug.LogWarning("[LobbyManager] Custom setup data is missing.");
            return;
        }

        switch (customSetupData.actionType)
        {
            case CustomLobbyActionType.HostLobby:
                LogCustomHostSetupData(customSetupData.hostSetupData);
                break;

            case CustomLobbyActionType.SearchLobby:
                LogCustomSearchSetupData(customSetupData.searchSetupData);
                break;

            default:
                Debug.LogWarning($"[LobbyManager] Custom action type is not valid: {customSetupData.actionType}");
                break;
        }
    }

    private void LogCustomHostSetupData(CustomHostLobbySetupData hostSetupData)
    {
        if (hostSetupData == null)
        {
            Debug.LogWarning("[LobbyManager] Custom host setup data is missing.");
            return;
        }

        Debug.Log(
            "[LobbyManager] Lobby scene ready with Custom Host setup data.\n" +
            $"Play Mode: {MainMenuPlayMode.Custom}\n" +
            $"Custom Action: {CustomLobbyActionType.HostLobby}\n" +
            $"Lobby Name: {hostSetupData.lobbyName}\n" +
            $"Has Password: {HasPassword(hostSetupData.password)}\n" +
            $"Unlimited Players: {hostSetupData.unlimitedPlayers}\n" +
            $"Max Players: {hostSetupData.maxPlayers}"
        );
    }

    private void LogCustomSearchSetupData(CustomSearchLobbySetupData searchSetupData)
    {
        if (searchSetupData == null)
        {
            Debug.LogWarning("[LobbyManager] Custom search setup data is missing.");
            return;
        }

        Debug.Log(
            "[LobbyManager] Lobby scene ready with Custom Search setup data.\n" +
            $"Play Mode: {MainMenuPlayMode.Custom}\n" +
            $"Custom Action: {CustomLobbyActionType.SearchLobby}\n" +
            $"Lobby Code: {searchSetupData.lobbyCode}\n" +
            $"Has Password: {HasPassword(searchSetupData.password)}"
        );
    }

    private bool HasPassword(string password)
    {
        return !string.IsNullOrWhiteSpace(password);
    }

    #endregion
}