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

    private SessionRuntimeType runtimeType =
        SessionRuntimeType.Local;

    private LobbyEntryState entryState =
        LobbyEntryState.Idle;

    private LobbyEntryResult lastEntryResult;

    private bool isEnteringLobby;

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

    public bool HasEnteredLobby =>
        entryState == LobbyEntryState.Completed &&
        currentLobby != null;

    public event Action<LobbyEntryState>
        LobbyEntryStateChanged;

    public event Action<LobbyEntryResult>
        LobbyEntryCompleted;

    public event Action<LobbyEntryResult>
        LobbyEntryFailed;


    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null &&
            instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
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

        pendingLobbySetupData =
            lobbySetupData;

        currentLobby = null;
        lastEntryResult = null;

        SetEntryState(LobbyEntryState.Idle);
    }

    public void ClearPendingLobbySetupData()
    {
        pendingLobbySetupData = null;
    }

    #endregion

    #region Lobby Entry

    public async void BeginPendingLobbyEntry()
    {
        if (isEnteringLobby)
        {
            return;
        }

        isEnteringLobby = true;

        currentLobby = null;
        lastEntryResult = null;

        if (!TryValidatePendingSetupData(
                out LobbyEntryResult validationFailure))
        {
            CompleteLobbyEntryFailure(
                validationFailure);

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
                    LobbyEntryFailureType
                        .ServiceUnavailable,
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

            result =
                LobbyEntryResult.Failed(
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
                    LobbyEntryFailureType
                        .LobbyJoinFailed,
                    "The lobby was not returned after the player joined."));

            return;
        }

        UserData userData =
            pendingLobbySetupData.userData;

        if (userData == null ||
            !result.lobby.HasPlayer(userData.userId))
        {
            CompleteLobbyEntryFailure(
                LobbyEntryResult.Failed(
                    LobbyEntryFailureType
                        .LobbyJoinFailed,
                    "The player was not added to the lobby."));

            return;
        }

        currentLobby = result.lobby;
        lastEntryResult = result;

        pendingLobbySetupData = null;
        isEnteringLobby = false;

        SetEntryState(
            LobbyEntryState.Completed);

        LobbyEntryCompleted?.Invoke(result);
    }

    private async Task<ILobbyService>
        WaitForLobbyServiceAsync(
            SessionRuntimeType selectedRuntimeType)
    {
        float timeoutTime =
            Time.realtimeSinceStartup +
            ServiceReadyTimeoutSeconds;

        while (Time.realtimeSinceStartup <
               timeoutTime)
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

        if (pendingLobbySetupData == null)
        {
            failureResult =
                LobbyEntryResult.Failed(
                    LobbyEntryFailureType
                        .InvalidSetupData,
                    "The lobby setup data is missing.");

            return false;
        }

        if (pendingLobbySetupData.playMode ==
            MainMenuPlayMode.None)
        {
            failureResult =
                LobbyEntryResult.Failed(
                    LobbyEntryFailureType
                        .InvalidSetupData,
                    "The lobby mode is not valid.");

            return false;
        }

        if (pendingLobbySetupData.userData == null ||
            !pendingLobbySetupData.userData.HasUser)
        {
            failureResult =
                LobbyEntryResult.Failed(
                    LobbyEntryFailureType.UserMissing,
                    "The current user is missing.");

            return false;
        }

        return true;
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

        SetEntryState(
            LobbyEntryState.Failed);

        LobbyEntryFailed?.Invoke(
            lastEntryResult);

        HandleLobbyEntryFailure(
            lastEntryResult);
    }

    private void HandleLobbyEntryFailure(
        LobbyEntryResult result)
    {
        // Failure scene transition, persistent loading UI,
        // and popup handling will be added in the next phase.

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