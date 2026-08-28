using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class GameSessionManager : MonoBehaviour, ISceneReadyCheck
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
    private bool isGameSessionSyncPending;
    private float nextGameSessionSyncTime;
    private int entryAttemptVersion;
    private Coroutine sceneEventSubscriptionRoutine;
    private GameSceneManager gameSceneManager;
    private bool isSubscribedToGameSceneManager;
    private bool isSceneReadyCheckRegistered;

    public string CurrentGameId => currentGameSession?.gameId ?? string.Empty;
    public string CurrentLobbyId => currentGameSession?.lobbyId ?? pendingLobbyId;
    public SessionRuntimeType RuntimeType => runtimeType;
    public GameSessionEntryState EntryState => entryState;
    public GameSessionData CurrentGameSession => currentGameSession;
    public GameSessionResult LastEntryResult => lastEntryResult;
    public bool IsEnteringGame => isEnteringGame;
    public bool IsLeavingGame => isLeavingGame;
    public bool HasEnteredGame => entryState == GameSessionEntryState.Completed && currentGameSession != null;

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
        NetworkGameSessionConnection.LocalGameCreationResultReceived -= ReceiveGameCreationResult;
        NetworkGameSessionConnection.LocalGameCreationResultReceived += ReceiveGameCreationResult;
        NetworkGameSessionConnection.LocalGameSessionUpdatedReceived -= OnNetworkGameSessionUpdated;
        NetworkGameSessionConnection.LocalGameSessionUpdatedReceived += OnNetworkGameSessionUpdated;
        NetworkGameSessionConnection.LocalGamePlayerStateChangedReceived -= OnNetworkGamePlayerStateChanged;
        NetworkGameSessionConnection.LocalGamePlayerStateChangedReceived += OnNetworkGamePlayerStateChanged;
        NetworkGameSessionConnection.LocalGamePlayerLeftReceived -= OnNetworkGamePlayerLeft;
        NetworkGameSessionConnection.LocalGamePlayerLeftReceived += OnNetworkGamePlayerLeft;
        NetworkGameSessionConnection.LocalGameDeletedReceived -= OnNetworkGameDeleted;
        NetworkGameSessionConnection.LocalGameDeletedReceived += OnNetworkGameDeleted;

        BeginSceneEventSubscription();
    }

    private void OnDisable()
    {
        NetworkGameSessionConnection.LocalGameCreationResultReceived -= ReceiveGameCreationResult;
        NetworkGameSessionConnection.LocalGameSessionUpdatedReceived -= OnNetworkGameSessionUpdated;
        NetworkGameSessionConnection.LocalGamePlayerStateChangedReceived -= OnNetworkGamePlayerStateChanged;
        NetworkGameSessionConnection.LocalGamePlayerLeftReceived -= OnNetworkGamePlayerLeft;
        NetworkGameSessionConnection.LocalGameDeletedReceived -= OnNetworkGameDeleted;
        EndSceneEventSubscription();
    }

    private void OnDestroy()
    {
        NetworkGameSessionConnection.LocalGameCreationResultReceived -= ReceiveGameCreationResult;
        NetworkGameSessionConnection.LocalGameSessionUpdatedReceived -= OnNetworkGameSessionUpdated;
        NetworkGameSessionConnection.LocalGamePlayerStateChangedReceived -= OnNetworkGamePlayerStateChanged;
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
        if (GameSceneManager.instance == null ||
            GameSceneManager.instance.CurrentSceneType != GameSceneType.Game ||
            HasEnteredGame ||
            isEnteringGame ||
            entryState == GameSessionEntryState.Failed ||
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

    public void ClearCurrentGame(bool clearLastGameId)
    {
        entryAttemptVersion++;
        isEnteringGame = false;
        pendingGameId = string.Empty;
        pendingLobbyId = string.Empty;
        currentGameSession = null;
        lastEntryResult = null;
        isLeavingGame = false;
        isReportingGameSceneReady = false;
        isGameSessionSyncPending = false;
        SetEntryState(GameSessionEntryState.Idle);

        if (clearLastGameId)
        {
            UserManager.instance?.ClearLastGameId();
        }

        GameSessionUpdated?.Invoke(null);
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
        runtimeType = currentGameSession.runtimeType;
        pendingGameId = string.Empty;
        pendingLobbyId = currentGameSession.lobbyId;
        isEnteringGame = false;
        isGameSessionSyncPending = false;
        SetEntryState(GameSessionEntryState.Completed);
        UserManager.instance?.SetLastGameId(currentGameSession.gameId);
        GameSessionUpdated?.Invoke(new GameSessionData(currentGameSession));
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

        playerData.isConnected = updateData.isConnected;
        playerData.isGameSceneReady = updateData.isGameSceneReady;
        playerData.canRejoin = updateData.canRejoin;
        currentGameSession.revision = updateData.revision;
        GameSessionUpdated?.Invoke(new GameSessionData(currentGameSession));
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
        GameSessionUpdated?.Invoke(new GameSessionData(currentGameSession));
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
                CompleteGameEntryFailure(result ?? GameSessionResult.Failed(
                    GameSessionOperationType.Sync,
                    GameSessionFailureType.Unknown,
                    "The Game session manager did not return synchronization data.",
                    gameId,
                    lobbyId));
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

    private void OnNetworkGamePlayerStateChanged(GamePlayerStateChangedData updateData)
    {
        ApplyGamePlayerStateChanged(updateData);
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
