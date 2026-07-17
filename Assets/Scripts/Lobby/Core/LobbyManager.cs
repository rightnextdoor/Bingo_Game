using System;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbyManager : MonoBehaviour
{
    public static LobbyManager instance;

    private const float ServiceReadyTimeoutSeconds = 15f;

    private LobbySetupData pendingLobbySetupData;

    private LocalLobbyManager localLobbyManager;
    private NetworkLobbyManager networkLobbyManager;

    private ILobbyService activeLobbyService;

    private Lobby currentLobby;
    private string currentUserId = string.Empty;

    private SessionRuntimeType runtimeType =
        SessionRuntimeType.Local;

    private LobbyEntryState entryState =
        LobbyEntryState.Idle;

    private LobbyEntryResult lastEntryResult;

    private bool isEnteringLobby;
    private bool isLeavingLobby;

    private NetworkBootstrap networkBootstrap;
    private bool isSubscribedToNetworkBootstrap;

    public bool HasPendingLobbySetupData =>
        pendingLobbySetupData != null;

    public LobbySetupData PendingLobbySetupData =>
        pendingLobbySetupData;

    public SessionRuntimeType RuntimeType =>
        runtimeType;

    public LobbyEntryState EntryState =>
        entryState;

    public Lobby CurrentLobby =>
        currentLobby;

    public LobbyEntryResult LastEntryResult =>
        lastEntryResult;

    public bool IsEnteringLobby =>
        isEnteringLobby;

    public bool IsLeavingLobby =>
        isLeavingLobby;

    public bool HasEnteredLobby =>
        entryState == LobbyEntryState.Completed &&
        currentLobby != null;

    public event Action<LobbyEntryState> LobbyEntryStateChanged;
    public event Action<LobbyEntryResult> LobbyEntryCompleted;
    public event Action<LobbyEntryResult> LobbyEntryFailed;

    public event Action<LobbyExitResult> LobbyExitCompleted;
    public event Action<LobbyExitResult> LobbyExitFailed;
    public event Action<LobbyExitNotification> LobbyForcedExit;

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
        SubscribeToNetworkEvents();
    }

    private void Start()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromNetworkEvents();

        if (instance == this)
        {
            instance = null;
        }
    }

    #region Setup Data

    public void SetPendingLobbySetupData(
        LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null)
        {
            Debug.LogWarning(
                "[LobbyManager] Cannot set pending lobby setup data because the data is null.");

            return;
        }

        pendingLobbySetupData = lobbySetupData;
        lastEntryResult = null;

        if (currentLobby == null)
        {
            SetEntryState(LobbyEntryState.Idle);
        }
    }

    public void ClearPendingLobbySetupData()
    {
        pendingLobbySetupData = null;
    }

    #endregion

    #region Lobby Entry

    public async void BeginPendingLobbyEntry()
    {
        if (isEnteringLobby || isLeavingLobby)
        {
            return;
        }

        isEnteringLobby = true;
        lastEntryResult = null;

        if (!TryValidatePendingSetupData(
                out LobbyEntryResult validationFailure))
        {
            CompleteLobbyEntryFailure(validationFailure);
            return;
        }

        runtimeType =
            GetRuntimeType(
                pendingLobbySetupData.playMode);

        SetEntryState(
            LobbyEntryState.WaitingForService);

        activeLobbyService =
            await WaitForLobbyServiceAsync(
                runtimeType);

        if (activeLobbyService == null)
        {
            CompleteLobbyEntryFailure(
                LobbyEntryResult.Failed(
                    LobbyEntryFailureType.ServiceUnavailable,
                    "The required lobby manager was not ready."));

            return;
        }

        SetEntryState(
            runtimeType == SessionRuntimeType.Network
                ? LobbyEntryState.Connecting
                : LobbyEntryState.Searching);

        LobbyEntryResult result;

        try
        {
            result =
                await activeLobbyService
                    .EnterLobbyAsync(
                        pendingLobbySetupData);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            result = LobbyEntryResult.Failed(
                LobbyEntryFailureType.Unknown,
                "An unexpected error occurred while entering the lobby.");
        }

        if (result == null || !result.success)
        {
            CompleteLobbyEntryFailure(
                result ??
                LobbyEntryResult.Failed(
                    LobbyEntryFailureType.Unknown,
                    "The lobby manager did not return a result."));

            return;
        }

        if (result.lobby == null)
        {
            CompleteLobbyEntryFailure(
                LobbyEntryResult.Failed(
                    LobbyEntryFailureType.LobbyJoinFailed,
                    "The lobby was not returned after the player joined."));

            return;
        }

        UserData userData =
            pendingLobbySetupData.userData;

        if (userData == null ||
            result.lobby.Controller == null ||
            !result.lobby.Controller.HasPlayer(
                userData.userId))
        {
            CompleteLobbyEntryFailure(
                LobbyEntryResult.Failed(
                    LobbyEntryFailureType.LobbyJoinFailed,
                    "The player was not added to the lobby."));

            return;
        }

        currentLobby = result.lobby;
        currentUserId = userData.userId;
        lastEntryResult = result;

        pendingLobbySetupData = null;
        isEnteringLobby = false;

        SubscribeToNetworkEvents();

        SetEntryState(LobbyEntryState.Completed);

        LobbyEntryCompleted?.Invoke(result);
    }

    private async Task<ILobbyService> WaitForLobbyServiceAsync(
        SessionRuntimeType selectedRuntimeType)
    {
        float timeoutTime =
            Time.realtimeSinceStartup +
            ServiceReadyTimeoutSeconds;

        while (Time.realtimeSinceStartup < timeoutTime)
        {
            ILobbyService lobbyService =
                GetLobbyService(
                    selectedRuntimeType);

            if (lobbyService != null &&
                lobbyService.IsReady)
            {
                return lobbyService;
            }

            await Task.Yield();
        }

        return null;
    }

    private ILobbyService GetLobbyService(
        SessionRuntimeType selectedRuntimeType)
    {
        switch (selectedRuntimeType)
        {
            case SessionRuntimeType.Local:
                if (localLobbyManager == null)
                {
                    localLobbyManager =
                        LocalLobbyManager.instance;
                }

                return localLobbyManager;

            case SessionRuntimeType.Network:
                if (networkLobbyManager == null)
                {
                    networkLobbyManager =
                        NetworkLobbyManager.instance;
                }

                return networkLobbyManager;

            default:
                return null;
        }
    }

    private SessionRuntimeType GetRuntimeType(
        MainMenuPlayMode playMode)
    {
        switch (playMode)
        {
            case MainMenuPlayMode.Solo:
                return SessionRuntimeType.Local;

            case MainMenuPlayMode.Online:
            case MainMenuPlayMode.Custom:
                return SessionRuntimeType.Network;

            default:
                return SessionRuntimeType.Local;
        }
    }

    private bool TryValidatePendingSetupData(
        out LobbyEntryResult failureResult)
    {
        failureResult = null;

        if (currentLobby != null)
        {
            failureResult = LobbyEntryResult.Failed(
                LobbyEntryFailureType.AlreadyInLobby,
                "The current player must leave the existing lobby first.");

            return false;
        }

        if (pendingLobbySetupData == null)
        {
            failureResult = LobbyEntryResult.Failed(
                LobbyEntryFailureType.InvalidSetupData,
                "The lobby setup data is missing.");

            return false;
        }

        if (pendingLobbySetupData.playMode ==
            MainMenuPlayMode.None)
        {
            failureResult = LobbyEntryResult.Failed(
                LobbyEntryFailureType.InvalidSetupData,
                "The lobby mode is not valid.");

            return false;
        }

        if (pendingLobbySetupData.userData == null ||
            !pendingLobbySetupData.userData.HasUser)
        {
            failureResult = LobbyEntryResult.Failed(
                LobbyEntryFailureType.UserMissing,
                "The current user is missing.");

            return false;
        }

        return true;
    }

    #endregion

    #region Lobby Exit

    public async void LeaveCurrentLobby()
    {
        if (isLeavingLobby || isEnteringLobby)
        {
            return;
        }

        if (currentLobby == null)
        {
            ReturnToMainScene();
            return;
        }

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            CompleteLobbyExitFailure(
                LobbyExitResult.Failed(
                    currentUserId,
                    LobbyPlayerExitReason.VoluntaryLeave,
                    "The current lobby player could not be resolved."));

            return;
        }

        ILobbyService lobbyService =
            GetLobbyService(runtimeType);

        if (lobbyService == null ||
            !lobbyService.IsReady)
        {
            CompleteLobbyExitFailure(
                LobbyExitResult.Failed(
                    currentUserId,
                    LobbyPlayerExitReason.VoluntaryLeave,
                    "The lobby service was not ready."));

            return;
        }

        isLeavingLobby = true;

        LobbyExitResult result;

        try
        {
            result =
                await lobbyService
                    .LeaveLobbyAsync(
                        currentUserId);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            result = LobbyExitResult.Failed(
                currentUserId,
                LobbyPlayerExitReason.VoluntaryLeave,
                "An unexpected error occurred while leaving the lobby.");
        }

        if (result == null || !result.success)
        {
            CompleteLobbyExitFailure(
                result ??
                LobbyExitResult.Failed(
                    currentUserId,
                    LobbyPlayerExitReason.VoluntaryLeave,
                    "The lobby service did not return a leave result."));

            return;
        }

        ClearCurrentLobby();

        isLeavingLobby = false;
        SetEntryState(LobbyEntryState.Idle);

        LobbyExitCompleted?.Invoke(result);

        ReturnToMainScene();
    }

    private void CompleteLobbyExitFailure(
        LobbyExitResult result)
    {
        isLeavingLobby = false;

        LobbyExitResult finalResult =
            result ??
            LobbyExitResult.Failed(
                currentUserId,
                LobbyPlayerExitReason.VoluntaryLeave,
                "The player could not leave the lobby.");

        LobbyExitFailed?.Invoke(finalResult);

        Debug.LogWarning(
            "[LobbyManager] Lobby exit failed. " +
            $"Message: {finalResult.failureMessage}");
    }

    private void OnLocalLobbyExitReceived(
        LobbyExitNotification notification)
    {
        HandleForcedLobbyExit(notification);
    }

    private void OnNetworkConnectionStateChanged(
        NetworkConnectionState connectionState)
    {
        if (runtimeType != SessionRuntimeType.Network ||
            currentLobby == null ||
            isLeavingLobby)
        {
            return;
        }

        if (connectionState !=
                NetworkConnectionState.Disconnected &&
            connectionState !=
                NetworkConnectionState.Failed)
        {
            return;
        }

        HandleForcedLobbyExit(
            LobbyExitNotification.ConnectionLost(
                currentLobby.GetLobbyId()));
    }

    private void HandleForcedLobbyExit(
        LobbyExitNotification notification)
    {
        if (notification == null ||
            currentLobby == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(
                notification.lobbyId) &&
            !string.Equals(
                notification.lobbyId,
                currentLobby.GetLobbyId(),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ClearCurrentLobby();

        isEnteringLobby = false;
        isLeavingLobby = false;

        SetEntryState(LobbyEntryState.Failed);

        LobbyForcedExit?.Invoke(notification);

        ReturnToMainScene();
    }

    private void ClearCurrentLobby()
    {
        currentLobby = null;
        currentUserId = string.Empty;
        activeLobbyService = null;
        lastEntryResult = null;
    }

    private void ReturnToMainScene()
    {
        GameSceneManager gameSceneManager =
            GameSceneManager.instance;

        if (gameSceneManager == null)
        {
            Debug.LogWarning(
                "[LobbyManager] Could not return to Main because GameSceneManager was not found.");

            return;
        }

        if (gameSceneManager.IsLoadingScene)
        {
            return;
        }

        gameSceneManager.LoadMainScene();
    }

    #endregion

    #region Network Events

    private void SubscribeToNetworkEvents()
    {
        NetworkLobbyConnection.LocalLobbyExitReceived -=
            OnLocalLobbyExitReceived;

        NetworkLobbyConnection.LocalLobbyExitReceived +=
            OnLocalLobbyExitReceived;

        if (isSubscribedToNetworkBootstrap)
        {
            return;
        }

        if (networkBootstrap == null)
        {
            networkBootstrap =
                NetworkBootstrap.instance;
        }

        if (networkBootstrap == null)
        {
            return;
        }

        networkBootstrap.ConnectionStateChanged +=
            OnNetworkConnectionStateChanged;

        isSubscribedToNetworkBootstrap = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        NetworkLobbyConnection.LocalLobbyExitReceived -=
            OnLocalLobbyExitReceived;

        if (isSubscribedToNetworkBootstrap &&
            networkBootstrap != null)
        {
            networkBootstrap.ConnectionStateChanged -=
                OnNetworkConnectionStateChanged;
        }

        isSubscribedToNetworkBootstrap = false;
    }

    #endregion

    #region Failure Shell

    private void CompleteLobbyEntryFailure(
        LobbyEntryResult result)
    {
        isEnteringLobby = false;

        lastEntryResult =
            result ??
            LobbyEntryResult.Failed(
                LobbyEntryFailureType.Unknown,
                "The lobby could not be entered.");

        SetEntryState(LobbyEntryState.Failed);

        HandleLobbyEntryFailure(lastEntryResult);
    }

    private void HandleLobbyEntryFailure(
        LobbyEntryResult result)
    {
        if (result == null)
        {
            return;
        }

        LobbyEntryFailed?.Invoke(result);

        Debug.LogWarning(
            $"[LobbyManager] Lobby entry failed. " +
            $"Type: {result.failureType}. " +
            $"Message: {result.failureMessage}");
    }

    #endregion

    #region State

    private void SetEntryState(
        LobbyEntryState newState)
    {
        if (entryState == newState)
        {
            return;
        }

        entryState = newState;

        LobbyEntryStateChanged?.Invoke(
            entryState);
    }

    #endregion
}
