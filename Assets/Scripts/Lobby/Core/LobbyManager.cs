using System;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbyManager : MonoBehaviour
{
    #region Fields

    public static LobbyManager instance;

    private const float ServiceReadyTimeoutSeconds = 15f;

    private LobbySetupData pendingLobbySetupData;

    private LocalLobbyManager localLobbyManager;
    private NetworkLobbyService networkLobbyService;

    private ILobbyService activeLobbyService;

    private readonly LobbyClientState lobbyClientState = new LobbyClientState();

    private Lobby currentLobby;
    private LobbyController subscribedLocalLobbyController;
    private LobbyViewData pendingNetworkLobbyViewData;
    private string currentUserId = string.Empty;

    private SessionRuntimeType runtimeType = SessionRuntimeType.Local;
    private LobbyEntryState entryState = LobbyEntryState.Idle;

    private LobbyEntryResult lastEntryResult;

    private bool isEnteringLobby;
    private bool isLeavingLobby;
    private bool isLobbyResyncPending;
    private int entryAttemptVersion;

    private MultiplayerSessionLifecycle multiplayerSessionLifecycle;
    private bool isSubscribedToMultiplayerSessionLifecycle;
    private GameSceneManager gameSceneManager;
    private bool isSubscribedToGameSceneManager;

    public bool HasPendingLobbySetupData => pendingLobbySetupData != null;
    public LobbySetupData PendingLobbySetupData => pendingLobbySetupData;
    public SessionRuntimeType RuntimeType => runtimeType;
    public LobbyEntryState EntryState => entryState;

    public Lobby CurrentLobby => currentLobby;
    public string CurrentLobbyId => lobbyClientState.LobbyId;
    public LobbyViewData CurrentLobbyViewData => lobbyClientState.ViewData;
    public LobbyEntryResult LastEntryResult => lastEntryResult;
    public string CurrentUserId => currentUserId;

    public bool IsEnteringLobby => isEnteringLobby;
    public bool IsLeavingLobby => isLeavingLobby;
    public bool HasEnteredLobby => entryState == LobbyEntryState.Completed && lobbyClientState.HasLobby;

    public event Action<LobbyEntryState> LobbyEntryStateChanged;
    public event Action<LobbyEntryResult> LobbyEntryCompleted;
    public event Action<LobbyEntryResult> LobbyEntryFailed;

    public event Action<LobbyExitResult> LobbyExitCompleted;
    public event Action<LobbyExitResult> LobbyExitFailed;
    public event Action<LobbyExitNotification> LobbyForcedExit;
    public event Action<LobbyViewData> LobbyViewUpdated;
    public event Action<LobbyPlayerBoardUpdateData> LobbyPlayerBoardUpdated;

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
        SubscribeToNetworkEvents();
        SubscribeToSceneEvents();
    }

    private void Start()
    {
        SubscribeToNetworkEvents();
        SubscribeToSceneEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
        UnsubscribeFromSceneEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromNetworkEvents();
        UnsubscribeFromSceneEvents();
        UnsubscribeFromLocalLobbyController();

        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Setup Data

    public void SetPendingLobbySetupData(LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null)
        {
            Debug.LogWarning("[LobbyManager] Cannot set pending lobby setup data because the data is null.");

            return;
        }

        LobbySaveDataService.ApplySavedDataToSetup(lobbySetupData);

        pendingLobbySetupData = lobbySetupData;
        lastEntryResult = null;

        if (!lobbyClientState.HasLobby)
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

        int currentEntryAttemptVersion = ++entryAttemptVersion;

        if (!TryValidatePendingSetupData(out LobbyEntryResult validationFailure))
        {
            CompleteLobbyEntryFailure(validationFailure);
            return;
        }

        runtimeType =
            GetRuntimeType(pendingLobbySetupData.playMode);

        SetEntryState(LobbyEntryState.WaitingForService);

        activeLobbyService =
            await WaitForLobbyServiceAsync(runtimeType);

        if (currentEntryAttemptVersion != entryAttemptVersion)
        {
            return;
        }

        if (activeLobbyService == null)
        {
            CompleteLobbyEntryFailure(
                LobbyEntryResult.Failed(
                    LobbyEntryFailureType.ServiceUnavailable,
                    "The required lobby manager was not ready."));

            return;
        }

        SetEntryState(runtimeType == SessionRuntimeType.Network ? LobbyEntryState.Connecting : LobbyEntryState.Searching);

        LobbyEntryResult result;

        try
        {
            result =
                await activeLobbyService
                    .EnterLobbyAsync(pendingLobbySetupData);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            result = LobbyEntryResult.Failed(LobbyEntryFailureType.Unknown, "An unexpected error occurred while entering the lobby.");
        }

        if (currentEntryAttemptVersion != entryAttemptVersion)
        {
            return;
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

        UserData userData = pendingLobbySetupData.userData;

        if (userData == null || string.IsNullOrWhiteSpace(result.lobbyId) || result.lobbyViewData == null || result.lobbyBoardData == null)
        {
            CompleteLobbyEntryFailure(LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyJoinFailed, "The lobby data was not returned after the player joined."));
            return;
        }

        if (runtimeType == SessionRuntimeType.Local)
        {
            if (result.localLobby?.Controller == null || !result.localLobby.Controller.HasPlayer(userData.userId))
            {
                CompleteLobbyEntryFailure(LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyJoinFailed, "The player was not added to the local lobby."));
                return;
            }

            currentLobby = result.localLobby;
        }
        else
        {
            currentLobby = null;
        }

        if (!lobbyClientState.SetSnapshot(result.lobbyId, result.lobbyViewData, result.revision) || !lobbyClientState.SetBoardSnapshot(result.lobbyBoardData))
        {
            currentLobby = null;
            CompleteLobbyEntryFailure(LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyJoinFailed, "The returned lobby state could not be applied."));
            return;
        }

        currentUserId = userData.userId;
        SubscribeToLocalLobbyController(currentLobby?.Controller);
        lastEntryResult = result;

        ApplyPendingNetworkLobbyViewData();
        PublishCurrentLobbyView();

        if (runtimeType == SessionRuntimeType.Network)
        {
            networkLobbyService?.RequestLobbyInitialSync();
        }

        pendingLobbySetupData = null;
        isEnteringLobby = false;

        SubscribeToNetworkEvents();

        SetEntryState(LobbyEntryState.Completed);

        LobbyEntryCompleted?.Invoke(result);
    }

    private async Task<ILobbyService> WaitForLobbyServiceAsync(SessionRuntimeType selectedRuntimeType)
    {
        float timeoutTime =
            Time.realtimeSinceStartup +
            ServiceReadyTimeoutSeconds;

        while (Time.realtimeSinceStartup < timeoutTime)
        {
            ILobbyService lobbyService =
                GetLobbyService(selectedRuntimeType);

            if (lobbyService != null && lobbyService.IsReady)
            {
                return lobbyService;
            }

            await Task.Yield();
        }

        return null;
    }

    private ILobbyService GetLobbyService(SessionRuntimeType selectedRuntimeType)
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
                if (networkLobbyService == null)
                {
                    networkLobbyService = NetworkLobbyService.instance;
                }

                return networkLobbyService;

            default:
                return null;
        }
    }

    private SessionRuntimeType GetRuntimeType(MainMenuPlayMode playMode)
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

    private bool TryValidatePendingSetupData(out LobbyEntryResult failureResult)
    {
        failureResult = null;

        if (lobbyClientState.HasLobby)
        {
            failureResult = LobbyEntryResult.Failed(LobbyEntryFailureType.AlreadyInLobby, "The current player must leave the existing lobby first.");

            return false;
        }

        if (pendingLobbySetupData == null)
        {
            failureResult = LobbyEntryResult.Failed(LobbyEntryFailureType.InvalidSetupData, "The lobby setup data is missing.");

            return false;
        }

        if (pendingLobbySetupData.playMode == MainMenuPlayMode.None)
        {
            failureResult = LobbyEntryResult.Failed(LobbyEntryFailureType.InvalidSetupData, "The lobby mode is not valid.");

            return false;
        }

        if (pendingLobbySetupData.userData == null || !pendingLobbySetupData.userData.HasUser)
        {
            failureResult = LobbyEntryResult.Failed(LobbyEntryFailureType.UserMissing, "The current user is missing.");

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

        if (!lobbyClientState.HasLobby)
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

        if (lobbyService == null || !lobbyService.IsReady)
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
                    .LeaveLobbyAsync(currentUserId);
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

    private void CompleteLobbyExitFailure(LobbyExitResult result)
    {
        isLeavingLobby = false;

        LobbyExitResult finalResult =
            result ??
            LobbyExitResult.Failed(currentUserId, LobbyPlayerExitReason.VoluntaryLeave, "The player could not leave the lobby.");

        LobbyExitFailed?.Invoke(finalResult);

        Debug.LogWarning("[LobbyManager] Lobby exit failed. " + $"Message: {finalResult.failureMessage}");
    }

    private void OnLocalLobbyExitReceived(LobbyExitNotification notification)
    {
        HandleForcedLobbyExit(notification);
    }

    private void OnLocalLobbyViewReceived(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData == null || string.IsNullOrWhiteSpace(lobbyViewData.lobbyId))
        {
            return;
        }

        if (!lobbyClientState.HasLobby)
        {
            pendingNetworkLobbyViewData = lobbyViewData;
            return;
        }

        if (!lobbyClientState.IsCurrentLobby(lobbyViewData.lobbyId) || !lobbyClientState.SetSnapshot(lobbyClientState.LobbyId, lobbyViewData))
        {
            return;
        }

        PublishCurrentLobbyView();
    }

    private void OnLocalPlayerBoardUpdateReceived(LobbyPlayerBoardUpdateData updateData)
    {
        if (updateData == null || lobbyClientState.ViewData == null || !lobbyClientState.IsCurrentLobby(updateData.lobbyId) || isLobbyResyncPending)
        {
            return;
        }

        LobbyStateApplyResult applyResult = lobbyClientState.ApplyNetworkBoardUpdate(updateData);

        if (!HandleNetworkApplyResult(applyResult))
        {
            return;
        }

        LobbyPlayerBoardUpdated?.Invoke(updateData);
    }

    private void OnLocalPlayerBoardCollectionReceived(LobbyBoardCollectionData boardCollectionData)
    {
        if (boardCollectionData == null || !lobbyClientState.IsCurrentLobby(boardCollectionData.lobbyId) || isLobbyResyncPending)
        {
            return;
        }

        LobbyStateApplyResult applyResult = lobbyClientState.ApplyNetworkBoardCollection(boardCollectionData);

        if (!HandleNetworkApplyResult(applyResult) || boardCollectionData.boards == null)
        {
            return;
        }

        for (int i = 0; i < boardCollectionData.boards.Count; i++)
        {
            LobbyPlayerBoardViewData playerBoard = boardCollectionData.boards[i];

            if (playerBoard == null)
            {
                continue;
            }

            LobbyPlayerBoardUpdated?.Invoke(new LobbyPlayerBoardUpdateData(boardCollectionData.lobbyId, playerBoard.userId, playerBoard.boardData));
        }
    }

    private void OnLocalPlayerJoinedReceived(LobbyPlayerJoinedData data)
    {
        if (data == null || isLobbyResyncPending)
        {
            return;
        }

        if (HandleNetworkApplyResult(lobbyClientState.ApplyPlayerJoined(data)))
        {
            PublishCurrentLobbyView();
        }
    }

    private void OnLocalPlayerJoinedBatchReceived(LobbyPlayerJoinedBatchData data)
    {
        if (data == null || isLobbyResyncPending)
        {
            return;
        }

        if (HandleNetworkApplyResult(lobbyClientState.ApplyPlayerJoinedBatch(data)))
        {
            PublishCurrentLobbyView();
        }
    }

    private void OnLocalLobbyInitialSyncBatchReceived(LobbyInitialSyncBatchData data)
    {
        if (data == null || !lobbyClientState.IsCurrentLobby(data.lobbyId))
        {
            return;
        }

        if (!lobbyClientState.ApplyInitialSyncBatch(data))
        {
            return;
        }

        if (data.isFinalBatch)
        {
            isLobbyResyncPending = false;
        }

        PublishCurrentLobbyView();

        if (data.boards == null)
        {
            return;
        }

        for (int i = 0; i < data.boards.Count; i++)
        {
            LobbyPlayerBoardViewData boardData = data.boards[i];

            if (boardData != null)
            {
                LobbyPlayerBoardUpdated?.Invoke(new LobbyPlayerBoardUpdateData(data.lobbyId, boardData.userId, boardData.boardData));
            }
        }
    }

    private void OnLocalPlayerLeftReceived(LobbyPlayerLeftData data)
    {
        if (data == null || isLobbyResyncPending)
        {
            return;
        }

        if (HandleNetworkApplyResult(lobbyClientState.ApplyPlayerLeft(data)))
        {
            PublishCurrentLobbyView();
        }
    }

    private void OnLocalPlayerReadyChangedReceived(LobbyPlayerReadyChangedData data)
    {
        if (data == null || isLobbyResyncPending)
        {
            return;
        }

        if (HandleNetworkApplyResult(lobbyClientState.ApplyPlayerReadyChanged(data)))
        {
            PublishCurrentLobbyView();
        }
    }

    private void OnLocalLobbySettingsChangedReceived(LobbySettingsChangedData data)
    {
        if (data == null || isLobbyResyncPending)
        {
            return;
        }

        if (HandleNetworkApplyResult(lobbyClientState.ApplySettingsChanged(data)))
        {
            PublishCurrentLobbyView();
        }
    }

    private void OnLocalLobbyStateChangedReceived(LobbyStateChangedData data)
    {
        if (data == null || isLobbyResyncPending)
        {
            return;
        }

        if (HandleNetworkApplyResult(lobbyClientState.ApplyStateChanged(data)))
        {
            PublishCurrentLobbyView();
        }
    }

    private void OnLocalLobbySyncSnapshotReceived(LobbySyncSnapshotData snapshotData)
    {
        if (snapshotData == null || string.IsNullOrWhiteSpace(snapshotData.lobbyId) || !lobbyClientState.IsCurrentLobby(snapshotData.lobbyId) || snapshotData.revision < lobbyClientState.Revision)
        {
            return;
        }

        if (!lobbyClientState.SetSnapshot(snapshotData.lobbyId, snapshotData.lobbyViewData, snapshotData.revision) ||
            !lobbyClientState.SetBoardSnapshot(snapshotData.lobbyBoardData))
        {
            return;
        }

        isLobbyResyncPending = false;
        PublishCurrentLobbyView();
    }

    private bool HandleNetworkApplyResult(LobbyStateApplyResult applyResult)
    {
        switch (applyResult)
        {
            case LobbyStateApplyResult.Applied:
                return true;

            case LobbyStateApplyResult.RequiresResync:
                RequestLobbyResync();
                return false;

            case LobbyStateApplyResult.Ignored:
            case LobbyStateApplyResult.Invalid:
            default:
                return false;
        }
    }

    private void RequestLobbyResync()
    {
        if (runtimeType != SessionRuntimeType.Network || !lobbyClientState.HasLobby || isLobbyResyncPending)
        {
            return;
        }

        NetworkLobbyService lobbyService = NetworkLobbyService.instance;

        if (lobbyService == null)
        {
            return;
        }

        isLobbyResyncPending = true;
        lobbyService.RequestLobbyResync();
    }

    private void OnMultiplayerConnectionLost(NetworkConnectionState _)
    {
        HandleMultiplayerConnectionLost();
    }

    public void HandleMultiplayerConnectionLost()
    {
        if (runtimeType != SessionRuntimeType.Network || isLeavingLobby)
        {
            return;
        }

        entryAttemptVersion++;

        if (lobbyClientState.HasLobby)
        {
            HandleForcedLobbyExit(LobbyExitNotification.ConnectionLost(lobbyClientState.LobbyId));
            return;
        }

        if (!isEnteringLobby && entryState != LobbyEntryState.Connecting &&
            entryState != LobbyEntryState.Searching &&
            entryState != LobbyEntryState.Joining &&
            entryState != LobbyEntryState.AddingPlayer)
        {
            return;
        }

        isEnteringLobby = false;
        pendingLobbySetupData = null;
        pendingNetworkLobbyViewData = null;
        activeLobbyService = null;

        LobbyEntryResult failureResult = LobbyEntryResult.Failed(
            LobbyEntryFailureType.ConnectionLost,
            "The multiplayer connection was lost.");

        lastEntryResult = failureResult;
        SetEntryState(LobbyEntryState.Failed);
        LobbyEntryFailed?.Invoke(failureResult);
        ReturnToMainScene();
    }

    private void HandleForcedLobbyExit(LobbyExitNotification notification)
    {
        if (notification == null || !lobbyClientState.HasLobby)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(notification.lobbyId) && !lobbyClientState.IsCurrentLobby(notification.lobbyId))
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
        UnsubscribeFromLocalLobbyController();
        currentLobby = null;
        lobbyClientState.Clear();
        pendingNetworkLobbyViewData = null;
        isLobbyResyncPending = false;
        currentUserId = string.Empty;
        activeLobbyService = null;
        lastEntryResult = null;
    }

    private void ApplyPendingNetworkLobbyViewData()
    {
        if (!lobbyClientState.HasLobby || pendingNetworkLobbyViewData == null)
        {
            return;
        }

        if (lobbyClientState.IsCurrentLobby(pendingNetworkLobbyViewData.lobbyId))
        {
            lobbyClientState.SetSnapshot(lobbyClientState.LobbyId, pendingNetworkLobbyViewData);
        }

        pendingNetworkLobbyViewData = null;
    }

    private void PublishCurrentLobbyView()
    {
        if (lobbyClientState.ViewData != null)
        {
            LobbyViewUpdated?.Invoke(lobbyClientState.ViewData);
        }
    }

    public LobbyBoardData GetPlayerBoard(string userId)
    {
        return lobbyClientState.GetPlayerBoard(userId);
    }

    private void ReturnToMainScene()
    {
        GameSceneManager gameSceneManager =
            GameSceneManager.instance;

        if (gameSceneManager == null)
        {
            Debug.LogWarning("[LobbyManager] Could not return to Main because GameSceneManager was not found.");

            return;
        }

        gameSceneManager.ReturnToMainSceneAfterFailure();
    }

    #endregion

    #region Local Lobby Board Events

    private void SubscribeToLocalLobbyController(LobbyController controller)
    {
        if (subscribedLocalLobbyController == controller)
        {
            return;
        }

        UnsubscribeFromLocalLobbyController();
        subscribedLocalLobbyController = controller;

        if (subscribedLocalLobbyController != null)
        {
            subscribedLocalLobbyController.PlayerBoardChanged += OnLocalLobbyPlayerBoardChanged;
        }
    }

    private void UnsubscribeFromLocalLobbyController()
    {
        if (subscribedLocalLobbyController != null)
        {
            subscribedLocalLobbyController.PlayerBoardChanged -= OnLocalLobbyPlayerBoardChanged;
            subscribedLocalLobbyController = null;
        }
    }

    private void OnLocalLobbyPlayerBoardChanged(LobbyController controller, LobbyPlayerBoardViewData playerBoard)
    {
        if (controller == null || controller != subscribedLocalLobbyController || playerBoard == null || !lobbyClientState.HasLobby)
        {
            return;
        }

        LobbyPlayerBoardUpdateData updateData = new LobbyPlayerBoardUpdateData(lobbyClientState.LobbyId, playerBoard.userId, playerBoard.boardData);

        if (!lobbyClientState.ApplyBoardUpdate(updateData))
        {
            return;
        }

        LobbyPlayerBoardUpdated?.Invoke(updateData);
    }

    #endregion

    #region Network Events

    private void SubscribeToNetworkEvents()
    {
        NetworkLobbyConnection.LocalLobbyExitReceived -= OnLocalLobbyExitReceived;
        NetworkLobbyConnection.LocalLobbyExitReceived += OnLocalLobbyExitReceived;
        NetworkLobbyConnection.LocalLobbyViewReceived -= OnLocalLobbyViewReceived;
        NetworkLobbyConnection.LocalLobbyViewReceived += OnLocalLobbyViewReceived;
        NetworkLobbyConnection.LocalPlayerBoardUpdateReceived -= OnLocalPlayerBoardUpdateReceived;
        NetworkLobbyConnection.LocalPlayerBoardUpdateReceived += OnLocalPlayerBoardUpdateReceived;
        NetworkLobbyConnection.LocalPlayerBoardCollectionReceived -= OnLocalPlayerBoardCollectionReceived;
        NetworkLobbyConnection.LocalPlayerBoardCollectionReceived += OnLocalPlayerBoardCollectionReceived;
        NetworkLobbyConnection.LocalPlayerJoinedReceived -= OnLocalPlayerJoinedReceived;
        NetworkLobbyConnection.LocalPlayerJoinedReceived += OnLocalPlayerJoinedReceived;
        NetworkLobbyConnection.LocalPlayerJoinedBatchReceived -= OnLocalPlayerJoinedBatchReceived;
        NetworkLobbyConnection.LocalPlayerJoinedBatchReceived += OnLocalPlayerJoinedBatchReceived;
        NetworkLobbyConnection.LocalLobbyInitialSyncBatchReceived -= OnLocalLobbyInitialSyncBatchReceived;
        NetworkLobbyConnection.LocalLobbyInitialSyncBatchReceived += OnLocalLobbyInitialSyncBatchReceived;
        NetworkLobbyConnection.LocalPlayerLeftReceived -= OnLocalPlayerLeftReceived;
        NetworkLobbyConnection.LocalPlayerLeftReceived += OnLocalPlayerLeftReceived;
        NetworkLobbyConnection.LocalPlayerReadyChangedReceived -= OnLocalPlayerReadyChangedReceived;
        NetworkLobbyConnection.LocalPlayerReadyChangedReceived += OnLocalPlayerReadyChangedReceived;
        NetworkLobbyConnection.LocalLobbySettingsChangedReceived -= OnLocalLobbySettingsChangedReceived;
        NetworkLobbyConnection.LocalLobbySettingsChangedReceived += OnLocalLobbySettingsChangedReceived;
        NetworkLobbyConnection.LocalLobbyStateChangedReceived -= OnLocalLobbyStateChangedReceived;
        NetworkLobbyConnection.LocalLobbyStateChangedReceived += OnLocalLobbyStateChangedReceived;
        NetworkLobbyConnection.LocalLobbySyncSnapshotReceived -= OnLocalLobbySyncSnapshotReceived;
        NetworkLobbyConnection.LocalLobbySyncSnapshotReceived += OnLocalLobbySyncSnapshotReceived;

        if (isSubscribedToMultiplayerSessionLifecycle)
        {
            return;
        }

        if (multiplayerSessionLifecycle == null)
        {
            multiplayerSessionLifecycle = MultiplayerSessionLifecycle.instance;
        }

        if (multiplayerSessionLifecycle == null)
        {
            return;
        }

        multiplayerSessionLifecycle.ConnectionLost += OnMultiplayerConnectionLost;
        isSubscribedToMultiplayerSessionLifecycle = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        NetworkLobbyConnection.LocalLobbyExitReceived -= OnLocalLobbyExitReceived;
        NetworkLobbyConnection.LocalLobbyViewReceived -= OnLocalLobbyViewReceived;
        NetworkLobbyConnection.LocalPlayerBoardUpdateReceived -= OnLocalPlayerBoardUpdateReceived;
        NetworkLobbyConnection.LocalPlayerBoardCollectionReceived -= OnLocalPlayerBoardCollectionReceived;
        NetworkLobbyConnection.LocalPlayerJoinedReceived -= OnLocalPlayerJoinedReceived;
        NetworkLobbyConnection.LocalPlayerJoinedBatchReceived -= OnLocalPlayerJoinedBatchReceived;
        NetworkLobbyConnection.LocalLobbyInitialSyncBatchReceived -= OnLocalLobbyInitialSyncBatchReceived;
        NetworkLobbyConnection.LocalPlayerLeftReceived -= OnLocalPlayerLeftReceived;
        NetworkLobbyConnection.LocalPlayerReadyChangedReceived -= OnLocalPlayerReadyChangedReceived;
        NetworkLobbyConnection.LocalLobbySettingsChangedReceived -= OnLocalLobbySettingsChangedReceived;
        NetworkLobbyConnection.LocalLobbyStateChangedReceived -= OnLocalLobbyStateChangedReceived;
        NetworkLobbyConnection.LocalLobbySyncSnapshotReceived -= OnLocalLobbySyncSnapshotReceived;

        if (isSubscribedToMultiplayerSessionLifecycle && multiplayerSessionLifecycle != null)
        {
            multiplayerSessionLifecycle.ConnectionLost -= OnMultiplayerConnectionLost;
        }

        isSubscribedToMultiplayerSessionLifecycle = false;
    }

    #endregion

    #region Scene Events

    private void SubscribeToSceneEvents()
    {
        if (isSubscribedToGameSceneManager)
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

        gameSceneManager.SceneReadyForFadeOut += OnSceneReadyForFadeOut;
        isSubscribedToGameSceneManager = true;
    }

    private void UnsubscribeFromSceneEvents()
    {
        if (isSubscribedToGameSceneManager && gameSceneManager != null)
        {
            gameSceneManager.SceneReadyForFadeOut -= OnSceneReadyForFadeOut;
        }

        isSubscribedToGameSceneManager = false;
    }

    private void OnSceneReadyForFadeOut(GameSceneType sceneType)
    {
        if (sceneType != GameSceneType.Lobby || runtimeType != SessionRuntimeType.Network || !lobbyClientState.HasLobby || entryState != LobbyEntryState.Completed)
        {
            return;
        }

        NetworkLobbyService lobbyService = NetworkLobbyService.instance;
        lobbyService?.NotifyLobbySceneReady();
    }

    #endregion

    #region Failure Shell

    private void CompleteLobbyEntryFailure(LobbyEntryResult result)
    {
        isEnteringLobby = false;
        UnsubscribeFromLocalLobbyController();
        currentLobby = null;
        lobbyClientState.Clear();
        pendingNetworkLobbyViewData = null;
        isLobbyResyncPending = false;

        lastEntryResult =
            result ??
            LobbyEntryResult.Failed(LobbyEntryFailureType.Unknown, "The lobby could not be entered.");

        SetEntryState(LobbyEntryState.Failed);

        HandleLobbyEntryFailure(lastEntryResult);
    }

    private void HandleLobbyEntryFailure(LobbyEntryResult result)
    {
        if (result == null)
        {
            return;
        }

        LobbyEntryFailed?.Invoke(result);

        Debug.LogWarning($"[LobbyManager] Lobby entry failed. Type: {result.failureType}. Message: {result.failureMessage}");

        ReturnToMainScene();
    }

    #endregion

    #region State

    private void SetEntryState(LobbyEntryState newState)
    {
        if (entryState == newState)
        {
            return;
        }

        entryState = newState;

        LobbyEntryStateChanged?.Invoke(entryState);
    }

    #endregion
}
