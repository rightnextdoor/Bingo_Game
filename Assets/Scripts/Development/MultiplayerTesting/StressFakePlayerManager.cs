using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum StressFakePlayerState
{
    PendingAdmission,
    Loading,
    SceneReady,
    Removed,
    Failed
}

public enum StressFakePlayerJoinOutcome
{
    Running,
    Completed,
    LobbyStarted,
    Failed
}

public enum MultiplayerStressTargetPlayer
{
    Player1 = 1,
    Player2 = 2,
    Player3 = 3,
    Player4 = 4
}

[Serializable]
public class StressFakePlayerJoinRequest
{
    public string lobbyId = string.Empty;
    [Min(1)] public int playerCount = 1;
    [Min(1)] public int minimumJoinBatch = 1;
    [Min(1)] public int maximumJoinBatch = 8;
    [Min(0f)] public float minimumJoinDelaySeconds = 0.2f;
    [Min(0f)] public float maximumJoinDelaySeconds = 1.5f;
    [Min(0f)] public float minimumLoadDelaySeconds = 0.5f;
    [Min(0f)] public float maximumLoadDelaySeconds = 4f;
    public bool firstPlayerIsHost;
    public bool limitToAvailableLobbyCapacity;
}

public class StressFakePlayerJoinResult
{
    public int operationId;
    public string lobbyId = string.Empty;
    public int requested;
    public int admitted;
    public int sceneReady;
    public int rejected;
    public int removedBeforeReady;
    public int notAdmittedDueToCapacity;
    public int notAdmittedBeforeStart;
    public bool completed;
    public StressFakePlayerJoinOutcome outcome = StressFakePlayerJoinOutcome.Running;
    public string failureReason = string.Empty;

    public bool Succeeded => completed && outcome != StressFakePlayerJoinOutcome.Failed && rejected == 0;
    public bool ReachedRequestedTarget => completed && outcome == StressFakePlayerJoinOutcome.Completed && admitted == requested && sceneReady == requested;
}

public class StressFakePlayerRecord
{
    public int operationId;
    public string lobbyId = string.Empty;
    public string userId = string.Empty;
    public string playerName = string.Empty;
    public bool isHost;
    public StressFakePlayerState state;
    public double loadCompleteTime;
}

[DisallowMultipleComponent]
public class StressFakePlayerManager : MonoBehaviour
{
    #region Fields

    public static StressFakePlayerManager instance;

    private readonly Dictionary<int, StressFakePlayerJoinOperation> joinOperations = new Dictionary<int, StressFakePlayerJoinOperation>();
    private readonly Dictionary<string, StressFakePlayerRecord> playersByUserId = new Dictionary<string, StressFakePlayerRecord>(StringComparer.Ordinal);
    private readonly HashSet<string> loadingUserIds = new HashSet<string>(StringComparer.Ordinal);

    private int nextOperationId = 1;
    private long nextFakePlayerNumber = 1;

    public int SyntheticPlayerCount => GetSyntheticPlayerCount();
    public int PendingSyntheticPlayerCount => GetPendingSyntheticPlayerCount();

    public event Action<StressFakePlayerJoinResult> JoinWaveCompleted;
    public event Action<StressFakePlayerJoinResult> JoinWaveClosedByLobbyStart;
    public event Action<StressFakePlayerJoinResult> JoinWaveFailed;

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

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ProcessLoadingPlayers();
#endif
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Join Waves

    public int StartJoinWave(StressFakePlayerJoinRequest request)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!TryValidateRequest(request, out string failureReason))
        {
            StressFakePlayerJoinResult failedResult = new StressFakePlayerJoinResult
            {
                operationId = 0,
                lobbyId = request?.lobbyId ?? string.Empty,
                requested = request != null ? Mathf.Max(0, request.playerCount) : 0,
                completed = true,
                outcome = StressFakePlayerJoinOutcome.Failed,
                failureReason = failureReason
            };

            JoinWaveFailed?.Invoke(failedResult);
            return 0;
        }

        int operationId = nextOperationId++;
        StressFakePlayerJoinOperation operation = new StressFakePlayerJoinOperation(operationId, CloneRequest(request));
        ApplyInitialCapacityLimit(operation);
        joinOperations[operationId] = operation;
        operation.routine = StartCoroutine(RunJoinWave(operation));
        return operationId;
#else
        return 0;
#endif
    }

    public bool IsJoinWaveRunning(int operationId)
    {
        return joinOperations.TryGetValue(operationId, out StressFakePlayerJoinOperation operation) && !operation.result.completed;
    }

    public bool TryGetJoinWaveResult(int operationId, out StressFakePlayerJoinResult result)
    {
        result = null;

        if (!joinOperations.TryGetValue(operationId, out StressFakePlayerJoinOperation operation))
        {
            return false;
        }

        result = operation.result;
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private IEnumerator RunJoinWave(StressFakePlayerJoinOperation operation)
    {
        StressFakePlayerJoinRequest request = operation.request;

        while (operation.createdCount < operation.targetPlayerCount)
        {
            if (!TryGetLobby(request.lobbyId, out Lobby lobby))
            {
                operation.result.failureReason = "The target lobby is no longer available.";
                break;
            }

            if (lobby.lobbyState != LobbyState.Open || lobby.Controller == null || lobby.Controller.IsJoinLocked)
            {
                MarkLobbyStarted(operation);
                break;
            }

            if (request.limitToAvailableLobbyCapacity && !lobby.Controller.UnlimitedPlayers && lobby.Controller.IsFull)
            {
                MarkLobbyCapacityReached(operation);
                break;
            }

            int remaining = operation.targetPlayerCount - operation.createdCount;
            int minimumBatch = Mathf.Clamp(request.minimumJoinBatch, 1, remaining);
            int maximumBatch = Mathf.Clamp(Mathf.Max(request.maximumJoinBatch, minimumBatch), minimumBatch, remaining);
            int batchCount = UnityEngine.Random.Range(minimumBatch, maximumBatch + 1);

            for (int i = 0; i < batchCount && operation.createdCount < operation.targetPlayerCount; i++)
            {
                if (request.limitToAvailableLobbyCapacity && IsLobbyAtCapacity(request.lobbyId))
                {
                    MarkLobbyCapacityReached(operation);
                    break;
                }

                bool isFirstPlayer = operation.createdCount == 0;
                bool isHost = request.firstPlayerIsHost && isFirstPlayer;
                UserData userData = CreateFakeUser();

                if (!NetworkLobbyManager.instance.TryAddStressPlayer(request.lobbyId, userData, isHost, out string failureReason))
                {
                    if (request.limitToAvailableLobbyCapacity && IsLobbyAtCapacity(request.lobbyId))
                    {
                        MarkLobbyCapacityReached(operation);
                        break;
                    }

                    if (IsLobbyStarted(request.lobbyId))
                    {
                        MarkLobbyStarted(operation);
                        break;
                    }

                    operation.createdCount++;
                    operation.result.rejected++;
                    operation.result.outcome = StressFakePlayerJoinOutcome.Failed;
                    operation.result.failureReason = failureReason;
                    break;
                }

                operation.createdCount++;

                StressFakePlayerRecord record = new StressFakePlayerRecord
                {
                    operationId = operation.operationId,
                    lobbyId = request.lobbyId,
                    userId = userData.userId,
                    playerName = userData.playerName,
                    isHost = isHost,
                    state = StressFakePlayerState.Loading,
                    loadCompleteTime = Time.unscaledTimeAsDouble + GetRandomRange(request.minimumLoadDelaySeconds, request.maximumLoadDelaySeconds)
                };

                playersByUserId[record.userId] = record;
                loadingUserIds.Add(record.userId);
                operation.userIds.Add(record.userId);
                operation.result.admitted++;
            }

            if (operation.result.outcome == StressFakePlayerJoinOutcome.LobbyStarted || operation.result.outcome == StressFakePlayerJoinOutcome.Failed)
            {
                break;
            }

            if (operation.createdCount < operation.targetPlayerCount)
            {
                yield return new WaitForSecondsRealtime(GetRandomRange(request.minimumJoinDelaySeconds, request.maximumJoinDelaySeconds));
            }
        }

        while (HasPendingPlayers(operation))
        {
            yield return null;
        }

        CompleteJoinOperation(operation);
    }

    private void CompleteJoinOperation(StressFakePlayerJoinOperation operation)
    {
        operation.result.completed = true;

        if (operation.result.outcome == StressFakePlayerJoinOutcome.Running)
        {
            operation.result.outcome = StressFakePlayerJoinOutcome.Completed;
        }

        if (operation.result.outcome == StressFakePlayerJoinOutcome.LobbyStarted)
        {
            operation.result.notAdmittedBeforeStart = Mathf.Max(operation.result.notAdmittedBeforeStart, operation.result.requested - operation.result.admitted - operation.result.rejected - operation.result.notAdmittedDueToCapacity);
            JoinWaveClosedByLobbyStart?.Invoke(operation.result);
            return;
        }

        if (operation.result.outcome == StressFakePlayerJoinOutcome.Failed || operation.result.rejected > 0 || operation.result.removedBeforeReady > 0)
        {
            operation.result.outcome = StressFakePlayerJoinOutcome.Failed;
            JoinWaveFailed?.Invoke(operation.result);
            return;
        }

        JoinWaveCompleted?.Invoke(operation.result);
    }

    public bool CancelJoinWave(int operationId, string reason)
    {
        if (!joinOperations.TryGetValue(operationId, out StressFakePlayerJoinOperation operation) || operation.result.completed)
        {
            return false;
        }

        if (operation.routine != null)
        {
            StopCoroutine(operation.routine);
            operation.routine = null;
        }

        operation.result.completed = true;
        operation.result.outcome = StressFakePlayerJoinOutcome.Failed;
        operation.result.failureReason = string.IsNullOrWhiteSpace(reason) ? "The fake-player join wave was cancelled." : reason.Trim();
        JoinWaveFailed?.Invoke(operation.result);
        return true;
    }

    private void MarkLobbyStarted(StressFakePlayerJoinOperation operation)
    {
        if (operation == null || operation.result.outcome == StressFakePlayerJoinOutcome.Failed)
        {
            return;
        }

        operation.result.outcome = StressFakePlayerJoinOutcome.LobbyStarted;
        operation.result.notAdmittedBeforeStart = Mathf.Max(operation.result.notAdmittedBeforeStart, operation.result.requested - operation.result.admitted - operation.result.rejected - operation.result.notAdmittedDueToCapacity);
        operation.result.failureReason = string.Empty;
    }

    private void ApplyInitialCapacityLimit(StressFakePlayerJoinOperation operation)
    {
        if (operation == null || !operation.request.limitToAvailableLobbyCapacity || !TryGetLobby(operation.request.lobbyId, out Lobby lobby) || lobby.Controller == null || lobby.Controller.UnlimitedPlayers)
        {
            return;
        }

        int availableCapacity = Mathf.Max(0, lobby.Controller.MaxPlayers - lobby.Controller.PlayerCount);
        operation.targetPlayerCount = Mathf.Min(operation.targetPlayerCount, availableCapacity);
        operation.result.notAdmittedDueToCapacity = Mathf.Max(0, operation.result.requested - operation.targetPlayerCount);
    }

    private void MarkLobbyCapacityReached(StressFakePlayerJoinOperation operation)
    {
        if (operation == null || operation.result.outcome == StressFakePlayerJoinOutcome.Failed)
        {
            return;
        }

        operation.targetPlayerCount = operation.createdCount;
        operation.result.notAdmittedDueToCapacity = Mathf.Max(operation.result.notAdmittedDueToCapacity, operation.result.requested - operation.result.admitted - operation.result.rejected - operation.result.notAdmittedBeforeStart);
    }

    private bool IsLobbyAtCapacity(string lobbyId)
    {
        return TryGetLobby(lobbyId, out Lobby lobby) && lobby.Controller != null && !lobby.Controller.UnlimitedPlayers && lobby.Controller.IsFull;
    }

    private bool IsLobbyStarted(string lobbyId)
    {
        return TryGetLobby(lobbyId, out Lobby lobby) && lobby.Controller != null && (lobby.lobbyState != LobbyState.Open || lobby.Controller.IsJoinLocked);
    }

#endif

    #endregion

    #region Player Tracking

    public List<StressFakePlayerRecord> GetSceneReadyPlayersForLobby(string lobbyId)
    {
        List<StressFakePlayerRecord> players = new List<StressFakePlayerRecord>();

        if (string.IsNullOrWhiteSpace(lobbyId))
        {
            return players;
        }

        foreach (StressFakePlayerRecord record in playersByUserId.Values)
        {
            if (record == null || record.lobbyId != lobbyId || record.state != StressFakePlayerState.SceneReady)
            {
                continue;
            }

            if (TryGetLobby(lobbyId, out Lobby lobby) && lobby.Controller != null && lobby.Controller.HasPlayer(record.userId))
            {
                players.Add(record);
            }
        }

        return players;
    }

    public bool IsSyntheticPlayer(string userId)
    {
        return !string.IsNullOrWhiteSpace(userId) && playersByUserId.ContainsKey(userId);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void ProcessLoadingPlayers()
    {
        if (NetworkLobbyManager.instance == null || !NetworkLobbyManager.instance.IsReady)
        {
            return;
        }

        double currentTime = Time.unscaledTimeAsDouble;
        List<string> loadingIds = new List<string>(loadingUserIds);

        for (int i = 0; i < loadingIds.Count; i++)
        {
            if (!playersByUserId.TryGetValue(loadingIds[i], out StressFakePlayerRecord record))
            {
                loadingUserIds.Remove(loadingIds[i]);
                continue;
            }

            if (record == null || record.state != StressFakePlayerState.Loading)
            {
                continue;
            }

            if (!TryGetLobby(record.lobbyId, out Lobby lobby) || lobby.Controller == null || !lobby.Controller.HasPlayer(record.userId))
            {
                MarkRemovedBeforeReady(record);
                continue;
            }

            if (currentTime < record.loadCompleteTime)
            {
                continue;
            }

            if (!NetworkLobbyManager.instance.TrySetStressPlayerSceneReady(record.lobbyId, record.userId, out _))
            {
                MarkRemovedBeforeReady(record);
                continue;
            }

            record.state = StressFakePlayerState.SceneReady;
            loadingUserIds.Remove(record.userId);

            if (joinOperations.TryGetValue(record.operationId, out StressFakePlayerJoinOperation operation))
            {
                operation.result.sceneReady++;
            }
        }
    }

    private void MarkRemovedBeforeReady(StressFakePlayerRecord record)
    {
        if (record == null || record.state != StressFakePlayerState.Loading)
        {
            return;
        }

        record.state = StressFakePlayerState.Removed;
        loadingUserIds.Remove(record.userId);

        if (!joinOperations.TryGetValue(record.operationId, out StressFakePlayerJoinOperation operation))
        {
            return;
        }

        operation.result.removedBeforeReady++;

        if (IsLobbyStarted(record.lobbyId))
        {
            MarkLobbyStarted(operation);
            return;
        }

        operation.result.outcome = StressFakePlayerJoinOutcome.Failed;

        if (string.IsNullOrWhiteSpace(operation.result.failureReason))
        {
            operation.result.failureReason = "One or more fake players were removed before finishing Lobby loading.";
        }
    }

    private bool HasPendingPlayers(StressFakePlayerJoinOperation operation)
    {
        if (operation == null)
        {
            return false;
        }

        for (int i = 0; i < operation.userIds.Count; i++)
        {
            if (playersByUserId.TryGetValue(operation.userIds[i], out StressFakePlayerRecord record) && record.state == StressFakePlayerState.Loading)
            {
                return true;
            }
        }

        return false;
    }

#endif

    #endregion

    #region Helpers

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private bool TryValidateRequest(StressFakePlayerJoinRequest request, out string failureReason)
    {
        failureReason = string.Empty;

        if (NetworkLobbyManager.instance == null || !NetworkLobbyManager.instance.IsReady)
        {
            failureReason = "The network lobby manager is not ready.";
            return false;
        }

        if (request == null || string.IsNullOrWhiteSpace(request.lobbyId) || request.playerCount <= 0)
        {
            failureReason = "The fake-player join request is invalid.";
            return false;
        }

        if (!TryGetLobby(request.lobbyId, out Lobby lobby) || lobby.Controller == null)
        {
            failureReason = "The target lobby could not be found.";
            return false;
        }

        if (lobby.playMode != MainMenuPlayMode.Online && lobby.playMode != MainMenuPlayMode.Custom)
        {
            failureReason = "Fake multiplayer players can only join Online or Custom lobbies.";
            return false;
        }

        return true;
    }

#endif

    private bool TryGetLobby(string lobbyId, out Lobby lobby)
    {
        lobby = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return NetworkLobbyManager.instance != null && NetworkLobbyManager.instance.TryGetStressLobby(lobbyId, out lobby);
#else
        return false;
#endif
    }

    private UserData CreateFakeUser()
    {
        long playerNumber = nextFakePlayerNumber++;
        UserData userData = new UserData
        {
            userId = $"stress-player-{playerNumber:D6}",
            userTag = UserTag.Player,
            playerName = $"Stress Player {playerNumber:D4}"
        };

        return userData;
    }

    private StressFakePlayerJoinRequest CloneRequest(StressFakePlayerJoinRequest request)
    {
        return new StressFakePlayerJoinRequest
        {
            lobbyId = request.lobbyId,
            playerCount = Mathf.Max(1, request.playerCount),
            minimumJoinBatch = Mathf.Max(1, request.minimumJoinBatch),
            maximumJoinBatch = Mathf.Max(1, request.maximumJoinBatch),
            minimumJoinDelaySeconds = Mathf.Max(0f, request.minimumJoinDelaySeconds),
            maximumJoinDelaySeconds = Mathf.Max(0f, request.maximumJoinDelaySeconds),
            minimumLoadDelaySeconds = Mathf.Max(0f, request.minimumLoadDelaySeconds),
            maximumLoadDelaySeconds = Mathf.Max(0f, request.maximumLoadDelaySeconds),
            firstPlayerIsHost = request.firstPlayerIsHost,
            limitToAvailableLobbyCapacity = request.limitToAvailableLobbyCapacity
        };
    }

    private float GetRandomRange(float minimum, float maximum)
    {
        float min = Mathf.Max(0f, Mathf.Min(minimum, maximum));
        float max = Mathf.Max(min, Mathf.Max(minimum, maximum));
        return Mathf.Approximately(min, max) ? min : UnityEngine.Random.Range(min, max);
    }

    private int GetSyntheticPlayerCount()
    {
        int count = 0;

        foreach (StressFakePlayerRecord record in playersByUserId.Values)
        {
            if (record == null)
            {
                continue;
            }

            if (record.state == StressFakePlayerState.Loading)
            {
                count++;
                continue;
            }

            if (record.state == StressFakePlayerState.SceneReady &&
                TryGetLobby(record.lobbyId, out Lobby lobby) &&
                lobby.Controller != null &&
                lobby.Controller.HasPlayer(record.userId))
            {
                count++;
            }
        }

        return count;
    }

    private int GetPendingSyntheticPlayerCount()
    {
        return loadingUserIds.Count;
    }

    private class StressFakePlayerJoinOperation
    {
        public readonly int operationId;
        public readonly StressFakePlayerJoinRequest request;
        public readonly StressFakePlayerJoinResult result;
        public readonly List<string> userIds = new List<string>();
        public Coroutine routine;
        public int createdCount;
        public int targetPlayerCount;

        public StressFakePlayerJoinOperation(int operationId, StressFakePlayerJoinRequest request)
        {
            this.operationId = operationId;
            this.request = request;
            targetPlayerCount = request.playerCount;
            result = new StressFakePlayerJoinResult
            {
                operationId = operationId,
                lobbyId = request.lobbyId,
                requested = request.playerCount
            };
        }
    }

    #endregion
}
