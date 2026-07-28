using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

public class StressHealthSnapshot
{
    public int activeServers;
    public int activeLobbies;
    public int activeGames;
    public int connectedClients;
    public int syntheticPlayers;
    public int pendingSyntheticPlayers;
    public int totalLobbyMembers;
    public int schedulerQueuedItems;
    public long schedulerQueuedBytes;
    public double oldestQueuedWorkSeconds;
}

[DisallowMultipleComponent]
public class StressHealthReporter : MonoBehaviour
{
    #region Fields

    public static StressHealthReporter instance;

    [Header("Health Sampling")]
    [SerializeField, Min(0.05f)] private float healthSampleIntervalSeconds = 0.25f;

    private readonly Dictionary<int, StressHealthRun> activeRuns = new Dictionary<int, StressHealthRun>();

    private StressFakePlayerManager fakePlayerManager;
    private NetworkLobbyManager lobbyManager;
    private MultiplayerSessionLifecycle sessionLifecycle;

    private int nextRunId = 1;
    private double nextHealthSampleTime;
    private bool eventsSubscribed;

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
        TrySubscribeToEvents();
        SampleActiveRuns();
#endif
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnsubscribeFromEvents();
#endif

        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Runs

    public int BeginRun(string runName, string setupSummary = "")
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int runId = nextRunId++;
        string resolvedRunName = string.IsNullOrWhiteSpace(runName) ? "Stress Test" : runName.Trim();
        StressHealthSnapshot startingSnapshot = CaptureSnapshot();
        StressHealthRun run = new StressHealthRun(runId, resolvedRunName, startingSnapshot, Time.unscaledTimeAsDouble);
        activeRuns[runId] = run;
        ApplySnapshotToRun(run, startingSnapshot);

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[STRESS][TEST START]");
        builder.AppendLine($"Run: {run.name}");
        builder.AppendLine($"Run ID: {run.id}");

        if (!string.IsNullOrWhiteSpace(setupSummary))
        {
            builder.AppendLine(setupSummary.Trim());
        }

        AppendHealth(builder, startingSnapshot, "Starting Health:");
        Debug.Log(builder.ToString());
        return runId;
#else
        return 0;
#endif
    }

    public void CompleteRun(int runId, bool success, string summary, string failureReason = "")
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!activeRuns.TryGetValue(runId, out StressHealthRun run))
        {
            return;
        }

        StressHealthSnapshot endingSnapshot = CaptureSnapshot();
        ApplySnapshotToRun(run, endingSnapshot);
        activeRuns.Remove(runId);

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[STRESS][TEST END]");
        builder.AppendLine($"Run: {run.name}");
        builder.AppendLine($"Run ID: {run.id}");
        builder.AppendLine($"Duration: {Math.Max(0d, Time.unscaledTimeAsDouble - run.startedTime):F2}s");

        if (!string.IsNullOrWhiteSpace(summary))
        {
            builder.AppendLine(summary.Trim());
        }

        if (!success && !string.IsNullOrWhiteSpace(failureReason))
        {
            builder.AppendLine($"Reason: {failureReason.Trim()}");
        }

        AppendHealth(builder, run.startingSnapshot, "Starting Health:");
        AppendHealth(builder, endingSnapshot, "Ending Health:");
        AppendPeakHealth(builder, run);
        builder.Append($"Result: {(success ? "PASS" : "FAIL")}");
        Debug.Log(builder.ToString());
#endif
    }

    public void ReportTestNotStarted(string requestedRunName, string activeRunName, string reason)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[STRESS][TEST NOT STARTED]");
        builder.AppendLine($"Requested: {(string.IsNullOrWhiteSpace(requestedRunName) ? "Stress Test" : requestedRunName.Trim())}");

        if (!string.IsNullOrWhiteSpace(activeRunName))
        {
            builder.AppendLine($"Active Test: {activeRunName.Trim()}");
        }

        builder.AppendLine($"Reason: {(string.IsNullOrWhiteSpace(reason) ? "Another stress simulation is still running." : reason.Trim())}");
        Debug.Log(builder.ToString());
#endif
    }

    #endregion

    #region Explicit Reports

    public void ReportFailure(string category, string reason, string lobbyId = "")
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"[STRESS][{(string.IsNullOrWhiteSpace(category) ? "FAILURE" : category.Trim().ToUpperInvariant())}]");

        if (!string.IsNullOrWhiteSpace(lobbyId))
        {
            builder.AppendLine($"Lobby: {lobbyId}");
        }

        builder.AppendLine($"Reason: {(string.IsNullOrWhiteSpace(reason) ? "Unknown failure." : reason.Trim())}");
        AppendHealth(builder, CaptureSnapshot());
        Debug.LogError(builder.ToString());
#endif
    }

    #endregion

    #region Event Subscription

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void TrySubscribeToEvents()
    {
        NetworkLobbyManager currentLobbyManager = NetworkLobbyManager.instance;
        MultiplayerSessionLifecycle currentLifecycle = MultiplayerSessionLifecycle.instance;
        StressFakePlayerManager currentFakePlayerManager = StressFakePlayerManager.instance;

        if (eventsSubscribed && lobbyManager == currentLobbyManager && sessionLifecycle == currentLifecycle && fakePlayerManager == currentFakePlayerManager)
        {
            return;
        }

        UnsubscribeFromEvents();

        lobbyManager = currentLobbyManager;
        sessionLifecycle = currentLifecycle;
        fakePlayerManager = currentFakePlayerManager;

        if (lobbyManager != null)
        {
            lobbyManager.LobbyCreated += OnLobbyCreated;
            lobbyManager.LobbyFinalCountdownCompleted += OnLobbyFinalCountdownCompleted;
            lobbyManager.LobbyGameCreated += OnLobbyGameCreated;
            lobbyManager.LobbyClosed += OnLobbyClosed;
        }

        if (sessionLifecycle != null)
        {
            sessionLifecycle.ConnectionLost += OnConnectionLost;
        }

        if (fakePlayerManager != null)
        {
            fakePlayerManager.JoinWaveCompleted += OnJoinWaveCompleted;
            fakePlayerManager.JoinWaveClosedByLobbyStart += OnJoinWaveClosedByLobbyStart;
            fakePlayerManager.JoinWaveFailed += OnJoinWaveFailed;
        }

        eventsSubscribed = lobbyManager != null || sessionLifecycle != null || fakePlayerManager != null;
    }

    private void UnsubscribeFromEvents()
    {
        if (lobbyManager != null)
        {
            lobbyManager.LobbyCreated -= OnLobbyCreated;
            lobbyManager.LobbyFinalCountdownCompleted -= OnLobbyFinalCountdownCompleted;
            lobbyManager.LobbyGameCreated -= OnLobbyGameCreated;
            lobbyManager.LobbyClosed -= OnLobbyClosed;
        }

        if (sessionLifecycle != null)
        {
            sessionLifecycle.ConnectionLost -= OnConnectionLost;
        }

        if (fakePlayerManager != null)
        {
            fakePlayerManager.JoinWaveCompleted -= OnJoinWaveCompleted;
            fakePlayerManager.JoinWaveClosedByLobbyStart -= OnJoinWaveClosedByLobbyStart;
            fakePlayerManager.JoinWaveFailed -= OnJoinWaveFailed;
        }

        eventsSubscribed = false;
    }

#endif

    #endregion

    #region Lifecycle Reports

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void OnLobbyCreated(Lobby lobby)
    {
        if (!IsStressRunActive() || lobby?.Controller == null)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[STRESS][LOBBY CREATED]");
        AppendLobby(builder, lobby);
        AppendHealth(builder, CaptureSnapshot());
        Debug.Log(builder.ToString());
    }

    private void OnLobbyFinalCountdownCompleted(Lobby lobby)
    {
        if (!IsStressRunActive() || lobby?.Controller == null)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[STRESS][LOBBY COUNTDOWN COMPLETE]");
        AppendLobby(builder, lobby);
        builder.AppendLine($"Players: {lobby.Controller.PlayerCount}");
        builder.AppendLine($"Scene Ready: {lobby.Controller.SceneReadyPlayerCount}");
        AppendHealth(builder, CaptureSnapshot());
        Debug.Log(builder.ToString());
    }

    private void OnLobbyGameCreated(Lobby lobby)
    {
        if (!IsStressRunActive() || lobby?.Controller == null)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[STRESS][GAME CREATED]");
        AppendLobby(builder, lobby);
        builder.AppendLine($"Players: {lobby.Controller.PlayerCount}");
        AppendHealth(builder, CaptureSnapshot());
        Debug.Log(builder.ToString());
    }

    private void OnLobbyClosed(Lobby lobby, LobbyCloseReason closeReason)
    {
        if (!IsStressRunActive() || lobby == null)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[STRESS][LOBBY CLOSED]");
        builder.AppendLine($"Lobby: {lobby.GetLobbyId()}");
        builder.AppendLine($"Reason: {closeReason}");
        AppendHealth(builder, CaptureSnapshot());
        Debug.Log(builder.ToString());
    }

    private void OnConnectionLost(NetworkConnectionState connectionState)
    {
        if (!IsStressRunActive())
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[STRESS][CONNECTION LOST]");
        builder.AppendLine($"State: {connectionState}");
        AppendHealth(builder, CaptureSnapshot());
        Debug.LogWarning(builder.ToString());
    }

    private void OnJoinWaveCompleted(StressFakePlayerJoinResult result)
    {
        if (!IsStressRunActive())
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[STRESS][FAKE PLAYER JOIN COMPLETE]");
        AppendJoinResult(builder, result);
        AppendHealth(builder, CaptureSnapshot());
        Debug.Log(builder.ToString());
    }

    private void OnJoinWaveClosedByLobbyStart(StressFakePlayerJoinResult result)
    {
        if (!IsStressRunActive())
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[STRESS][FAKE PLAYER JOIN CLOSED]");
        AppendJoinResult(builder, result);
        builder.AppendLine("Reason: The lobby started before the full requested join target was reached.");
        AppendHealth(builder, CaptureSnapshot());
        Debug.Log(builder.ToString());
    }

    private void OnJoinWaveFailed(StressFakePlayerJoinResult result)
    {
        if (!IsStressRunActive())
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[STRESS][FAKE PLAYER JOIN FAILED]");
        AppendJoinResult(builder, result);
        builder.AppendLine($"Reason: {(string.IsNullOrWhiteSpace(result.failureReason) ? "Unknown failure." : result.failureReason)}");
        AppendHealth(builder, CaptureSnapshot());
        Debug.LogError(builder.ToString());
    }

    private bool IsStressRunActive()
    {
        return StressSimulationCoordinator.instance != null && StressSimulationCoordinator.instance.IsRunActive && activeRuns.Count > 0;
    }

#endif

    #endregion

    #region Health

    public StressHealthSnapshot CaptureSnapshot()
    {
        StressHealthSnapshot snapshot = new StressHealthSnapshot();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        NetworkManager networkManager = NetworkManager.Singleton;
        NetworkLobbyManager manager = NetworkLobbyManager.instance;
        MultiplayerNetworkScheduler scheduler = MultiplayerNetworkScheduler.instance;
        StressFakePlayerManager fakePlayers = StressFakePlayerManager.instance;

        snapshot.activeServers = networkManager != null && networkManager.IsListening && networkManager.IsServer ? 1 : 0;
        snapshot.connectedClients = networkManager != null && networkManager.IsListening ? networkManager.ConnectedClientsIds.Count : 0;
        snapshot.syntheticPlayers = fakePlayers != null ? fakePlayers.SyntheticPlayerCount : 0;
        snapshot.pendingSyntheticPlayers = fakePlayers != null ? fakePlayers.PendingSyntheticPlayerCount : 0;
        snapshot.schedulerQueuedItems = scheduler != null ? scheduler.QueuedItemCount : 0;
        snapshot.schedulerQueuedBytes = scheduler != null ? scheduler.QueuedBytes : 0L;
        snapshot.oldestQueuedWorkSeconds = scheduler != null ? scheduler.OldestQueuedAgeSeconds : 0d;

        if (manager != null)
        {
            IReadOnlyList<Lobby> lobbies = manager.Lobbies;

            for (int i = 0; i < lobbies.Count; i++)
            {
                Lobby lobby = lobbies[i];

                if (lobby?.Controller == null || lobby.lobbyState == LobbyState.Closed)
                {
                    continue;
                }

                snapshot.totalLobbyMembers += lobby.Controller.PlayerCount;

                if (lobby.lobbyState == LobbyState.InGame)
                {
                    snapshot.activeGames++;
                }
                else
                {
                    snapshot.activeLobbies++;
                }
            }
        }
#endif

        return snapshot;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void SampleActiveRuns()
    {
        if (activeRuns.Count == 0 || Time.unscaledTimeAsDouble < nextHealthSampleTime)
        {
            return;
        }

        nextHealthSampleTime = Time.unscaledTimeAsDouble + Mathf.Max(0.05f, healthSampleIntervalSeconds);
        StressHealthSnapshot snapshot = CaptureSnapshot();

        foreach (StressHealthRun run in activeRuns.Values)
        {
            ApplySnapshotToRun(run, snapshot);
        }
    }

    private void ApplySnapshotToRun(StressHealthRun run, StressHealthSnapshot snapshot)
    {
        if (run == null || snapshot == null)
        {
            return;
        }

        run.peakActiveServers = Mathf.Max(run.peakActiveServers, snapshot.activeServers);
        run.peakActiveLobbies = Mathf.Max(run.peakActiveLobbies, snapshot.activeLobbies);
        run.peakActiveGames = Mathf.Max(run.peakActiveGames, snapshot.activeGames);
        run.peakConnectedClients = Mathf.Max(run.peakConnectedClients, snapshot.connectedClients);
        run.peakSyntheticPlayers = Mathf.Max(run.peakSyntheticPlayers, snapshot.syntheticPlayers);
        run.peakPendingSyntheticPlayers = Mathf.Max(run.peakPendingSyntheticPlayers, snapshot.pendingSyntheticPlayers);
        run.peakTotalLobbyMembers = Mathf.Max(run.peakTotalLobbyMembers, snapshot.totalLobbyMembers);
        run.peakSchedulerQueuedItems = Mathf.Max(run.peakSchedulerQueuedItems, snapshot.schedulerQueuedItems);
        run.peakSchedulerQueuedBytes = Math.Max(run.peakSchedulerQueuedBytes, snapshot.schedulerQueuedBytes);
        run.highestOldestQueuedWorkSeconds = Math.Max(run.highestOldestQueuedWorkSeconds, snapshot.oldestQueuedWorkSeconds);
    }

    private void AppendLobby(StringBuilder builder, Lobby lobby)
    {
        builder.AppendLine($"Lobby: {lobby.GetLobbyId()}");
        builder.AppendLine($"Mode: {lobby.playMode}");
        builder.AppendLine($"Game Mode: {lobby.Controller.GameModeType}");
        builder.AppendLine($"Ball Count: {lobby.Controller.BallCountType}");
    }

    private void AppendJoinResult(StringBuilder builder, StressFakePlayerJoinResult result)
    {
        builder.AppendLine($"Lobby: {result.lobbyId}");
        builder.AppendLine($"Requested: {result.requested}");
        builder.AppendLine($"Admitted: {result.admitted}");
        builder.AppendLine($"Scene Ready: {result.sceneReady}");
        builder.AppendLine($"Rejected: {result.rejected}");
        builder.AppendLine($"Not Added Due To Capacity: {result.notAdmittedDueToCapacity}");
        builder.AppendLine($"Not Admitted Before Start: {result.notAdmittedBeforeStart}");
        builder.AppendLine($"Removed Before Ready: {result.removedBeforeReady}");
    }

    private void AppendHealth(StringBuilder builder, StressHealthSnapshot snapshot, string heading = "Health:")
    {
        builder.AppendLine(heading);
        builder.AppendLine($"Active Servers: {snapshot.activeServers}");
        builder.AppendLine($"Active Lobbies: {snapshot.activeLobbies}");
        builder.AppendLine($"Active Games: {snapshot.activeGames}");
        builder.AppendLine($"Connected Clients: {snapshot.connectedClients}");
        builder.AppendLine($"Synthetic Players: {snapshot.syntheticPlayers}");
        builder.AppendLine($"Pending Synthetic Players: {snapshot.pendingSyntheticPlayers}");
        builder.AppendLine($"Total Lobby Members: {snapshot.totalLobbyMembers}");
        builder.AppendLine($"Scheduler Queued Items: {snapshot.schedulerQueuedItems}");
        builder.AppendLine($"Scheduler Queued Bytes: {snapshot.schedulerQueuedBytes}");
        builder.AppendLine($"Oldest Network Work: {snapshot.oldestQueuedWorkSeconds:F2}s");
    }

    private void AppendPeakHealth(StringBuilder builder, StressHealthRun run)
    {
        builder.AppendLine("Peak Health:");
        builder.AppendLine($"Active Servers: {run.peakActiveServers}");
        builder.AppendLine($"Active Lobbies: {run.peakActiveLobbies}");
        builder.AppendLine($"Active Games: {run.peakActiveGames}");
        builder.AppendLine($"Connected Clients: {run.peakConnectedClients}");
        builder.AppendLine($"Synthetic Players: {run.peakSyntheticPlayers}");
        builder.AppendLine($"Pending Synthetic Players: {run.peakPendingSyntheticPlayers}");
        builder.AppendLine($"Total Lobby Members: {run.peakTotalLobbyMembers}");
        builder.AppendLine($"Scheduler Queued Items: {run.peakSchedulerQueuedItems}");
        builder.AppendLine($"Scheduler Queued Bytes: {run.peakSchedulerQueuedBytes}");
        builder.AppendLine($"Highest Oldest Work Age: {run.highestOldestQueuedWorkSeconds:F2}s");
    }

#endif

    private class StressHealthRun
    {
        public readonly int id;
        public readonly string name;
        public readonly StressHealthSnapshot startingSnapshot;
        public readonly double startedTime;
        public int peakActiveServers;
        public int peakActiveLobbies;
        public int peakActiveGames;
        public int peakConnectedClients;
        public int peakSyntheticPlayers;
        public int peakPendingSyntheticPlayers;
        public int peakTotalLobbyMembers;
        public int peakSchedulerQueuedItems;
        public long peakSchedulerQueuedBytes;
        public double highestOldestQueuedWorkSeconds;

        public StressHealthRun(int id, string name, StressHealthSnapshot startingSnapshot, double startedTime)
        {
            this.id = id;
            this.name = name;
            this.startingSnapshot = startingSnapshot;
            this.startedTime = startedTime;
        }
    }

    #endregion
}
