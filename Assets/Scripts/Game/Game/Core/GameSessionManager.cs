using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class GameSessionManager : MonoBehaviour, ISceneReadyCheck, ISaveManager
{
    public static GameSessionManager instance;

    private const float ServiceReadyTimeoutSeconds = 15f;

    private string pendingGameId = string.Empty;
    private string pendingLobbyId = string.Empty;
    private SessionRuntimeType runtimeType = SessionRuntimeType.Local;
    private GameSessionEntryState entryState = GameSessionEntryState.Idle;
    private GameSessionData currentGameSession;
    private GameSessionResult lastEntryResult;
    private bool isEnteringGame;
    private bool isLeavingGame;
    private bool isReportingGameSceneReady;
    private bool isStartingRiskSubmitAfterLoading;
    private bool isGameSessionSyncPending;
    private bool isGameSimulationCreationPending;
    private float nextGameSessionSyncTime;
    private int entryAttemptVersion;
    private Coroutine sceneEventSubscriptionRoutine;
    private GameSceneManager gameSceneManager;
    private bool isSubscribedToGameSceneManager;
    private bool isSceneReadyCheckRegistered;
    private SoloGameSaveData savedSoloGameData = new SoloGameSaveData();
    private SoloGameCheckpointData latestSoloCheckpoint;
    private string pendingSoloReplayGameId = string.Empty;
    private string handledCompletedGameId = string.Empty;
    private bool isDeferredNetworkCleanupPending;
    private float nextDeferredNetworkCleanupTime;
    private bool lastConnectionRestoreFoundEndedGame;

    public string CurrentGameId => currentGameSession?.gameId ?? string.Empty;
    public string CurrentLobbyId => currentGameSession?.lobbyId ?? pendingLobbyId;
    public SessionRuntimeType RuntimeType => runtimeType;
    public GameSessionEntryState EntryState => entryState;
    public GameSessionData CurrentGameSession => currentGameSession;
    public GameSessionResult LastEntryResult => lastEntryResult;
    public bool LastConnectionRestoreFoundEndedGame => lastConnectionRestoreFoundEndedGame;
    public bool IsEnteringGame => isEnteringGame;
    public bool IsLeavingGame => isLeavingGame;
    public bool IsGameSimulationCreationPending => isGameSimulationCreationPending;
    public bool HasEnteredGame => entryState == GameSessionEntryState.Completed && currentGameSession != null;
    public bool HasSavedSoloGameForCurrentUser =>
        savedSoloGameData?.IsValidFor(UserManager.instance?.UserId) == true;
    public string SavedSoloGameDisplayTitle =>
        HasSavedSoloGameForCurrentUser
            ? savedSoloGameData.displayTitle ?? string.Empty
            : string.Empty;

    string ISceneReadyCheck.ReadyName => "Game Session Manager";
    bool ISceneReadyCheck.IsReady =>
        GameSceneManager.instance == null ||
        GameSceneManager.instance.CurrentSceneType != GameSceneType.Game ||
        HasEnteredGame;

    public event Action<GameSessionEntryState> GameEntryStateChanged;
    public event Action<GameSessionResult> GameCreationCompleted;
    public event Action<GameSessionResult> GameCreationFailed;
    public event Action<GameSessionResult> GameEntryCompleted;
    public event Action<GameSessionResult> GameEntryFailed;
    public event Action<GameSessionData> GameSessionUpdated;
    public event Action<GamePlayerMarkedCellChangedData> GamePlayerMarkedCellChanged;
    public event Action<GameBingoCheckResolvedData> BingoCheckResolved;
    public event Action<string> GameDeleted;

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
        LocalGameSessionManager.LocalGameSessionUpdated -= OnLocalGameSessionUpdated;
        LocalGameSessionManager.LocalGameSessionUpdated += OnLocalGameSessionUpdated;
        LocalGameSessionManager.LocalGamePlayerMarkedCellChanged -= OnLocalGamePlayerMarkedCellChanged;
        LocalGameSessionManager.LocalGamePlayerMarkedCellChanged += OnLocalGamePlayerMarkedCellChanged;
        LocalGameSessionManager.LocalBingoCheckResolved -= OnLocalBingoCheckResolved;
        LocalGameSessionManager.LocalBingoCheckResolved += OnLocalBingoCheckResolved;
        NetworkGameSessionConnection.LocalGameCreationResultReceived -= ReceiveGameCreationResult;
        NetworkGameSessionConnection.LocalGameCreationResultReceived += ReceiveGameCreationResult;
        NetworkGameSessionConnection.LocalGameSessionUpdatedReceived -= OnNetworkGameSessionUpdated;
        NetworkGameSessionConnection.LocalGameSessionUpdatedReceived += OnNetworkGameSessionUpdated;
        NetworkGameSessionConnection.LocalGamePlayStateChangedReceived -= OnNetworkGamePlayStateChanged;
        NetworkGameSessionConnection.LocalGamePlayStateChangedReceived += OnNetworkGamePlayStateChanged;
        NetworkGameSessionConnection.LocalGamePlayerStateChangedReceived -= OnNetworkGamePlayerStateChanged;
        NetworkGameSessionConnection.LocalGamePlayerStateChangedReceived += OnNetworkGamePlayerStateChanged;
        NetworkGameSessionConnection.LocalGamePlayerMarkedCellChangedReceived -= OnNetworkGamePlayerMarkedCellChanged;
        NetworkGameSessionConnection.LocalGamePlayerMarkedCellChangedReceived += OnNetworkGamePlayerMarkedCellChanged;
        NetworkGameSessionConnection.LocalBingoCheckResolvedReceived -= OnNetworkBingoCheckResolved;
        NetworkGameSessionConnection.LocalBingoCheckResolvedReceived += OnNetworkBingoCheckResolved;
        NetworkGameSessionConnection.LocalGamePlayerLeftReceived -= OnNetworkGamePlayerLeft;
        NetworkGameSessionConnection.LocalGamePlayerLeftReceived += OnNetworkGamePlayerLeft;
        NetworkGameSessionConnection.LocalGameDeletedReceived -= OnNetworkGameDeleted;
        NetworkGameSessionConnection.LocalGameDeletedReceived += OnNetworkGameDeleted;

        BeginSceneEventSubscription();
    }

    private void OnDisable()
    {
        LocalGameSessionManager.LocalGameSessionUpdated -= OnLocalGameSessionUpdated;
        LocalGameSessionManager.LocalGamePlayerMarkedCellChanged -= OnLocalGamePlayerMarkedCellChanged;
        LocalGameSessionManager.LocalBingoCheckResolved -= OnLocalBingoCheckResolved;
        NetworkGameSessionConnection.LocalGameCreationResultReceived -= ReceiveGameCreationResult;
        NetworkGameSessionConnection.LocalGameSessionUpdatedReceived -= OnNetworkGameSessionUpdated;
        NetworkGameSessionConnection.LocalGamePlayStateChangedReceived -= OnNetworkGamePlayStateChanged;
        NetworkGameSessionConnection.LocalGamePlayerStateChangedReceived -= OnNetworkGamePlayerStateChanged;
        NetworkGameSessionConnection.LocalGamePlayerMarkedCellChangedReceived -= OnNetworkGamePlayerMarkedCellChanged;
        NetworkGameSessionConnection.LocalBingoCheckResolvedReceived -= OnNetworkBingoCheckResolved;
        NetworkGameSessionConnection.LocalGamePlayerLeftReceived -= OnNetworkGamePlayerLeft;
        NetworkGameSessionConnection.LocalGameDeletedReceived -= OnNetworkGameDeleted;
        EndSceneEventSubscription();
    }

    private void OnDestroy()
    {
        LocalGameSessionManager.LocalGameSessionUpdated -= OnLocalGameSessionUpdated;
        LocalGameSessionManager.LocalGamePlayerMarkedCellChanged -= OnLocalGamePlayerMarkedCellChanged;
        LocalGameSessionManager.LocalBingoCheckResolved -= OnLocalBingoCheckResolved;
        NetworkGameSessionConnection.LocalGameCreationResultReceived -= ReceiveGameCreationResult;
        NetworkGameSessionConnection.LocalGameSessionUpdatedReceived -= OnNetworkGameSessionUpdated;
        NetworkGameSessionConnection.LocalGamePlayStateChangedReceived -= OnNetworkGamePlayStateChanged;
        NetworkGameSessionConnection.LocalGamePlayerStateChangedReceived -= OnNetworkGamePlayerStateChanged;
        NetworkGameSessionConnection.LocalGamePlayerMarkedCellChangedReceived -= OnNetworkGamePlayerMarkedCellChanged;
        NetworkGameSessionConnection.LocalBingoCheckResolvedReceived -= OnNetworkBingoCheckResolved;
        NetworkGameSessionConnection.LocalGamePlayerLeftReceived -= OnNetworkGamePlayerLeft;
        NetworkGameSessionConnection.LocalGameDeletedReceived -= OnNetworkGameDeleted;
        EndSceneEventSubscription();

        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        TryProcessDeferredNetworkCleanup();

        if (GameManager.instance == null ||
            !GameManager.instance.HasCompletedSessionStartupCleanup ||
            GameSceneManager.instance == null ||
            GameSceneManager.instance.CurrentSceneType != GameSceneType.Game ||
            HasEnteredGame ||
            isEnteringGame ||
            entryState == GameSessionEntryState.Failed ||
            (isGameSimulationCreationPending && string.IsNullOrWhiteSpace(pendingLobbyId)) ||
            isGameSessionSyncPending ||
            Time.realtimeSinceStartup < nextGameSessionSyncTime)
        {
            return;
        }

        string lobbyId = ResolveExpectedLobbyId();
        string gameId = !string.IsNullOrWhiteSpace(pendingGameId)
            ? pendingGameId
            : UserManager.instance?.CurrentUser?.lastGameId;

        if (string.IsNullOrWhiteSpace(gameId) && string.IsNullOrWhiteSpace(lobbyId))
        {
            return;
        }

        RequestGameSessionSync(true);
    }

    public void PrepareForGameCreation(string lobbyId, SessionRuntimeType requestedRuntimeType)
    {
        if (requestedRuntimeType == SessionRuntimeType.Network)
        {
            string currentLobbyId = LobbyManager.instance?.CurrentLobbyId;

            if (string.IsNullOrWhiteSpace(currentLobbyId) ||
                !string.Equals(currentLobbyId, lobbyId, StringComparison.Ordinal))
            {
                return;
            }
        }

        pendingLobbyId = lobbyId ?? string.Empty;
        pendingGameId = string.Empty;
        runtimeType = requestedRuntimeType;
        lastEntryResult = null;
        isEnteringGame = false;
        isLeavingGame = false;
        isReportingGameSceneReady = false;
        isGameSessionSyncPending = false;
        handledCompletedGameId = string.Empty;
        SetEntryState(GameSessionEntryState.WaitingForService);
    }

    public void ReceiveGameCreationResult(GameSessionResult result)
    {
        if (!IsExpectedGameCreationResult(result))
        {
            return;
        }

        lastEntryResult = result;

        if (result == null || !result.success)
        {
            GameSessionResult failureResult = result ?? GameSessionResult.Failed(
                GameSessionOperationType.Create,
                GameSessionFailureType.Unknown,
                "The Game session manager did not return a creation result.",
                lobbyId: pendingLobbyId);

            pendingGameId = string.Empty;
            pendingLobbyId = string.Empty;
            isGameSimulationCreationPending = false;
            SetEntryState(GameSessionEntryState.Idle);
            GameCreationFailed?.Invoke(failureResult);
            return;
        }

        if (!TryApplySuccessfulResult(result, out GameSessionResult playerFailure))
        {
            CompleteGameEntryFailure(playerFailure);
            return;
        }

        GameCreationCompleted?.Invoke(result);
        GameEntryCompleted?.Invoke(result);
    }

    public bool PrepareLastGameRejoin(string gameId)
    {
        if (isEnteringGame || string.IsNullOrWhiteSpace(gameId))
        {
            return false;
        }

        pendingGameId = gameId.Trim();
        pendingLobbyId = string.Empty;
        runtimeType = ResolveRuntimeType(pendingGameId);
        currentGameSession = null;
        lastEntryResult = null;
        isLeavingGame = false;
        isReportingGameSceneReady = false;
        isGameSessionSyncPending = false;
        isGameSimulationCreationPending = false;
        SetEntryState(GameSessionEntryState.WaitingForService);
        return true;
    }

    public async void BeginPendingGameEntry()
    {
        if (isEnteringGame || string.IsNullOrWhiteSpace(pendingGameId))
        {
            return;
        }

        isEnteringGame = true;
        int currentAttemptVersion = ++entryAttemptVersion;

        UserData userData = UserManager.instance?.CurrentUser;

        if (userData == null || !userData.HasUser)
        {
            CompleteGameEntryFailure(GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.PlayerNotFound,
                "The current player could not be resolved.",
                pendingGameId));
            return;
        }

        IGameSessionService service = await WaitForGameServiceAsync(runtimeType);

        if (currentAttemptVersion != entryAttemptVersion)
        {
            return;
        }

        if (service == null)
        {
            CompleteGameEntryFailure(GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.ServiceUnavailable,
                "The required Game session service was not ready.",
                pendingGameId));
            return;
        }

        SetEntryState(GameSessionEntryState.Joining);
        GameSessionResult result;

        try
        {
            result = await service.RejoinGameAsync(pendingGameId, userData);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            result = GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.Unknown,
                "An unexpected error occurred while rejoining the Game.",
                pendingGameId);
        }

        if (currentAttemptVersion != entryAttemptVersion)
        {
            return;
        }

        lastEntryResult = result;

        if (result == null || !result.success)
        {
            CompleteGameEntryFailure(result ?? GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.Unknown,
                "The Game session manager did not return a rejoin result.",
                pendingGameId));
            return;
        }

        if (!TryApplySuccessfulResult(result, out GameSessionResult playerFailure))
        {
            CompleteGameEntryFailure(playerFailure);
            return;
        }

        GameEntryCompleted?.Invoke(result);
    }

    public void CancelPendingGameEntry()
    {
        entryAttemptVersion++;
        isEnteringGame = false;
        pendingGameId = string.Empty;
        pendingLobbyId = string.Empty;

        if (currentGameSession == null)
        {
            SetEntryState(GameSessionEntryState.Idle);
        }
    }

    public void SetGameSimulationCreationPending(bool isPending)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        isGameSimulationCreationPending = isPending;
#endif
    }

    public bool PrepareForGameSimulationEntry(string lobbyId)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (string.IsNullOrWhiteSpace(lobbyId))
        {
            return false;
        }

        PrepareForGameCreation(lobbyId, SessionRuntimeType.Network);

        if (!string.Equals(pendingLobbyId, lobbyId, StringComparison.Ordinal))
        {
            return false;
        }

        isGameSimulationCreationPending = true;
        nextGameSessionSyncTime = 0f;
        return true;
#else
        return false;
#endif
    }

    public void ClearCurrentGame(bool clearLastGameId)
    {
        SessionPauseManager.SetGameplayPaused(false);
        lastConnectionRestoreFoundEndedGame = false;
        entryAttemptVersion++;
        isEnteringGame = false;
        pendingGameId = string.Empty;
        pendingLobbyId = string.Empty;
        currentGameSession = null;
        lastEntryResult = null;
        isLeavingGame = false;
        isReportingGameSceneReady = false;
        isGameSessionSyncPending = false;
        isGameSimulationCreationPending = false;
        SetEntryState(GameSessionEntryState.Idle);

        if (clearLastGameId)
        {
            UserManager.instance?.ClearLastGameId();
        }

        GameSessionUpdated?.Invoke(null);
    }

    public void LoadData(GameData data)
    {
        savedSoloGameData = data?.soloGameSaveData != null
            ? new SoloGameSaveData(data.soloGameSaveData)
            : new SoloGameSaveData();
        latestSoloCheckpoint = null;
        pendingSoloReplayGameId = string.Empty;
    }

    public void SaveData(ref GameData data)
    {
        data ??= new GameData();
        data.soloGameSaveData = new SoloGameSaveData(savedSoloGameData);
    }

    public bool IsReady()
    {
        return true;
    }

    public void CaptureSoloCheckpoint(GameSessionData gameSessionData)
    {
        UserData currentUser = UserManager.instance?.CurrentUser;

        if (gameSessionData == null ||
            gameSessionData.playMode != MainMenuPlayMode.Solo ||
            gameSessionData.runtimeType != SessionRuntimeType.Local ||
            gameSessionData.gameState != GameSessionState.InProgress ||
            currentUser == null ||
            !currentUser.HasUser ||
            gameSessionData.GetPlayer(currentUser.userId) == null)
        {
            return;
        }

        latestSoloCheckpoint = new SoloGameCheckpointData(gameSessionData);
    }

    public bool IsCurrentSoloCheckActive()
    {
        if (currentGameSession?.playMode != MainMenuPlayMode.Solo)
        {
            return false;
        }

        string userId = UserManager.instance?.UserId;
        GamePlayerData playerData = currentGameSession.GetPlayer(userId);

        return playerData?.gameStatus == GamePlayerStatus.Checking ||
               currentGameSession.gamePlayController?.HasPendingCheckAnimation(userId) == true;
    }

    public async void SaveAndLeaveCurrentSoloGame()
    {
        if (isLeavingGame ||
            currentGameSession?.playMode != MainMenuPlayMode.Solo ||
            latestSoloCheckpoint == null ||
            IsCurrentSoloCheckActive())
        {
            return;
        }

        UserData currentUser = UserManager.instance?.CurrentUser;

        if (currentUser == null ||
            !currentUser.HasUser ||
            !latestSoloCheckpoint.IsValidFor(currentUser.userId))
        {
            return;
        }

        isLeavingGame = true;
        savedSoloGameData = new SoloGameSaveData
        {
            hasSavedGame = true,
            ownerUserId = currentUser.userId,
            displayTitle = BuildGameDisplayTitle(
                MainMenuPlayMode.Solo,
                latestSoloCheckpoint.gameSessionData.gameModeType),
            checkpoint = new SoloGameCheckpointData(latestSoloCheckpoint)
        };

        SaveManager.instance?.SaveGame();
        LocalGameSessionManager.instance?.SuspendSoloGame(currentGameSession.gameId);

        if (LobbyManager.instance?.HasEnteredLobby == true)
        {
            await LobbyManager.instance.LeaveCurrentLobbyAsync(false);
        }

        ClearCurrentGame(false);
        GameSceneManager.instance?.LoadMainScene();
    }

    public void LeaveCurrentSoloWithoutSaving()
    {
        if (currentGameSession?.playMode != MainMenuPlayMode.Solo)
        {
            return;
        }

        ClearSavedSoloGame(true);
        LeaveCurrentGame();
    }

    public async void LeaveCurrentSoloDuringCheck()
    {
        if (isLeavingGame || currentGameSession?.playMode != MainMenuPlayMode.Solo)
        {
            return;
        }

        isLeavingGame = true;
        UserData currentUser = UserManager.instance?.CurrentUser;
        GameSessionResult result =
            LocalGameSessionManager.instance?.FinalizeSoloCheckAndRemoveGame(
                currentGameSession.gameId,
                currentUser);
        GameScoreAuthority.PersistFinalScoreResult(result?.finalScoreResult);
        ClearSavedSoloGame(true);

        if (LobbyManager.instance?.HasEnteredLobby == true)
        {
            await LobbyManager.instance.LeaveCurrentLobbyAsync(false);
        }

        ClearCurrentGame(false);
        GameSceneManager.instance?.LoadMainScene();
    }

    public bool PrepareSavedSoloRejoin()
    {
        UserData currentUser = UserManager.instance?.CurrentUser;

        if (currentUser == null ||
            !currentUser.HasUser ||
            !savedSoloGameData.IsValidFor(currentUser.userId) ||
            LocalGameSessionManager.instance == null ||
            !LocalGameSessionManager.instance.RestoreSavedSoloGame(
                savedSoloGameData.checkpoint))
        {
            return false;
        }

        string gameId = savedSoloGameData.checkpoint.gameSessionData.gameId;
        latestSoloCheckpoint = new SoloGameCheckpointData(
            savedSoloGameData.checkpoint);
        pendingSoloReplayGameId = savedSoloGameData.checkpoint.replayCurrentBall
            ? gameId
            : string.Empty;
        return PrepareLastGameRejoin(gameId);
    }

    public async Task DeclineSavedSoloGameAsync()
    {
        UserData currentUser = UserManager.instance?.CurrentUser;

        if (currentUser != null &&
            currentUser.HasUser &&
            savedSoloGameData.IsValidFor(currentUser.userId) &&
            LocalGameSessionManager.instance != null &&
            LocalGameSessionManager.instance.RestoreSavedSoloGame(
                savedSoloGameData.checkpoint))
        {
            GameSessionResult result = await LocalGameSessionManager.instance.LeaveGameAsync(
                savedSoloGameData.checkpoint.gameSessionData.gameId,
                currentUser);
            GameScoreAuthority.PersistFinalScoreResult(result?.finalScoreResult);
        }

        ClearSavedSoloGame(true);
    }

    public bool ConsumeSavedSoloBallReplay(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId) ||
            !string.Equals(pendingSoloReplayGameId, gameId, StringComparison.Ordinal))
        {
            return false;
        }

        pendingSoloReplayGameId = string.Empty;
        return true;
    }

    public string GetNetworkRejoinDisplayTitle()
    {
        GameSessionData sessionData = currentGameSession;

        if (sessionData != null && sessionData.playMode != MainMenuPlayMode.Solo)
        {
            return BuildGameDisplayTitle(sessionData.playMode, sessionData.gameModeType);
        }

        LobbyViewData lobbyViewData = LobbyManager.instance?.CurrentLobbyViewData;

        if (lobbyViewData != null && lobbyViewData.playMode != MainMenuPlayMode.Solo)
        {
            return BuildGameDisplayTitle(
                lobbyViewData.playMode,
                lobbyViewData.gameModeType);
        }

        UserData currentUser = UserManager.instance?.CurrentUser;

        if (currentUser?.hasLastGameDisplayData == true &&
            currentUser.lastGamePlayMode != MainMenuPlayMode.Solo)
        {
            return BuildGameDisplayTitle(
                currentUser.lastGamePlayMode,
                currentUser.lastGameModeType);
        }

        return "Online - Game";
    }

    public void ReturnToLobbyAfterCompletedGame()
    {
        if (currentGameSession?.gameState != GameSessionState.Completed)
        {
            return;
        }

        if (currentGameSession.IsCustomHostGone())
        {
            ReturnToMainMenuAfterCompletedGame();
            return;
        }

        GameSessionData completedSession = new GameSessionData(currentGameSession);
        LobbyManager lobbyManager = LobbyManager.instance;
        bool hasExistingLobby = lobbyManager?.HasEnteredLobby == true;

        PrepareLobbyAfterCompletedGame(completedSession);
        ClearCurrentGame(true);

        if (!hasExistingLobby &&
            completedSession.playMode == MainMenuPlayMode.Solo &&
            lobbyManager != null &&
            UserManager.instance?.CurrentUser != null)
        {
            LobbySetupData lobbySetupData = BuildSoloLobbySetupData(
                completedSession,
                UserManager.instance.CurrentUser);
            lobbyManager.SetPendingLobbySetupData(lobbySetupData, false);
            GameSceneManager.instance?.LoadLobbyScene();
            lobbyManager.BeginPendingLobbyEntry();
            return;
        }

        GameSceneManager.instance?.LoadLobbyScene();
    }

    public void ReturnToMainMenuAfterCompletedGame()
    {
        if (currentGameSession?.gameState != GameSessionState.Completed)
        {
            return;
        }

        if (currentGameSession.playMode == MainMenuPlayMode.Solo)
        {
            ClearSavedSoloGame(true);
        }

        LeaveCurrentGame();
    }

    public bool SetCurrentGamePaused(bool paused)
    {
        if (!HasEnteredGame || currentGameSession.playMode != MainMenuPlayMode.Solo)
        {
            return false;
        }

        SessionPauseManager.SetGameplayPaused(paused);
        return true;
    }

    public void ResetForFreshApplicationStart()
    {
        ClearCurrentGame(false);
        runtimeType = SessionRuntimeType.Local;
        nextGameSessionSyncTime = 0f;
    }

    public async Task ClearPreviousSessionForFreshLobbyEntryAsync()
    {
        if (isLeavingGame)
        {
            return;
        }

        UserData userData = UserManager.instance?.CurrentUser;

        if (userData == null || !userData.HasUser)
        {
            ClearCurrentGame(true);
            return;
        }

        entryAttemptVersion++;
        isEnteringGame = false;
        isGameSessionSyncPending = false;
        isGameSimulationCreationPending = false;
        isLeavingGame = true;

        string gameId = CurrentGameId;

        if (string.IsNullOrWhiteSpace(gameId))
        {
            gameId = userData.lastGameId;
        }

        if (!string.IsNullOrWhiteSpace(gameId))
        {
            SessionRuntimeType previousRuntimeType = ResolveRuntimeType(gameId);
            bool offlineNetworkGame = previousRuntimeType == SessionRuntimeType.Network &&
                                      NetworkBootstrap.instance?.IsConnected != true;
            IGameSessionService service = offlineNetworkGame
                ? null
                : await WaitForGameServiceAsync(previousRuntimeType);

            if (service != null)
            {
                try
                {
                    GameSessionResult result = await service.LeaveGameAsync(gameId, userData);
                    GameScoreAuthority.PersistFinalScoreResult(result?.finalScoreResult);

                    if (result == null ||
                        (!result.success && !IsAlreadyDetachedFailure(result.failureType)))
                    {
                        Debug.LogWarning($"[GameSessionManager] Previous Game cleanup could not be confirmed: {result?.failureMessage ?? "No result was returned."}");
                        if (previousRuntimeType == SessionRuntimeType.Network)
                        {
                            UserManager.instance?.DeferNetworkGameCleanup(gameId);
                        }
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[GameSessionManager] Previous Game cleanup failed: {exception.Message}");
                    if (previousRuntimeType == SessionRuntimeType.Network)
                    {
                        UserManager.instance?.DeferNetworkGameCleanup(gameId);
                    }
                }
            }
            else
            {
                if (previousRuntimeType == SessionRuntimeType.Network)
                {
                    UserManager.instance?.DeferNetworkGameCleanup(gameId);
                }
            }
        }

        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager != null)
        {
            await lobbyManager.ClearPreviousLobbyMembershipAsync(userData);
        }

        ClearCurrentGame(true);
    }

    public async Task<GameSessionResult> CheckNetworkRejoinAsync(string gameId)
    {
        UserData userData = UserManager.instance?.CurrentUser;

        if (userData == null || !userData.HasUser ||
            string.IsNullOrWhiteSpace(gameId))
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.PlayerNotFound,
                "The previous game could not be found.",
                gameId);
        }

        if (NetworkBootstrap.instance?.IsConnected != true)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.NetworkConnectionFailed,
                "A connection is required to rejoin this game.",
                gameId);
        }

        GameSessionResult result;

        try
        {
            IGameSessionService service = await WaitForGameServiceAsync(SessionRuntimeType.Network);
            result = service != null
                ? await service.SyncGameSessionAsync(gameId, string.Empty, userData)
                : GameSessionResult.Failed(
                    GameSessionOperationType.Rejoin,
                    GameSessionFailureType.ServiceUnavailable,
                    "The game service is not available.",
                    gameId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[GameSessionManager] Rejoin check failed: {exception.Message}");
            result = GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.NetworkConnectionFailed,
                "The previous game could not be checked right now.",
                gameId);
        }

        GameScoreAuthority.PersistFinalScoreResult(result?.finalScoreResult);

        if (result == null || !result.success)
        {
            return result;
        }

        GamePlayerData playerData = result.gameSessionData?.GetPlayer(userData.userId);

        GameScoreAuthority.PersistFinalScoreResult(
            GameScoreAuthority.CreateFinalScoreResult(result.gameSessionData, playerData));

        if (result.gameSessionData == null ||
            result.gameSessionData.gameState == GameSessionState.Completed ||
            playerData == null || !playerData.canRejoin)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.PlayerNotEligible,
                "This game has ended and can no longer be rejoined.",
                gameId);
        }

        return result;
    }

    public async Task<bool> RestoreCurrentNetworkGameAsync()
    {
        lastConnectionRestoreFoundEndedGame = false;

        if (runtimeType != SessionRuntimeType.Network ||
            NetworkBootstrap.instance?.IsConnected != true)
        {
            return false;
        }

        string gameId = CurrentGameId;

        if (string.IsNullOrWhiteSpace(gameId))
        {
            gameId = UserManager.instance?.CurrentUser?.lastGameId;
        }

        UserData userData = UserManager.instance?.CurrentUser;
        NetworkGameSessionService service = NetworkGameSessionService.instance;

        if (string.IsNullOrWhiteSpace(gameId) || userData == null ||
            service == null || !service.IsReady)
        {
            return false;
        }

        GameSessionResult result = await service.RejoinGameAsync(gameId, userData);

        if (result?.success != true || result.gameSessionData == null)
        {
            GameScoreAuthority.PersistFinalScoreResult(result?.finalScoreResult);
            lastConnectionRestoreFoundEndedGame =
                result?.failureType == GameSessionFailureType.GameNotFound ||
                result?.failureType == GameSessionFailureType.PlayerNotFound ||
                result?.failureType == GameSessionFailureType.PlayerNotEligible;
            return false;
        }

        ApplyAuthoritativeGameSessionSnapshot(result.gameSessionData);
        GameSessionResult sceneReadyResult =
            await service.SetGameSceneReadyAsync(gameId, userData);

        if (sceneReadyResult?.success != true)
        {
            GameScoreAuthority.PersistFinalScoreResult(sceneReadyResult?.finalScoreResult);
            lastConnectionRestoreFoundEndedGame =
                sceneReadyResult?.failureType == GameSessionFailureType.GameNotFound ||
                sceneReadyResult?.failureType == GameSessionFailureType.PlayerNotFound ||
                sceneReadyResult?.failureType == GameSessionFailureType.PlayerNotEligible;
            return false;
        }

        if (sceneReadyResult.gameSessionData != null)
        {
            ApplyAuthoritativeGameSessionSnapshot(sceneReadyResult.gameSessionData);
        }

        return true;
    }

    public async Task<bool> StartPendingRiskSubmitAfterLoadingAsync()
    {
        GameSceneManager sceneManager = GameSceneManager.instance;

        if (isStartingRiskSubmitAfterLoading || runtimeType != SessionRuntimeType.Network ||
            !HasEnteredGame || sceneManager?.CurrentSceneType != GameSceneType.Game ||
            sceneManager.IsLoadingScene || NetworkBootstrap.instance?.IsConnected != true)
        {
            return false;
        }

        GamePlayerData playerData = GetCurrentPlayer();

        if (!RiskGameplayAuthority.IsRiskGame(currentGameSession) ||
            playerData?.controlType != GamePlayerControlType.Human ||
            !playerData.isRiskSubmitReconnectGrace || playerData.isSubmitTimerActive)
        {
            return false;
        }

        isStartingRiskSubmitAfterLoading = true;
        string gameId = CurrentGameId;

        try
        {
            // The first scene-ready request can still be finishing after the fade.
            while (isReportingGameSceneReady && string.Equals(gameId, CurrentGameId, StringComparison.Ordinal))
            {
                await Task.Yield();
            }

            if (!string.Equals(gameId, CurrentGameId, StringComparison.Ordinal) ||
                GameSceneManager.instance?.CurrentSceneType != GameSceneType.Game ||
                GameSceneManager.instance.IsLoadingScene ||
                NetworkBootstrap.instance?.IsConnected != true ||
                GetCurrentPlayer()?.isRiskSubmitReconnectGrace != true)
            {
                return false;
            }

            NetworkGameSessionService service = NetworkGameSessionService.instance;

            if (service?.IsReady != true)
            {
                return false;
            }

            GameSessionResult result = await service.SetGameSceneReadyAsync(
                gameId, UserManager.instance?.CurrentUser);

            if (!string.Equals(gameId, CurrentGameId, StringComparison.Ordinal) ||
                result?.success != true)
            {
                return false;
            }

            if (result.gameSessionData != null)
            {
                ApplyAuthoritativeGameSessionSnapshot(result.gameSessionData);
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[GameSessionManager] Could not start the Risk submit window after loading: {exception.Message}");
            return false;
        }
        finally
        {
            isStartingRiskSubmitAfterLoading = false;
        }
    }

    private void TryProcessDeferredNetworkCleanup()
    {
        string pendingGameId = UserManager.instance?.CurrentUser?.pendingNetworkGameCleanupId;

        if (isDeferredNetworkCleanupPending ||
            string.IsNullOrWhiteSpace(pendingGameId) ||
            Time.realtimeSinceStartup < nextDeferredNetworkCleanupTime ||
            NetworkBootstrap.instance?.IsConnected != true ||
            NetworkGameSessionService.instance?.IsReady != true)
        {
            return;
        }

        _ = ProcessDeferredNetworkCleanupAsync(pendingGameId);
    }

    private async Task ProcessDeferredNetworkCleanupAsync(string gameId)
    {
        isDeferredNetworkCleanupPending = true;

        try
        {
            GameSessionResult result = await NetworkGameSessionService.instance.LeaveGameAsync(
                gameId,
                UserManager.instance?.CurrentUser);

            GameScoreAuthority.PersistFinalScoreResult(result?.finalScoreResult);

            if (result != null && (result.success || IsAlreadyDetachedFailure(result.failureType)))
            {
                if (string.Equals(
                        UserManager.instance?.CurrentUser?.pendingNetworkGameCleanupId,
                        gameId,
                        StringComparison.Ordinal))
                {
                    UserManager.instance.ClearDeferredNetworkGameCleanup();
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[GameSessionManager] Deferred game cleanup failed: {exception.Message}");
        }
        finally
        {
            isDeferredNetworkCleanupPending = false;
            nextDeferredNetworkCleanupTime = Time.realtimeSinceStartup + 10f;
        }
    }

    public async void LeaveCurrentGame()
    {
        if (isLeavingGame)
        {
            return;
        }

        string gameId = CurrentGameId;
        UserData userData = UserManager.instance?.CurrentUser;

        isLeavingGame = true;

        if (!string.IsNullOrWhiteSpace(gameId) && userData != null && userData.HasUser)
        {
            IGameSessionService service = await WaitForGameServiceAsync(runtimeType);

            if (service != null)
            {
                try
                {
                    GameSessionResult result = await service.LeaveGameAsync(gameId, userData);
                    GameScoreAuthority.PersistFinalScoreResult(result?.finalScoreResult);

                    if (result == null || !result.success)
                    {
                        Debug.LogWarning($"[GameSessionManager] Leave failed: {result?.failureMessage ?? "No leave result was returned."}");
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
            else
            {
                Debug.LogWarning("[GameSessionManager] The Game session service was not available while leaving.");
            }
        }

        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager != null && lobbyManager.HasEnteredLobby)
        {
            try
            {
                LobbyExitResult lobbyExitResult = await lobbyManager.LeaveCurrentLobbyAsync(false);

                if (lobbyExitResult == null || !lobbyExitResult.success)
                {
                    Debug.LogWarning($"[GameSessionManager] Linked Lobby leave failed: {lobbyExitResult?.failureMessage ?? "No lobby leave result was returned."}");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        ClearCurrentGame(true);
        GameSceneManager.instance?.LoadMainScene();
    }

    public GamePlayerData GetCurrentPlayer()
    {
        return currentGameSession?.GetPlayer(UserManager.instance?.UserId);
    }

    public bool SetCurrentPlayerMarkedCell(int cellIndex, bool isMarked)
    {
        if (!HasEnteredGame ||
            SessionPauseManager.IsPaused ||
            ConnectionRecoveryManager.instance?.IsRecoveringInScene == true ||
            GetCurrentPlayer()?.isAutomaticBoardEnabled == true ||
            currentGameSession.gamePlayController?.IsPlayerInputClosed == true)
        {
            return false;
        }

        UserData userData = UserManager.instance?.CurrentUser;
        IGameSessionService service = GetGameService(runtimeType);

        if (userData == null ||
            !userData.HasUser ||
            service == null ||
            !service.IsReady ||
            !service.TrySetPlayerMarkedCell(
                currentGameSession.gameId,
                userData,
                cellIndex,
                isMarked,
                out GamePlayerMarkedCellChangedData updateData))
        {
            return false;
        }

        if (updateData != null)
        {
            ApplyGamePlayerMarkedCellChanged(updateData);
        }

        return true;
    }

    public bool SubmitCurrentPlayerBingoCheck(
        LobbyBoardData boardData,
        IReadOnlyList<int> markedCellIndices)
    {
        if (!HasEnteredGame || boardData == null || SessionPauseManager.IsPaused ||
            ConnectionRecoveryManager.instance?.IsRecoveringInScene == true)
        {
            return false;
        }

        UserData userData = UserManager.instance?.CurrentUser;
        GamePlayerData playerData = GetCurrentPlayer();
        IGameSessionService service = GetGameService(runtimeType);

        if (userData == null ||
            !userData.HasUser ||
            playerData == null ||
            playerData.controlType != GamePlayerControlType.Human ||
            playerData.returnState != GamePlayerReturnState.Active ||
            playerData.gameStatus != GamePlayerStatus.Eligible ||
            service == null ||
            !service.IsReady ||
            !service.TrySubmitBingoCheck(
                currentGameSession.gameId,
                userData,
                boardData,
                markedCellIndices,
                out GameBingoCheckResolvedData resolvedData))
        {
            return false;
        }

        if (resolvedData != null)
        {
            BingoCheckResolved?.Invoke(resolvedData);
        }

        return true;
    }

    public bool CompleteCurrentPlayerBingoCheckAnimation()
    {
        if (!HasEnteredGame || SessionPauseManager.IsPaused ||
            ConnectionRecoveryManager.instance?.IsRecoveringInScene == true)
        {
            return false;
        }

        UserData userData = UserManager.instance?.CurrentUser;
        IGameSessionService service = GetGameService(runtimeType);

        return userData != null &&
               userData.HasUser &&
               service != null &&
               service.IsReady &&
               service.TryCompleteBingoCheckAnimation(
                   currentGameSession.gameId,
                   userData);
    }

    public bool ResolveCurrentPlayerRiskDecision(bool endPlayerGame)
    {
        if (!HasEnteredGame || SessionPauseManager.IsPaused ||
            ConnectionRecoveryManager.instance?.IsRecoveringInScene == true)
        {
            return false;
        }

        UserData userData = UserManager.instance?.CurrentUser;
        GamePlayerData playerData = GetCurrentPlayer();
        IGameSessionService service = GetGameService(runtimeType);

        return userData != null &&
               userData.HasUser &&
               playerData != null &&
               playerData.gameStatus == GamePlayerStatus.Eligible &&
               !playerData.hasRiskCashedOut &&
               playerData.isRiskDecisionPending &&
               service != null &&
               service.IsReady &&
               service.TryResolveRiskDecision(
                   currentGameSession.gameId,
                   userData,
                   endPlayerGame);
    }

    private bool TryApplySuccessfulResult(GameSessionResult result, out GameSessionResult failureResult)
    {
        failureResult = null;

        if (result?.gameSessionData == null || string.IsNullOrWhiteSpace(result.gameSessionData.gameId))
        {
            failureResult = GameSessionResult.Failed(
                result?.operationType ?? GameSessionOperationType.None,
                GameSessionFailureType.Unknown,
                "The Game session data was not returned.",
                result?.gameId,
                result?.lobbyId);
            return false;
        }

        string userId = UserManager.instance?.UserId;
        GamePlayerData playerData = result.gameSessionData.GetPlayer(userId);

        if (playerData == null || playerData.userTag == UserTag.Bot)
        {
            failureResult = GameSessionResult.Failed(
                result.operationType,
                GameSessionFailureType.PlayerNotFound,
                "The current player was not found in the locked Game player list.",
                result.gameId,
                result.lobbyId);
            return false;
        }

        if (!playerData.canRejoin)
        {
            failureResult = GameSessionResult.Failed(
                result.operationType,
                GameSessionFailureType.PlayerNotEligible,
                "The current player is no longer eligible to enter this Game.",
                result.gameId,
                result.lobbyId);
            return false;
        }

        currentGameSession = new GameSessionData(result.gameSessionData);
        if (result.operationType == GameSessionOperationType.Create &&
            currentGameSession.playMode == MainMenuPlayMode.Solo)
        {
            latestSoloCheckpoint = null;
            pendingSoloReplayGameId = string.Empty;
        }

        if (currentGameSession.gameState != GameSessionState.Completed)
        {
            handledCompletedGameId = string.Empty;
        }
        runtimeType = currentGameSession.runtimeType;
        pendingGameId = string.Empty;
        pendingLobbyId = currentGameSession.lobbyId;
        isEnteringGame = false;
        isGameSessionSyncPending = false;
        isGameSimulationCreationPending = false;
        SetEntryState(GameSessionEntryState.Completed);
        if (currentGameSession.playMode != MainMenuPlayMode.Solo)
        {
            UserManager.instance?.SetLastGameInfo(
                currentGameSession.gameId,
                currentGameSession.playMode,
                currentGameSession.gameModeType);
        }
        ApplyFinalizedScoreForCurrentNetworkUser();
        GameSessionUpdated?.Invoke(new GameSessionData(currentGameSession));
        HandleCurrentGameCompletion();
        return true;
    }

    private void ApplyGameSessionUpdate(GameSessionData gameSessionData)
    {
        if (gameSessionData == null || currentGameSession == null ||
            !string.Equals(gameSessionData.gameId, currentGameSession.gameId, StringComparison.Ordinal))
        {
            return;
        }

        if (gameSessionData.revision <= currentGameSession.revision)
        {
            return;
        }

        ApplyAuthoritativeGameSessionSnapshot(gameSessionData);
    }

    private void ApplyGamePlayStateChanged(GamePlayStateChangedData updateData)
    {
        if (updateData == null ||
            currentGameSession == null ||
            string.IsNullOrWhiteSpace(updateData.gameId) ||
            !string.Equals(updateData.gameId, currentGameSession.gameId, StringComparison.Ordinal) ||
            updateData.revision <= currentGameSession.revision)
        {
            return;
        }

        currentGameSession.gameState = updateData.gameState;
        currentGameSession.gamePlayController ??= new GamePlayController();
        currentGameSession.gamePlayController.ApplyNetworkState(updateData);

        if (updateData.playerStates != null)
        {
            for (int i = 0; i < updateData.playerStates.Count; i++)
            {
                GamePlayerMatchStateData playerState = updateData.playerStates[i];
                GamePlayerData playerData = currentGameSession.GetPlayer(playerState?.userId);

                if (playerState == null || playerData == null)
                {
                    continue;
                }

                ApplyPlayerMatchState(playerData, playerState);
            }
        }

        GamePlayerData localPlayer = currentGameSession.GetPlayer(
            UserManager.instance?.UserId);

        if (localPlayer?.isAutomaticBoardEnabled == true &&
            updateData.localAutomaticBoardMarks != null)
        {
            for (int i = 0; i < updateData.localAutomaticBoardMarks.Count; i++)
            {
                localPlayer.TrySetMarkedCell(updateData.localAutomaticBoardMarks[i], true);
            }
        }

        currentGameSession.revision = updateData.revision;
        ApplyFinalizedScoreForCurrentNetworkUser();
        GameSessionUpdated?.Invoke(new GameSessionData(currentGameSession));
        HandleCurrentGameCompletion();
    }

    private void ApplyGamePlayerStateChanged(GamePlayerStateChangedData updateData)
    {
        if (updateData == null || !CanApplyGameSessionDelta(updateData.gameId, updateData.revision))
        {
            return;
        }

        GamePlayerData playerData = currentGameSession.GetPlayer(updateData.userId);

        if (playerData == null)
        {
            RequestGameSessionSync(false);
            return;
        }

        ApplyPlayerState(playerData, updateData);
        currentGameSession.revision = updateData.revision;
        ApplyFinalizedScoreForCurrentNetworkUser();
        GameSessionUpdated?.Invoke(new GameSessionData(currentGameSession));
    }

    private void ApplyGamePlayerMarkedCellChanged(
        GamePlayerMarkedCellChangedData updateData)
    {
        if (updateData == null ||
            currentGameSession == null ||
            !string.Equals(updateData.gameId, currentGameSession.gameId, StringComparison.Ordinal))
        {
            return;
        }

        GamePlayerData playerData = currentGameSession.GetPlayer(updateData.userId);
        LobbyBoardData boardData = playerData?.boardData;

        if (boardData?.cellNumbers == null ||
            updateData.cellIndex < 0 ||
            updateData.cellIndex >= boardData.cellNumbers.Count)
        {
            return;
        }

        if (boardData.usesFreeCell && updateData.cellIndex == 12)
        {
            updateData.isMarked = true;
        }

        playerData.TrySetMarkedCell(updateData.cellIndex, updateData.isMarked);

        GamePlayerMarkedCellChanged?.Invoke(updateData);
    }

    private void ApplyGamePlayerLeft(GamePlayerLeftData updateData)
    {
        if (updateData == null || !CanApplyGameSessionDelta(updateData.gameId, updateData.revision))
        {
            return;
        }

        if (!currentGameSession.RemovePlayer(updateData.userId))
        {
            RequestGameSessionSync(false);
            return;
        }

        currentGameSession.revision = updateData.revision;
        GameSessionUpdated?.Invoke(new GameSessionData(currentGameSession));
    }

    private bool CanApplyGameSessionDelta(string gameId, long revision)
    {
        if (currentGameSession == null || string.IsNullOrWhiteSpace(gameId) ||
            !string.Equals(gameId, currentGameSession.gameId, StringComparison.Ordinal) || revision < 1)
        {
            return false;
        }

        if (revision <= currentGameSession.revision)
        {
            return false;
        }

        if (revision != currentGameSession.revision + 1)
        {
            RequestGameSessionSync(false);
            return false;
        }

        return true;
    }

    private void ApplyAuthoritativeGameSessionSnapshot(GameSessionData gameSessionData)
    {
        if (gameSessionData == null || currentGameSession == null ||
            !string.Equals(gameSessionData.gameId, currentGameSession.gameId, StringComparison.Ordinal))
        {
            return;
        }

        currentGameSession = new GameSessionData(gameSessionData);
        pendingLobbyId = currentGameSession.lobbyId;
        ApplyFinalizedScoreForCurrentNetworkUser();
        GameSessionUpdated?.Invoke(new GameSessionData(currentGameSession));
        HandleCurrentGameCompletion();
    }

    private void HandleCurrentGameCompletion()
    {
        if (currentGameSession?.gameState != GameSessionState.Completed ||
            string.Equals(
                handledCompletedGameId,
                currentGameSession.gameId,
                StringComparison.Ordinal))
        {
            return;
        }

        handledCompletedGameId = currentGameSession.gameId ?? string.Empty;
        if (!currentGameSession.IsCustomHostGone())
        {
            PrepareLobbyAfterCompletedGame(currentGameSession);
        }

        if (currentGameSession.playMode == MainMenuPlayMode.Solo)
        {
            ClearSavedSoloGame(true);
        }
        else
        {
            UserManager.instance?.ClearLastGameId();
        }
    }

    private static void PrepareLobbyAfterCompletedGame(GameSessionData gameSessionData)
    {
        if (gameSessionData == null || string.IsNullOrWhiteSpace(gameSessionData.lobbyId))
        {
            return;
        }

        if (gameSessionData.runtimeType == SessionRuntimeType.Local)
        {
            LocalLobbyManager.instance?.PrepareLobbyAfterCompletedGame(
                gameSessionData.lobbyId);
        }
        else
        {
            NetworkLobbyManager.instance?.PrepareLobbyAfterCompletedGame(
                gameSessionData.lobbyId);
        }
    }

    private void ClearSavedSoloGame(bool queueSave)
    {
        savedSoloGameData = new SoloGameSaveData();
        latestSoloCheckpoint = null;
        pendingSoloReplayGameId = string.Empty;

        if (queueSave)
        {
            SaveManager.instance?.SaveGame();
        }
    }

    private static string BuildGameDisplayTitle(
        MainMenuPlayMode playMode,
        BingoGameModeType gameModeType)
    {
        string playModeName = playMode switch
        {
            MainMenuPlayMode.Solo => "Solo",
            MainMenuPlayMode.Online => "Online",
            MainMenuPlayMode.Custom => "Custom",
            _ => "Game"
        };
        BingoGameModeData gameModeData =
            GameModeManager.instance?.GetGameModeData(gameModeType);
        string gameName = gameModeData != null &&
                          !string.IsNullOrWhiteSpace(gameModeData.GameName)
            ? gameModeData.GameName
            : gameModeType.ToString();
        return $"{playModeName} - {gameName}";
    }

    private static LobbySetupData BuildSoloLobbySetupData(
        GameSessionData gameSessionData,
        UserData userData)
    {
        return new LobbySetupData
        {
            playMode = MainMenuPlayMode.Solo,
            startFreshEntry = true,
            userData = userData,
            soloSetupData = new SoloLobbySetupData
            {
                gameModeType = gameSessionData.gameModeType,
                ballCountType = gameSessionData.ballCountType,
                useFreeCell = gameSessionData.useFreeCell,
                usesDefaultRank = gameSessionData.usesDefaultRank,
                useRank = gameSessionData.useRank,
                hasRiskMatchDurationOverride =
                    gameSessionData.hasRiskMatchDurationOverride,
                riskMatchDurationMinutes = gameSessionData.riskMatchDurationMinutes,
                usesDefaultPatterns = gameSessionData.usesDefaultPatterns,
                patternTypes = gameSessionData.patternTypes != null
                    ? new List<BingoPatternType>(gameSessionData.patternTypes)
                    : new List<BingoPatternType>(),
                maxPlayers = true,
                maxPlayer = Math.Max(1, gameSessionData.players?.Count ?? 1)
            }
        };
    }

    private static void ApplyPlayerState(
        GamePlayerData playerData,
        GamePlayerStateChangedData updateData)
    {
        playerData.isConnected = updateData.isConnected;
        playerData.isGameSceneReady = updateData.isGameSceneReady;
        playerData.canRejoin = updateData.canRejoin;
        playerData.returnState = updateData.returnState;
        playerData.controlType = updateData.controlType;
        playerData.isAutomaticBoardEnabled = updateData.isAutomaticBoardEnabled;
        playerData.gameStatus = updateData.gameStatus;
        playerData.currentMatchScore = updateData.currentMatchScore;
        playerData.rank = updateData.rank;
        playerData.isRankFinal = updateData.isRankFinal;
        playerData.isRankWinBlocked = updateData.isRankWinBlocked;
        playerData.hasRiskCashedOut = updateData.hasRiskCashedOut;
        playerData.hasPendingRankCheck = updateData.hasPendingRankCheck;
        playerData.pendingRankCheckScore = updateData.pendingRankCheckScore;
        playerData.rankResolutionOrder = updateData.rankResolutionOrder;
        playerData.areStatisticsFinalized = updateData.areStatisticsFinalized;
        playerData.finalizedScoreDelta = updateData.finalizedScoreDelta;
        playerData.isScorePersisted = updateData.isScorePersisted;
        playerData.isSubmitTimerActive = updateData.isSubmitTimerActive;
        playerData.submitTimerEndTime = updateData.submitTimerEndTime;
        playerData.isRiskSubmitReconnectGrace = updateData.isRiskSubmitReconnectGrace;
        playerData.isRiskDecisionPending = updateData.isRiskDecisionPending;
        playerData.queuedRiskPatterns = BingoPatternIdentityList.Clone(updateData.queuedRiskPatterns);
        playerData.activeRiskSubmitPatterns = BingoPatternIdentityList.Clone(updateData.activeRiskSubmitPatterns);
        playerData.lateRiskPatterns = BingoPatternIdentityList.Clone(updateData.lateRiskPatterns);
        playerData.pendingRiskCheckPatterns = BingoPatternIdentityList.Clone(updateData.pendingRiskCheckPatterns);
    }

    private static void ApplyPlayerMatchState(
        GamePlayerData playerData,
        GamePlayerMatchStateData updateData)
    {
        playerData.isConnected = updateData.isConnected;
        playerData.isGameSceneReady = updateData.isGameSceneReady;
        playerData.canRejoin = updateData.canRejoin;
        playerData.returnState = updateData.returnState;
        playerData.controlType = updateData.controlType;
        playerData.isAutomaticBoardEnabled = updateData.isAutomaticBoardEnabled;
        playerData.gameStatus = updateData.gameStatus;
        playerData.currentMatchScore = updateData.currentMatchScore;
        playerData.rank = updateData.rank;
        playerData.isRankFinal = updateData.isRankFinal;
        playerData.isRankWinBlocked = updateData.isRankWinBlocked;
        playerData.hasRiskCashedOut = updateData.hasRiskCashedOut;
        playerData.hasPendingRankCheck = updateData.hasPendingRankCheck;
        playerData.pendingRankCheckScore = updateData.pendingRankCheckScore;
        playerData.rankResolutionOrder = updateData.rankResolutionOrder;
        playerData.areStatisticsFinalized = updateData.areStatisticsFinalized;
        playerData.finalizedScoreDelta = updateData.finalizedScoreDelta;
        playerData.isScorePersisted = updateData.isScorePersisted;
        playerData.isSubmitTimerActive = updateData.isSubmitTimerActive;
        playerData.submitTimerEndTime = updateData.submitTimerEndTime;
        playerData.isRiskSubmitReconnectGrace = updateData.isRiskSubmitReconnectGrace;
        playerData.isRiskDecisionPending = updateData.isRiskDecisionPending;
    }

    private void ApplyFinalizedScoreForCurrentNetworkUser()
    {
        if (currentGameSession == null ||
            currentGameSession.runtimeType != SessionRuntimeType.Network ||
            MultiplayerPlayModeTestContext.IsActive)
        {
            return;
        }

        GameScoreAuthority.PersistFinalizedScoreForCurrentUser(currentGameSession);
    }

    private async void RequestGameSessionSync(bool recoverMissingEntry)
    {
        if (isGameSessionSyncPending)
        {
            return;
        }

        UserData userData = UserManager.instance?.CurrentUser;
        string gameId = currentGameSession?.gameId ?? pendingGameId;
        string lobbyId = ResolveExpectedLobbyId();

        if (userData == null || !userData.HasUser ||
            (string.IsNullOrWhiteSpace(gameId) && string.IsNullOrWhiteSpace(lobbyId)))
        {
            return;
        }

        SessionRuntimeType requestedRuntimeType = currentGameSession != null
            ? currentGameSession.runtimeType
            : (!string.IsNullOrWhiteSpace(gameId) ? ResolveRuntimeType(gameId) : LobbyManager.instance?.RuntimeType ?? runtimeType);

        isGameSessionSyncPending = true;
        nextGameSessionSyncTime = Time.realtimeSinceStartup + 1f;
        int currentAttemptVersion = entryAttemptVersion;

        if (recoverMissingEntry)
        {
            isEnteringGame = true;
            runtimeType = requestedRuntimeType;
            SetEntryState(GameSessionEntryState.Joining);
        }

        GameSessionResult result;

        try
        {
            IGameSessionService service = await WaitForGameServiceAsync(requestedRuntimeType);

            if (service == null)
            {
                result = GameSessionResult.Failed(
                    GameSessionOperationType.Sync,
                    GameSessionFailureType.ServiceUnavailable,
                    "The required Game session service was not ready for synchronization.",
                    gameId,
                    lobbyId);
            }
            else
            {
                result = await service.SyncGameSessionAsync(gameId, lobbyId, userData);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            result = GameSessionResult.Failed(
                GameSessionOperationType.Sync,
                GameSessionFailureType.Unknown,
                "An unexpected error occurred while synchronizing the Game.",
                gameId,
                lobbyId);
        }

        if (currentAttemptVersion != entryAttemptVersion)
        {
            return;
        }

        isGameSessionSyncPending = false;

        if (result == null || !result.success || result.gameSessionData == null)
        {
            if (recoverMissingEntry)
            {
                GameSessionResult failureResult = result ?? GameSessionResult.Failed(
                    GameSessionOperationType.Sync,
                    GameSessionFailureType.Unknown,
                    "The Game session manager did not return synchronization data.",
                    gameId,
                    lobbyId);

                if (isGameSimulationCreationPending && IsTransientGameSimulationSyncFailure(failureResult))
                {
                    lastEntryResult = null;
                    isEnteringGame = false;
                    SetEntryState(GameSessionEntryState.WaitingForService);
                    return;
                }

                CompleteGameEntryFailure(failureResult);
            }
            else
            {
                Debug.LogWarning($"[GameSessionManager] Game synchronization failed: {result?.failureMessage ?? "No result was returned."}");
            }

            return;
        }

        if (currentGameSession == null)
        {
            lastEntryResult = result;

            if (!TryApplySuccessfulResult(result, out GameSessionResult playerFailure))
            {
                CompleteGameEntryFailure(playerFailure);
                return;
            }

            GameEntryCompleted?.Invoke(result);
            return;
        }

        if (!string.Equals(result.gameSessionData.gameId, currentGameSession.gameId, StringComparison.Ordinal) ||
            result.gameSessionData.revision < currentGameSession.revision)
        {
            return;
        }

        ApplyAuthoritativeGameSessionSnapshot(result.gameSessionData);
    }

    private bool IsExpectedGameCreationResult(GameSessionResult result)
    {
        if (result == null)
        {
            return false;
        }

        string resultLobbyId = !string.IsNullOrWhiteSpace(result.lobbyId)
            ? result.lobbyId
            : result.gameSessionData?.lobbyId;
        string expectedLobbyId = ResolveExpectedLobbyId();

        return !string.IsNullOrWhiteSpace(resultLobbyId) &&
               !string.IsNullOrWhiteSpace(expectedLobbyId) &&
               string.Equals(resultLobbyId, expectedLobbyId, StringComparison.Ordinal);
    }

    private string ResolveExpectedLobbyId()
    {
        if (!string.IsNullOrWhiteSpace(pendingLobbyId))
        {
            return pendingLobbyId;
        }

        string currentLobbyId = LobbyManager.instance?.CurrentLobbyId;

        if (!string.IsNullOrWhiteSpace(currentLobbyId))
        {
            return currentLobbyId;
        }

        return currentGameSession?.lobbyId ?? string.Empty;
    }

    private void CompleteGameEntryFailure(GameSessionResult failureResult)
    {
        lastEntryResult = failureResult;
        isEnteringGame = false;
        isGameSessionSyncPending = false;
        isGameSimulationCreationPending = false;
        pendingLobbyId = failureResult?.lobbyId ?? string.Empty;

        if (ShouldClearLastGameId(failureResult?.failureType ?? GameSessionFailureType.Unknown))
        {
            UserManager.instance?.ClearLastGameId();
            pendingGameId = string.Empty;
        }

        SetEntryState(GameSessionEntryState.Failed);
        GameEntryFailed?.Invoke(failureResult);
        GameSceneManager.instance?.ReturnToMainSceneAfterFailure();
    }

    private async Task<IGameSessionService> WaitForGameServiceAsync(SessionRuntimeType requestedRuntimeType)
    {
        float timeoutTime = Time.realtimeSinceStartup + ServiceReadyTimeoutSeconds;

        while (Time.realtimeSinceStartup < timeoutTime)
        {
            IGameSessionService service = GetGameService(requestedRuntimeType);

            if (service != null && service.IsReady)
            {
                return service;
            }

            await Task.Yield();
        }

        return null;
    }

    private IGameSessionService GetGameService(SessionRuntimeType requestedRuntimeType)
    {
        return requestedRuntimeType == SessionRuntimeType.Local
            ? LocalGameSessionManager.instance
            : NetworkGameSessionService.instance;
    }

    private SessionRuntimeType ResolveRuntimeType(string gameId)
    {
        if (!string.IsNullOrWhiteSpace(gameId))
        {
            if (gameId.StartsWith(LocalGameSessionManager.GameIdPrefix, StringComparison.Ordinal))
            {
                return SessionRuntimeType.Local;
            }

            if (gameId.StartsWith(NetworkGameSessionManager.GameIdPrefix, StringComparison.Ordinal))
            {
                return SessionRuntimeType.Network;
            }
        }

        return LobbyManager.instance != null
            ? LobbyManager.instance.RuntimeType
            : SessionRuntimeType.Network;
    }

    private bool ShouldClearLastGameId(GameSessionFailureType failureType)
    {
        return failureType == GameSessionFailureType.GameNotFound ||
               failureType == GameSessionFailureType.PlayerNotFound ||
               failureType == GameSessionFailureType.PlayerNotEligible;
    }

    private bool IsAlreadyDetachedFailure(GameSessionFailureType failureType)
    {
        return failureType == GameSessionFailureType.GameNotFound ||
               failureType == GameSessionFailureType.PlayerNotFound ||
               failureType == GameSessionFailureType.PlayerNotEligible;
    }

    private bool IsTransientGameSimulationSyncFailure(GameSessionResult result)
    {
        if (result == null)
        {
            return false;
        }

        return result.failureType == GameSessionFailureType.GameNotFound ||
               result.failureType == GameSessionFailureType.ServiceUnavailable ||
               result.failureType == GameSessionFailureType.NetworkConnectionFailed ||
               result.failureType == GameSessionFailureType.NetworkGameConnectionUnavailable;
    }

    private void BeginSceneEventSubscription()
    {
        if ((TrySubscribeToSceneEvents() && TryRegisterSceneReadyCheck()) || sceneEventSubscriptionRoutine != null)
        {
            return;
        }

        sceneEventSubscriptionRoutine = StartCoroutine(SubscribeToSceneEventsWhenReady());
    }

    private IEnumerator SubscribeToSceneEventsWhenReady()
    {
        while (!TrySubscribeToSceneEvents() || !TryRegisterSceneReadyCheck())
        {
            yield return null;
        }

        sceneEventSubscriptionRoutine = null;
    }

    private bool TrySubscribeToSceneEvents()
    {
        if (isSubscribedToGameSceneManager)
        {
            return true;
        }

        gameSceneManager = GameSceneManager.instance;

        if (gameSceneManager == null)
        {
            return false;
        }

        gameSceneManager.SceneReadyForFadeOut -= OnSceneReadyForFadeOut;
        gameSceneManager.SceneReadyForFadeOut += OnSceneReadyForFadeOut;
        gameSceneManager.SceneReadyToStart -= OnSceneReadyToStart;
        gameSceneManager.SceneReadyToStart += OnSceneReadyToStart;
        isSubscribedToGameSceneManager = true;
        return true;
    }

    private bool TryRegisterSceneReadyCheck()
    {
        if (isSceneReadyCheckRegistered)
        {
            return true;
        }

        if (SceneReadyController.instance == null)
        {
            return false;
        }

        SceneReadyController.instance.RegisterReadyCheck(this, true);
        isSceneReadyCheckRegistered = true;
        return true;
    }

    private void EndSceneEventSubscription()
    {
        if (sceneEventSubscriptionRoutine != null)
        {
            StopCoroutine(sceneEventSubscriptionRoutine);
            sceneEventSubscriptionRoutine = null;
        }

        if (isSubscribedToGameSceneManager && gameSceneManager != null)
        {
            gameSceneManager.SceneReadyForFadeOut -= OnSceneReadyForFadeOut;
            gameSceneManager.SceneReadyToStart -= OnSceneReadyToStart;
        }

        if (isSceneReadyCheckRegistered && SceneReadyController.instance != null)
        {
            SceneReadyController.instance.UnregisterReadyCheck(this);
        }

        gameSceneManager = null;
        isSubscribedToGameSceneManager = false;
        isSceneReadyCheckRegistered = false;
    }

    private async void OnSceneReadyForFadeOut(GameSceneType sceneType)
    {
        if (sceneType != GameSceneType.Game || !HasEnteredGame || isReportingGameSceneReady)
        {
            return;
        }

        UserData userData = UserManager.instance?.CurrentUser;
        GamePlayerData playerData = GetCurrentPlayer();

        if (userData == null || !userData.HasUser || playerData == null || playerData.isGameSceneReady)
        {
            return;
        }

        string gameId = CurrentGameId;
        isReportingGameSceneReady = true;

        try
        {
            IGameSessionService service = await WaitForGameServiceAsync(runtimeType);

            if (service == null)
            {
                Debug.LogWarning("[GameSessionManager] The Game session service was not ready for the scene-ready update.");
                return;
            }

            GameSessionResult result = await service.SetGameSceneReadyAsync(gameId, userData);

            if (!string.Equals(gameId, CurrentGameId, StringComparison.Ordinal))
            {
                return;
            }

            if (result == null || !result.success)
            {
                Debug.LogWarning($"[GameSessionManager] Scene-ready update failed: {result?.failureMessage ?? "No result was returned."}");
                return;
            }

            if (result.gameSessionData != null)
            {
                ApplyGameSessionUpdate(result.gameSessionData);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            isReportingGameSceneReady = false;
        }
    }

    private void OnNetworkGameSessionUpdated(GameSessionData gameSessionData)
    {
        ApplyGameSessionUpdate(gameSessionData);
    }

    private void OnSceneReadyToStart(GameSceneType sceneType)
    {
        if (sceneType == GameSceneType.Game)
        {
            _ = StartPendingRiskSubmitAfterLoadingAsync();
        }
    }

    private void OnNetworkGamePlayStateChanged(GamePlayStateChangedData updateData)
    {
        ApplyGamePlayStateChanged(updateData);
    }

    private void OnLocalGameSessionUpdated(GameSessionData gameSessionData)
    {
        ApplyGameSessionUpdate(gameSessionData);
    }

    private void OnLocalGamePlayerMarkedCellChanged(
        GamePlayerMarkedCellChangedData updateData)
    {
        ApplyGamePlayerMarkedCellChanged(updateData);
    }

    private void OnLocalBingoCheckResolved(GameBingoCheckResolvedData resolvedData)
    {
        BingoCheckResolved?.Invoke(resolvedData);
    }

    private void OnNetworkGamePlayerStateChanged(GamePlayerStateChangedData updateData)
    {
        ApplyGamePlayerStateChanged(updateData);
    }

    private void OnNetworkGamePlayerMarkedCellChanged(
        GamePlayerMarkedCellChangedData updateData)
    {
        ApplyGamePlayerMarkedCellChanged(updateData);
    }

    private void OnNetworkBingoCheckResolved(GameBingoCheckResolvedData resolvedData)
    {
        if (resolvedData == null ||
            currentGameSession == null ||
            !string.Equals(
                resolvedData.gameId,
                currentGameSession.gameId,
                StringComparison.Ordinal))
        {
            return;
        }

        BingoCheckResolved?.Invoke(resolvedData);
    }

    private void OnNetworkGamePlayerLeft(GamePlayerLeftData updateData)
    {
        ApplyGamePlayerLeft(updateData);
    }

    private void OnNetworkGameDeleted(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return;
        }

        string savedGameId = UserManager.instance?.CurrentUser?.lastGameId;

        if (string.Equals(savedGameId, gameId, StringComparison.Ordinal))
        {
            UserManager.instance?.ClearLastGameId();
        }

        if (currentGameSession != null && string.Equals(currentGameSession.gameId, gameId, StringComparison.Ordinal))
        {
            currentGameSession = null;
            SetEntryState(GameSessionEntryState.Idle);
            GameSessionUpdated?.Invoke(null);
        }

        GameDeleted?.Invoke(gameId);
    }

    private void SetEntryState(GameSessionEntryState newEntryState)
    {
        if (entryState == newEntryState)
        {
            return;
        }

        entryState = newEntryState;
        GameEntryStateChanged?.Invoke(entryState);
    }
}
