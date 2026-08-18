using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class FailureManager : MonoBehaviour
{
    public static FailureManager instance;

    private const float PopupReadyTimeoutSeconds = 5f;

    #region Private Fields

    private LobbyManager lobbyManager;
    private GameSceneManager gameSceneManager;

    private LobbyFailure lobbyFailure;
    private OnlineFailure onlineFailure;
    private GameFailure gameFailure;

    private bool isSubscribedToLobbyEntryFailure;
    private bool isSubscribedToLobbyForcedExit;
    private bool isSubscribedToSceneReady;

    private bool hasPendingFailure;

    private FailureDisplayMode pendingFailureDisplayMode =
        FailureDisplayMode.ShowNow;

    private FailurePrecedence pendingFailurePrecedence =
        FailurePrecedence.None;

    private string pendingFailureMessage =
        string.Empty;

    private Coroutine openPopupRoutine;

    #endregion

    #region Properties

    public LobbyFailure LobbyFailure => lobbyFailure;
    public OnlineFailure OnlineFailure => onlineFailure;
    public GameFailure GameFailure => gameFailure;

    public bool HasPendingFailure => hasPendingFailure;
    public string PendingFailureMessage => pendingFailureMessage;
    public FailurePrecedence PendingFailurePrecedence => pendingFailurePrecedence;

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

        lobbyFailure = GetComponent<LobbyFailure>();
        onlineFailure = GetComponent<OnlineFailure>();
        gameFailure = GetComponent<GameFailure>();
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
        StopPopupRoutine();
    }

    private void OnDestroy()
    {
        UnsubscribeFromManagers();
        StopPopupRoutine();

        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Subscriptions

    private void SubscribeToManagers()
    {
        if (lobbyManager == null)
        {
            lobbyManager = LobbyManager.instance;
        }

        if (lobbyManager != null)
        {
            if (!isSubscribedToLobbyEntryFailure)
            {
                lobbyManager.LobbyEntryFailed += OnLobbyEntryFailed;
                isSubscribedToLobbyEntryFailure = true;
            }

            if (!isSubscribedToLobbyForcedExit)
            {
                lobbyManager.LobbyForcedExit += OnLobbyForcedExit;
                isSubscribedToLobbyForcedExit = true;
            }
        }

        if (!isSubscribedToSceneReady)
        {
            if (gameSceneManager == null)
            {
                gameSceneManager = GameSceneManager.instance;
            }

            if (gameSceneManager != null)
            {
                gameSceneManager.SceneReadyToStart += OnSceneReadyToStart;
                isSubscribedToSceneReady = true;
            }
        }
    }

    private void UnsubscribeFromManagers()
    {
        if (lobbyManager != null)
        {
            if (isSubscribedToLobbyEntryFailure)
            {
                lobbyManager.LobbyEntryFailed -= OnLobbyEntryFailed;
            }

            if (isSubscribedToLobbyForcedExit)
            {
                lobbyManager.LobbyForcedExit -= OnLobbyForcedExit;
            }
        }

        if (isSubscribedToSceneReady &&
            gameSceneManager != null)
        {
            gameSceneManager.SceneReadyToStart -= OnSceneReadyToStart;
        }

        isSubscribedToLobbyEntryFailure = false;
        isSubscribedToLobbyForcedExit = false;
        isSubscribedToSceneReady = false;
    }

    #endregion

    #region Lobby Failures

    private void OnLobbyEntryFailed(LobbyEntryResult result)
    {
        if (lobbyFailure == null)
        {
            lobbyFailure = GetComponent<LobbyFailure>();
        }

        if (lobbyFailure == null)
        {
            QueueFailure(
                "The lobby could not be entered.",
                FailurePrecedence.Domain,
                FailureDisplayMode.WaitForMain);

            return;
        }

        lobbyFailure.GetEntryFailure(
            result,
            out string message,
            out FailurePrecedence precedence);

        QueueFailure(
            message,
            precedence,
            FailureDisplayMode.WaitForMain);
    }

    private void OnLobbyForcedExit(LobbyExitNotification notification)
    {
        if (lobbyFailure == null)
        {
            lobbyFailure = GetComponent<LobbyFailure>();
        }

        if (lobbyFailure != null)
        {
            lobbyFailure.GetForcedExitFailure(
                notification,
                out string message,
                out FailurePrecedence precedence);

            QueueFailure(
                message,
                precedence,
                FailureDisplayMode.WaitForMain);

            return;
        }

        LobbyEntryFailureType failureType =
            notification != null
                ? notification.failureType
                : LobbyEntryFailureType.Unknown;

        string fallbackMessage =
            notification != null &&
            !string.IsNullOrWhiteSpace(notification.message)
                ? notification.message
                : "The lobby could not continue.";

        FailurePrecedence fallbackPrecedence =
            failureType == LobbyEntryFailureType.NetworkConnectionFailed ||
            failureType == LobbyEntryFailureType.NetworkLobbyConnectionUnavailable
                ? FailurePrecedence.SessionConnection
                : FailurePrecedence.Domain;

        QueueFailure(
            fallbackMessage,
            fallbackPrecedence,
            FailureDisplayMode.WaitForMain);
    }

    #endregion

    #region Failure Entry

    public bool ReportFailure(
        string message,
        FailurePrecedence precedence,
        FailureDisplayMode displayMode)
    {
        SubscribeToManagers();

        if (!QueueFailure(message, precedence, displayMode))
        {
            return false;
        }

        if (displayMode == FailureDisplayMode.ShowNow)
        {
            StartPopupRoutine();
        }

        return true;
    }

    public bool ShowFailure(
        string message,
        FailurePrecedence precedence = FailurePrecedence.Domain)
    {
        return ReportFailure(
            message,
            precedence,
            FailureDisplayMode.ShowNow);
    }

    public bool ShowFailureOnMain(
        string message,
        FailurePrecedence precedence = FailurePrecedence.Domain)
    {
        return ReportFailure(
            message,
            precedence,
            FailureDisplayMode.WaitForMain);
    }

    private bool QueueFailure(
        string message,
        FailurePrecedence precedence,
        FailureDisplayMode displayMode)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (hasPendingFailure &&
            precedence <= pendingFailurePrecedence)
        {
            return false;
        }

        hasPendingFailure = true;
        pendingFailureDisplayMode = displayMode;
        pendingFailurePrecedence = precedence;
        pendingFailureMessage = message;

        SubscribeToManagers();

        return true;
    }

    #endregion

    #region Scene Ready

    private void OnSceneReadyToStart(GameSceneType sceneType)
    {
        if (!hasPendingFailure)
        {
            return;
        }

        if (pendingFailureDisplayMode == FailureDisplayMode.WaitForMain &&
            sceneType != GameSceneType.Main)
        {
            return;
        }

        StartPopupRoutine();
    }

    #endregion

    #region Popup

    private void StartPopupRoutine()
    {
        if (!hasPendingFailure)
        {
            return;
        }

        StopPopupRoutine();

        openPopupRoutine =
            StartCoroutine(OpenFailurePopupWhenReady());
    }

    private void StopPopupRoutine()
    {
        if (openPopupRoutine == null)
        {
            return;
        }

        StopCoroutine(openPopupRoutine);
        openPopupRoutine = null;
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
                "[FailureManager] Could not show the failure popup because PopupManager was not found.");

            openPopupRoutine = null;
            yield break;
        }

        bool popupOpened =
            PopupManager.instance.OpenFailurePopup(
                pendingFailureMessage);

        if (popupOpened)
        {
            ClearPendingFailure();
        }

        openPopupRoutine = null;
    }

    #endregion

    #region Clear

    private void ClearPendingFailure()
    {
        hasPendingFailure = false;

        pendingFailureDisplayMode =
            FailureDisplayMode.ShowNow;

        pendingFailurePrecedence =
            FailurePrecedence.None;

        pendingFailureMessage =
            string.Empty;
    }

    #endregion
}