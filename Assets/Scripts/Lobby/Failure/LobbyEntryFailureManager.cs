using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbyEntryFailureManager : MonoBehaviour
{
    public static LobbyEntryFailureManager instance;

    private const float PopupReadyTimeoutSeconds = 5f;

    private LobbyManager lobbyManager;
    private GameSceneManager gameSceneManager;

    private bool isSubscribedToLobbyFailure;
    private bool isSubscribedToForcedExit;
    private bool isSubscribedToSceneReady;
    private bool hasPendingFailure;

    private LobbyEntryFailureType pendingFailureType =
        LobbyEntryFailureType.None;

    private string pendingFailureMessage =
        string.Empty;

    private Coroutine openPopupRoutine;

    public bool HasPendingFailure =>
        hasPendingFailure;

    public LobbyEntryFailureType PendingFailureType =>
        pendingFailureType;

    public string PendingFailureMessage =>
        pendingFailureMessage;

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
        SubscribeToManagers();
    }

    private void Start()
    {
        SubscribeToManagers();
    }

    private void OnDisable()
    {
        UnsubscribeFromManagers();
    }

    private void OnDestroy()
    {
        UnsubscribeFromManagers();

        if (instance == this)
        {
            instance = null;
        }
    }

    private void SubscribeToManagers()
    {
        if (lobbyManager == null)
        {
            lobbyManager =
                LobbyManager.instance;
        }

        if (lobbyManager != null)
        {
            if (!isSubscribedToLobbyFailure)
            {
                lobbyManager.LobbyEntryFailed +=
                    OnLobbyEntryFailed;

                isSubscribedToLobbyFailure = true;
            }

            if (!isSubscribedToForcedExit)
            {
                lobbyManager.LobbyForcedExit +=
                    OnLobbyForcedExit;

                isSubscribedToForcedExit = true;
            }
        }

        if (!isSubscribedToSceneReady)
        {
            if (gameSceneManager == null)
            {
                gameSceneManager =
                    GameSceneManager.instance;
            }

            if (gameSceneManager != null)
            {
                gameSceneManager.SceneReadyToStart +=
                    OnSceneReadyToStart;

                isSubscribedToSceneReady = true;
            }
        }
    }

    private void UnsubscribeFromManagers()
    {
        if (lobbyManager != null)
        {
            if (isSubscribedToLobbyFailure)
            {
                lobbyManager.LobbyEntryFailed -=
                    OnLobbyEntryFailed;
            }

            if (isSubscribedToForcedExit)
            {
                lobbyManager.LobbyForcedExit -=
                    OnLobbyForcedExit;
            }
        }

        if (isSubscribedToSceneReady &&
            gameSceneManager != null)
        {
            gameSceneManager.SceneReadyToStart -=
                OnSceneReadyToStart;
        }

        isSubscribedToLobbyFailure = false;
        isSubscribedToForcedExit = false;
        isSubscribedToSceneReady = false;
    }

    private void OnLobbyEntryFailed(
        LobbyEntryResult result)
    {
        pendingFailureType =
            result != null
                ? result.failureType
                : LobbyEntryFailureType.Unknown;

        pendingFailureMessage =
            GetFailureMessage(result);

        hasPendingFailure = true;

        SubscribeToManagers();

        if (gameSceneManager == null)
        {
            Debug.LogWarning(
                "[LobbyEntryFailureManager] Could not return to Main because GameSceneManager was not found.");

            return;
        }

        gameSceneManager
            .ReturnToMainSceneAfterLobbyFailure();
    }

    private void OnLobbyForcedExit(
        LobbyExitNotification notification)
    {
        pendingFailureType =
            notification != null
                ? notification.failureType
                : LobbyEntryFailureType.Unknown;

        pendingFailureMessage =
            notification != null &&
            !string.IsNullOrWhiteSpace(
                notification.message)
                ? notification.message
                : GetFailureMessage(
                    pendingFailureType);

        hasPendingFailure = true;

        SubscribeToManagers();
    }

    private void OnSceneReadyToStart(
        GameSceneType sceneType)
    {
        if (!hasPendingFailure ||
            sceneType != GameSceneType.Main)
        {
            return;
        }

        if (openPopupRoutine != null)
        {
            StopCoroutine(openPopupRoutine);
        }

        openPopupRoutine =
            StartCoroutine(
                OpenFailurePopupWhenReady());
    }

    private IEnumerator OpenFailurePopupWhenReady()
    {
        yield return null;

        float timeoutTime =
            Time.unscaledTime +
            PopupReadyTimeoutSeconds;

        while (Time.unscaledTime < timeoutTime)
        {
            bool loaderFinished =
                LoadingFaderManager.instance == null ||
                !LoadingFaderManager.instance.IsShowing;

            bool popupManagerReady =
                PopupManager.instance != null;

            if (loaderFinished &&
                popupManagerReady)
            {
                break;
            }

            yield return null;
        }

        if (PopupManager.instance == null)
        {
            Debug.LogWarning(
                "[LobbyEntryFailureManager] Could not show the failure popup because PopupManager was not found.");

            openPopupRoutine = null;
            yield break;
        }

        bool popupOpened =
            PopupManager.instance
                .OpenLobbyEntryFailurePopup(
                    pendingFailureMessage);

        if (popupOpened)
        {
            ClearPendingFailure();
        }

        openPopupRoutine = null;
    }

    private string GetFailureMessage(
        LobbyEntryResult result)
    {
        if (result != null &&
            !string.IsNullOrWhiteSpace(
                result.failureMessage))
        {
            return result.failureMessage;
        }

        LobbyEntryFailureType failureType =
            result != null
                ? result.failureType
                : LobbyEntryFailureType.Unknown;

        return GetFailureMessage(failureType);
    }

    private string GetFailureMessage(
        LobbyEntryFailureType failureType)
    {
        switch (failureType)
        {
            case LobbyEntryFailureType.InvalidSetupData:
                return "The lobby settings were not valid.";

            case LobbyEntryFailureType.UserMissing:
                return "The current player could not be found.";

            case LobbyEntryFailureType.ServiceUnavailable:
                return "The lobby service is currently unavailable.";

            case LobbyEntryFailureType.NetworkConnectionFailed:
                return "The network connection could not be completed.";

            case LobbyEntryFailureType.NetworkLobbyConnectionUnavailable:
                return "The network lobby connection was not available.";

            case LobbyEntryFailureType.LobbyNotFound:
                return "The lobby could not be found.";

            case LobbyEntryFailureType.LobbyFull:
                return "The lobby is full.";

            case LobbyEntryFailureType.InvalidPassword:
                return "The lobby password is incorrect.";

            case LobbyEntryFailureType.AlreadyInLobby:
                return "The player is already in a lobby.";

            case LobbyEntryFailureType.LobbyCreationFailed:
                return "The lobby could not be created.";

            case LobbyEntryFailureType.LobbyJoinFailed:
                return "The lobby could not be joined.";

            case LobbyEntryFailureType.LobbyLeaveFailed:
                return "The lobby could not be left.";

            case LobbyEntryFailureType.KickedFromLobby:
                return "You were removed from the lobby by the host.";

            case LobbyEntryFailureType.LobbyClosed:
                return "The lobby was closed.";

            case LobbyEntryFailureType.ConnectionLost:
                return "The connection to the lobby was lost.";

            default:
                return "The lobby could not be entered.";
        }
    }

    private void ClearPendingFailure()
    {
        hasPendingFailure = false;
        pendingFailureType =
            LobbyEntryFailureType.None;

        pendingFailureMessage =
            string.Empty;
    }
}
