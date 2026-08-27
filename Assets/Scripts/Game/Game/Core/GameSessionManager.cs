using System;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class GameSessionManager : MonoBehaviour
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
    private int entryAttemptVersion;

    public string CurrentGameId => currentGameSession?.gameId ?? string.Empty;
    public string CurrentLobbyId => currentGameSession?.lobbyId ?? pendingLobbyId;
    public SessionRuntimeType RuntimeType => runtimeType;
    public GameSessionEntryState EntryState => entryState;
    public GameSessionData CurrentGameSession => currentGameSession;
    public GameSessionResult LastEntryResult => lastEntryResult;
    public bool IsEnteringGame => isEnteringGame;
    public bool HasEnteredGame => entryState == GameSessionEntryState.Completed && currentGameSession != null;

    public event Action<GameSessionEntryState> GameEntryStateChanged;
    public event Action<GameSessionResult> GameCreationCompleted;
    public event Action<GameSessionResult> GameCreationFailed;
    public event Action<GameSessionResult> GameEntryCompleted;
    public event Action<GameSessionResult> GameEntryFailed;
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
        NetworkGameSessionConnection.LocalGameDeletedReceived -= OnNetworkGameDeleted;
        NetworkGameSessionConnection.LocalGameDeletedReceived += OnNetworkGameDeleted;
    }

    private void OnDisable()
    {
        NetworkGameSessionConnection.LocalGameCreationResultReceived -= ReceiveGameCreationResult;
        NetworkGameSessionConnection.LocalGameDeletedReceived -= OnNetworkGameDeleted;
    }

    private void OnDestroy()
    {
        NetworkGameSessionConnection.LocalGameCreationResultReceived -= ReceiveGameCreationResult;
        NetworkGameSessionConnection.LocalGameDeletedReceived -= OnNetworkGameDeleted;

        if (instance == this)
        {
            instance = null;
        }
    }

    public void PrepareForGameCreation(string lobbyId, SessionRuntimeType requestedRuntimeType)
    {
        pendingLobbyId = lobbyId ?? string.Empty;
        pendingGameId = string.Empty;
        runtimeType = requestedRuntimeType;
        lastEntryResult = null;
        isEnteringGame = false;
        SetEntryState(GameSessionEntryState.WaitingForService);
    }

    public void ReceiveGameCreationResult(GameSessionResult result)
    {
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
        SetEntryState(GameSessionEntryState.Idle);

        if (clearLastGameId)
        {
            UserManager.instance?.ClearLastGameId();
        }
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
        SetEntryState(GameSessionEntryState.Completed);
        UserManager.instance?.SetLastGameId(currentGameSession.gameId);
        return true;
    }

    private void CompleteGameEntryFailure(GameSessionResult failureResult)
    {
        lastEntryResult = failureResult;
        isEnteringGame = false;
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
