using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbyBoardRerollStressSimulation : MonoBehaviour
{
    #region Fields

    [Header("Simulation Controls")]
    [Tooltip("Reroll existing bots and synthetic players. If none are ready, add up to five synthetic players first.")]
    [SerializeField] private bool runRerolls;
    [Tooltip("Stop rerolling, including its automatic player setup.")]
    [SerializeField] private bool stopRerolling;
    [Tooltip("Add synthetic players using the Fake Player Join settings.")]
    [SerializeField] private bool addPlayers;
    [Tooltip("Stop the manual Add Players operation.")]
    [SerializeField] private bool stopAddingPlayers;
    [Tooltip("Stop whichever operation this simulation is running.")]
    [SerializeField] private bool stopSimulation;

    [Space]
    [Header("Target Lobby")]
    [SerializeField] private MultiplayerStressTargetPlayer targetPlayer = MultiplayerStressTargetPlayer.Player1;

    [Space]
    [Header("Fake Player Join")]
    [SerializeField] private bool useMaxLobbySize;
    [SerializeField, Min(1)] private int playersToAdd = 50;
    [SerializeField, Min(1)] private int minimumJoinBatch = 1;
    [SerializeField, Min(1)] private int maximumJoinBatch = 8;
    [SerializeField, Min(0f)] private float minimumJoinDelaySeconds = 0.2f;
    [SerializeField, Min(0f)] private float maximumJoinDelaySeconds = 1.5f;
    [SerializeField, Min(0f)] private float minimumLoadDelaySeconds = 0.5f;
    [SerializeField, Min(0f)] private float maximumLoadDelaySeconds = 4f;

    [Space]
    [Header("Board Reroll Stress")]
    [SerializeField, Min(1)] private int rerollsPerPlayer = 20;
    [SerializeField, Min(0f)] private float minimumRerollDelaySeconds = 0.2f;
    [SerializeField, Min(0f)] private float maximumRerollDelaySeconds = 2f;

    private readonly List<RerollPlayerState> rerollPlayers = new List<RerollPlayerState>();

    private int joinOperationId;
    private int joinStressRunId;
    private int activeJoinRequestedPlayers;
    private int rerollStressRunId;
    private int rerollJoinOperationId;
    private string activeJoinLobbyId = string.Empty;
    private string activeRerollLobbyId = string.Empty;
    private int totalRerollsRequested;
    private int totalRerollsCompleted;
    private int failedRerolls;
    private bool rerollRunning;

    [SerializeField, HideInInspector] private int inspectorDefaultsVersion;

    private const int CurrentInspectorDefaultsVersion = 1;
    private const int AutomaticRerollPlayerCount = 5;

    #endregion

    #region Unity Methods

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (inspectorDefaultsVersion >= CurrentInspectorDefaultsVersion)
        {
            return;
        }

        useMaxLobbySize = false;
        inspectorDefaultsVersion = CurrentInspectorDefaultsVersion;
    }
#endif

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!CanOperate())
        {
            CancelSimulation();
            return;
        }

        if (StressSimulationCoordinator.instance.IsRunActive)
        {
            if (joinOperationId == 0)
            {
                addPlayers = false;
            }
            if (!rerollRunning)
            {
                runRerolls = false;
            }
        }

        ProcessStopSimulation();
        ProcessJoinTrigger();
        ProcessRerollTrigger();
        ProcessRerolls();
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CancelSimulation();
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool CanOperate()
    {
        return GameSceneManager.instance != null &&
               GameSceneManager.instance.CurrentSceneType == GameSceneType.Lobby &&
               GameSceneManager.instance.IsActiveScene(GameSceneType.Lobby) &&
               !GameSceneManager.instance.IsLoadingScene &&
               NetworkBootstrap.instance != null && NetworkBootstrap.instance.IsReady &&
               NetworkBootstrap.instance.IsConnected && NetworkBootstrap.instance.IsAuthority &&
               NetworkLobbyManager.instance != null && NetworkLobbyManager.instance.IsReady &&
               StressFakePlayerManager.instance != null && StressSimulationCoordinator.instance != null &&
               StressHealthReporter.instance != null &&
               IsLobbyAvailable(activeJoinLobbyId) && IsLobbyAvailable(activeRerollLobbyId);
    }

    private bool IsLobbyAvailable(string _lobbyId)
    {
        return string.IsNullOrWhiteSpace(_lobbyId) ||
               (NetworkLobbyManager.instance.TryGetStressLobby(_lobbyId, out Lobby lobby) &&
                lobby != null && lobby.Controller != null && lobby.lobbyState != LobbyState.InGame);
    }

    private void CancelSimulation()
    {
        if (joinOperationId > 0)
        {
            StressFakePlayerJoinResult result = null;
            StressFakePlayerManager.instance?.CancelJoinWave(joinOperationId, "The fake-player join stress simulation was disabled before completion.");

            if (StressFakePlayerManager.instance != null)
            {
                StressFakePlayerManager.instance.TryGetJoinWaveResult(joinOperationId, out result);
            }

            FinishJoinStress(StressTestResult.Cancelled, result, "The fake-player join stress simulation was disabled before completion.");
        }

        if (rerollRunning)
        {
            FinishRerollStress(StressTestResult.Cancelled, "The board reroll stress simulation was disabled before completion.");
        }
        addPlayers = false;
        stopAddingPlayers = false;
        runRerolls = false;
        stopRerolling = false;
        stopSimulation = false;
    }
#endif

    #endregion

    #region Fake Player Join

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void ProcessJoinTrigger()
    {
        if (joinOperationId > 0)
        {
            addPlayers = true;

            if (stopAddingPlayers)
            {
                RequestJoinStop("User stopped adding fake players.");
                stopAddingPlayers = false;
            }

            if (StressSimulationCoordinator.instance != null && StressSimulationCoordinator.instance.IsStopRequestedFor(joinStressRunId))
            {
                StressFakePlayerManager.instance?.CancelJoinWave(joinOperationId, StressSimulationCoordinator.instance.StopReason);
            }

            if (!StressFakePlayerManager.instance.TryGetJoinWaveResult(joinOperationId, out StressFakePlayerJoinResult result) || !result.completed)
            {
                return;
            }

            if (result.outcome == StressFakePlayerJoinOutcome.Cancelled)
            {
                FinishJoinStress(StressTestResult.Cancelled, result, result.failureReason);
                return;
            }

            bool success = result.outcome != StressFakePlayerJoinOutcome.Failed;
            FinishJoinStress(success ? StressTestResult.Passed : StressTestResult.Failed, result, success ? string.Empty : result.failureReason);
            return;
        }

        if (!addPlayers)
        {
            return;
        }

        if (!TryResolveTargetLobby(out Lobby lobby, out string failureReason))
        {
            addPlayers = false;
            StressHealthReporter.instance?.ReportTestNotStarted("Current Lobby Fake Player Join", StressSimulationCoordinator.instance?.ActiveRunName, failureReason);
            return;
        }

        if (StressSimulationCoordinator.instance == null || StressHealthReporter.instance == null)
        {
            addPlayers = false;
            StressHealthReporter.instance?.ReportFailure("FAKE PLAYER JOIN FAILED", "The shared stress simulation services are not ready.");
            return;
        }

        int requestedPlayerCount = GetRequestedJoinPlayerCount(lobby);

        if (requestedPlayerCount <= 0)
        {
            addPlayers = false;
            StressHealthReporter.instance?.ReportTestNotStarted("Current Lobby Fake Player Join", StressSimulationCoordinator.instance?.ActiveRunName, "The target Lobby is already full.");
            return;
        }

        string setupSummary = BuildJoinSetupSummary(lobby, requestedPlayerCount);

        if (!StressSimulationCoordinator.instance.TryBeginRun("Current Lobby Fake Player Join", setupSummary, out joinStressRunId, out _))
        {
            addPlayers = false;
            return;
        }

        activeJoinRequestedPlayers = requestedPlayerCount;
        stopAddingPlayers = false;
        stopSimulation = false;

        StressFakePlayerJoinRequest request = new StressFakePlayerJoinRequest
        {
            lobbyId = lobby.GetLobbyId(),
            playerCount = requestedPlayerCount,
            minimumJoinBatch = minimumJoinBatch,
            maximumJoinBatch = maximumJoinBatch,
            minimumJoinDelaySeconds = minimumJoinDelaySeconds,
            maximumJoinDelaySeconds = maximumJoinDelaySeconds,
            minimumLoadDelaySeconds = minimumLoadDelaySeconds,
            maximumLoadDelaySeconds = maximumLoadDelaySeconds,
            firstPlayerIsHost = false,
            limitToAvailableLobbyCapacity = true
        };

        joinOperationId = StressFakePlayerManager.instance.StartJoinWave(request);
        StressSimulationCoordinator.instance.TrackJoinWave(joinStressRunId, joinOperationId);

        if (joinOperationId <= 0)
        {
            FinishJoinStress(StressTestResult.Failed, null, "The fake-player join wave could not start.");
            return;
        }

        activeJoinLobbyId = lobby.GetLobbyId();
    }

    private void FinishJoinStress(StressTestResult resultType, StressFakePlayerJoinResult result, string reason)
    {
        StringBuilder summary = new StringBuilder();
        summary.AppendLine($"Lobby: {activeJoinLobbyId}");

        if (result != null)
        {
            summary.AppendLine($"Requested: {result.requested}");
            summary.AppendLine($"Admitted: {result.admitted}");
            summary.AppendLine($"Scene Ready: {result.sceneReady}");
            summary.AppendLine($"Not Added Due To Capacity: {result.notAdmittedDueToCapacity}");
            summary.AppendLine($"Not Admitted Before Start: {result.notAdmittedBeforeStart}");
            summary.AppendLine($"Not Added Due To Cancellation: {result.notAdmittedDueToCancellation}");
            summary.AppendLine($"Removed Before Ready: {result.removedBeforeReady}");
            summary.Append($"Outcome: {result.outcome}");
        }
        else
        {
            summary.Append($"Requested: {Mathf.Max(1, activeJoinRequestedPlayers)}");
        }

        if (resultType == StressTestResult.Cancelled)
        {
            StressSimulationCoordinator.instance?.CancelRun(joinStressRunId, summary.ToString(), reason);
        }
        else
        {
            StressSimulationCoordinator.instance?.CompleteRun(joinStressRunId, resultType == StressTestResult.Passed, summary.ToString(), reason);
        }

        joinOperationId = 0;
        joinStressRunId = 0;
        activeJoinRequestedPlayers = 0;
        activeJoinLobbyId = string.Empty;
        addPlayers = false;
        stopAddingPlayers = false;
        stopSimulation = false;
    }

#endif

    #endregion

    #region Board Rerolls

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void ProcessRerollTrigger()
    {
        if (rerollRunning || !runRerolls)
        {
            return;
        }

        runRerolls = false;
        if (StressSimulationCoordinator.instance != null && StressSimulationCoordinator.instance.IsRunActive)
        {
            return;
        }

        if (!TryResolveTargetLobby(out Lobby lobby, out string failureReason))
        {
            runRerolls = false;
            StressHealthReporter.instance?.ReportTestNotStarted("Board Reroll Stress", StressSimulationCoordinator.instance?.ActiveRunName, failureReason);
            return;
        }

        if (StressSimulationCoordinator.instance == null || StressHealthReporter.instance == null)
        {
            runRerolls = false;
            StressHealthReporter.instance?.ReportFailure("BOARD REROLL FAILED", "The shared stress simulation services are not ready.");
            return;
        }

        if (lobby.lobbyState != LobbyState.Open)
        {
            StressHealthReporter.instance.ReportTestNotStarted("Board Reroll Stress", string.Empty, "The target Lobby must be open to reroll boards.");
            return;
        }

        CollectRerollPlayers(lobby);
        int playersToJoin = rerollPlayers.Count == 0
            ? Mathf.Min(AutomaticRerollPlayerCount, Mathf.Max(0, lobby.Controller.MaxPlayer - lobby.Controller.ReservedPlayerCount))
            : 0;

        if (rerollPlayers.Count == 0 && playersToJoin == 0)
        {
            StressHealthReporter.instance.ReportTestNotStarted("Board Reroll Stress", string.Empty, "The target Lobby has no eligible bots or synthetic players and no room to add any.");
            return;
        }

        string setupSummary = BuildRerollSetupSummary(lobby, rerollPlayers.Count);
        if (playersToJoin > 0)
        {
            setupSummary += $"\nAutomatic Synthetic Players To Add: {playersToJoin}";
        }

        if (!StressSimulationCoordinator.instance.TryBeginRun("Board Reroll Stress", setupSummary, out rerollStressRunId, out _))
        {
            runRerolls = false;
            return;
        }

        stopRerolling = false;
        stopSimulation = false;
        activeRerollLobbyId = lobby.GetLobbyId();
        totalRerollsRequested = rerollPlayers.Count * Mathf.Max(1, rerollsPerPlayer);
        totalRerollsCompleted = 0;
        failedRerolls = 0;
        rerollRunning = true;
        runRerolls = true;

        if (playersToJoin > 0)
        {
            rerollJoinOperationId = StressFakePlayerManager.instance.StartJoinWave(new StressFakePlayerJoinRequest
            {
                lobbyId = activeRerollLobbyId,
                playerCount = playersToJoin,
                minimumJoinBatch = minimumJoinBatch,
                maximumJoinBatch = maximumJoinBatch,
                minimumJoinDelaySeconds = minimumJoinDelaySeconds,
                maximumJoinDelaySeconds = maximumJoinDelaySeconds,
                minimumLoadDelaySeconds = minimumLoadDelaySeconds,
                maximumLoadDelaySeconds = maximumLoadDelaySeconds,
                firstPlayerIsHost = false,
                limitToAvailableLobbyCapacity = true
            });
            StressSimulationCoordinator.instance.TrackJoinWave(rerollStressRunId, rerollJoinOperationId);

            if (rerollJoinOperationId <= 0)
            {
                FinishRerollStress(StressTestResult.Failed, "The automatic fake-player join wave could not start.");
            }
        }
    }

    private void CollectRerollPlayers(Lobby _lobby)
    {
        rerollPlayers.Clear();
        IReadOnlyList<LobbyPlayerData> players = _lobby.Controller.Players;
        double now = Time.unscaledTimeAsDouble;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData player = players[i];
            if (player == null || !player.HasValidUser || !player.isLobbySceneReady ||
                (player.userData.userTag != UserTag.Bot &&
                 !StressFakePlayerManager.instance.IsSyntheticPlayer(player.userData.userId)))
            {
                continue;
            }

            rerollPlayers.Add(new RerollPlayerState
            {
                userId = player.userData.userId,
                remainingRerolls = Mathf.Max(1, rerollsPerPlayer),
                nextRerollTime = now + GetRandomRerollDelay()
            });
        }
    }

    private void ProcessRerolls()
    {
        if (!rerollRunning)
        {
            return;
        }

        runRerolls = true;

        if (stopRerolling)
        {
            RequestRerollStop("User stopped board rerolling.");
            stopRerolling = false;
        }

        if (StressSimulationCoordinator.instance != null && StressSimulationCoordinator.instance.IsStopRequestedFor(rerollStressRunId))
        {
            FinishRerollStress(StressTestResult.Cancelled, StressSimulationCoordinator.instance.StopReason);
            return;
        }

        if (NetworkLobbyManager.instance == null || !NetworkLobbyManager.instance.TryGetStressLobby(activeRerollLobbyId, out Lobby lobby) || lobby?.Controller == null)
        {
            FinishRerollStress(StressTestResult.Failed, "The target Lobby became unavailable during board reroll stress.");
            return;
        }

        if (lobby.lobbyState != LobbyState.Open)
        {
            FinishRerollStress(StressTestResult.Cancelled, "The target Lobby is no longer open for board rerolls.");
            return;
        }

        if (rerollJoinOperationId > 0)
        {
            if (!StressFakePlayerManager.instance.TryGetJoinWaveResult(rerollJoinOperationId, out StressFakePlayerJoinResult result))
            {
                FinishRerollStress(StressTestResult.Failed, "The automatic join result is no longer available.");
                return;
            }
            if (!result.completed)
            {
                return;
            }

            rerollJoinOperationId = 0;
            if (result.outcome == StressFakePlayerJoinOutcome.Cancelled || result.outcome == StressFakePlayerJoinOutcome.Failed)
            {
                FinishRerollStress(result.outcome == StressFakePlayerJoinOutcome.Cancelled ? StressTestResult.Cancelled : StressTestResult.Failed, result.failureReason);
                return;
            }

            CollectRerollPlayers(lobby);
            if (rerollPlayers.Count == 0)
            {
                FinishRerollStress(StressTestResult.Failed, "No bots or synthetic players became ready for board rerolls.");
                return;
            }
            totalRerollsRequested = rerollPlayers.Count * Mathf.Max(1, rerollsPerPlayer);
        }

        double now = Time.unscaledTimeAsDouble;
        bool hasRemainingWork = false;

        for (int i = 0; i < rerollPlayers.Count; i++)
        {
            RerollPlayerState playerState = rerollPlayers[i];

            if (playerState.remainingRerolls <= 0)
            {
                continue;
            }

            hasRemainingWork = true;

            if (now < playerState.nextRerollTime)
            {
                continue;
            }

            if (NetworkLobbyManager.instance.TryRerollStressPlayerBoard(activeRerollLobbyId, playerState.userId, out _))
            {
                totalRerollsCompleted++;
            }
            else
            {
                failedRerolls++;
            }

            playerState.remainingRerolls--;
            playerState.nextRerollTime = now + GetRandomRerollDelay();
        }

        if (!hasRemainingWork)
        {
            FinishRerollStress(failedRerolls == 0 ? StressTestResult.Passed : StressTestResult.Failed, failedRerolls == 0 ? string.Empty : "One or more bot or synthetic-player board rerolls failed.");
        }
    }

    private void FinishRerollStress(StressTestResult resultType, string reason)
    {
        int cancelledRerolls = Mathf.Max(0, totalRerollsRequested - totalRerollsCompleted - failedRerolls);

        StringBuilder summary = new StringBuilder();
        summary.AppendLine($"Lobby: {activeRerollLobbyId}");
        summary.AppendLine($"Bots And Synthetic Players: {rerollPlayers.Count}");
        summary.AppendLine($"Rerolls Per Player: {Mathf.Max(1, rerollsPerPlayer)}");
        summary.AppendLine($"Requested Rerolls: {totalRerollsRequested}");
        summary.AppendLine($"Completed Rerolls: {totalRerollsCompleted}");
        summary.AppendLine($"Failed Rerolls: {failedRerolls}");
        summary.Append($"Cancelled Rerolls: {(resultType == StressTestResult.Cancelled ? cancelledRerolls : 0)}");

        if (resultType == StressTestResult.Cancelled)
        {
            StressSimulationCoordinator.instance?.CancelRun(rerollStressRunId, summary.ToString(), reason);
        }
        else
        {
            StressSimulationCoordinator.instance?.CompleteRun(rerollStressRunId, resultType == StressTestResult.Passed, summary.ToString(), reason);
        }

        rerollPlayers.Clear();
        activeRerollLobbyId = string.Empty;
        totalRerollsRequested = 0;
        totalRerollsCompleted = 0;
        failedRerolls = 0;
        rerollStressRunId = 0;
        rerollJoinOperationId = 0;
        rerollRunning = false;
        runRerolls = false;
        stopRerolling = false;
        stopSimulation = false;
    }

#endif

    #endregion

    #region Run Control

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void ProcessStopSimulation()
    {
        if (!stopSimulation)
        {
            return;
        }

        if (joinOperationId > 0)
        {
            RequestJoinStop("User stopped the simulation.");
            stopSimulation = false;
            return;
        }

        if (rerollRunning)
        {
            RequestRerollStop("User stopped the simulation.");
            stopSimulation = false;
            return;
        }

        stopSimulation = false;
    }

    private void RequestJoinStop(string reason)
    {
        if (joinOperationId <= 0)
        {
            return;
        }

        StressSimulationCoordinator.instance?.RequestStopRun(joinStressRunId, reason);
        StressFakePlayerManager.instance?.CancelJoinWave(joinOperationId, reason);
    }

    private void RequestRerollStop(string reason)
    {
        if (!rerollRunning)
        {
            return;
        }

        StressSimulationCoordinator.instance?.RequestStopRun(rerollStressRunId, reason);
    }

#endif

    #endregion

    #region Target Resolution

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private bool TryResolveTargetLobby(out Lobby lobby, out string failureReason)
    {
        lobby = null;
        failureReason = string.Empty;

        if (NetworkBootstrap.instance == null || !NetworkBootstrap.instance.IsAuthority)
        {
            failureReason = "Run this simulation from the authority/host player.";
            return false;
        }

        if (NetworkLobbyManager.instance == null || !NetworkLobbyManager.instance.IsReady || StressFakePlayerManager.instance == null)
        {
            failureReason = "The stress simulation services are not ready.";
            return false;
        }

        string userId = MultiplayerPlayModeTestContext.IsActive
            ? MultiplayerPlayModeTestContext.GetUserId((int)targetPlayer)
            : targetPlayer == MultiplayerStressTargetPlayer.Player1 && UserManager.instance != null && UserManager.instance.HasUser
                ? UserManager.instance.UserId
                : string.Empty;

        if (string.IsNullOrWhiteSpace(userId))
        {
            failureReason = $"The MPPM identity for {targetPlayer} could not be resolved.";
            return false;
        }

        if (!NetworkLobbyManager.instance.TryGetStressLobbyForUser(userId, out lobby) || lobby?.Controller == null)
        {
            failureReason = $"{targetPlayer} is not currently in a network Lobby.";
            return false;
        }

        if (lobby.lobbyState == LobbyState.InGame ||
            (lobby.playMode != MainMenuPlayMode.Online && lobby.playMode != MainMenuPlayMode.Custom))
        {
            failureReason = $"{targetPlayer} must be in an Online or Custom Lobby.";
            lobby = null;
            return false;
        }

        return true;
    }

#endif

    #endregion

    #region Helpers

    private int GetRequestedJoinPlayerCount(Lobby lobby)
    {
        if (lobby?.Controller == null)
        {
            return 0;
        }

        if (!useMaxLobbySize)
        {
            return Mathf.Max(1, playersToAdd);
        }

        return Mathf.Max(0, lobby.Controller.MaxPlayer - lobby.Controller.PlayerCount);
    }

    private string BuildJoinSetupSummary(Lobby lobby, int requestedPlayerCount)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Target Player: {targetPlayer}");
        builder.AppendLine($"Lobby: {lobby.GetLobbyId()}");
        builder.AppendLine($"Use Max Lobby Size: {useMaxLobbySize}");
        builder.AppendLine($"Players To Add: {requestedPlayerCount}");
        builder.AppendLine($"Current Players: {lobby.Controller.PlayerCount}");
        builder.AppendLine(lobby.Controller.MaxPlayers ? "Lobby Capacity: Max" : $"Lobby Capacity: {lobby.Controller.MaxPlayer}");

        builder.AppendLine($"Join Batch: {Mathf.Max(1, minimumJoinBatch)} - {Mathf.Max(1, maximumJoinBatch)}");
        builder.AppendLine($"Join Delay: {Mathf.Max(0f, minimumJoinDelaySeconds):F2}s - {Mathf.Max(0f, maximumJoinDelaySeconds):F2}s");
        builder.Append($"Load Delay: {Mathf.Max(0f, minimumLoadDelaySeconds):F2}s - {Mathf.Max(0f, maximumLoadDelaySeconds):F2}s");
        return builder.ToString();
    }

    private string BuildRerollSetupSummary(Lobby lobby, int _eligiblePlayerCount)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Target Player: {targetPlayer}");
        builder.AppendLine($"Lobby: {lobby.GetLobbyId()}");
        builder.AppendLine($"Bots And Synthetic Players: {_eligiblePlayerCount}");
        builder.AppendLine($"Rerolls Per Player: {Mathf.Max(1, rerollsPerPlayer)}");
        builder.Append($"Reroll Delay: {Mathf.Max(0f, minimumRerollDelaySeconds):F2}s - {Mathf.Max(0f, maximumRerollDelaySeconds):F2}s");
        return builder.ToString();
    }

    private float GetRandomRerollDelay()
    {
        float minimum = Mathf.Max(0f, Mathf.Min(minimumRerollDelaySeconds, maximumRerollDelaySeconds));
        float maximum = Mathf.Max(minimum, Mathf.Max(minimumRerollDelaySeconds, maximumRerollDelaySeconds));
        return Mathf.Approximately(minimum, maximum) ? minimum : Random.Range(minimum, maximum);
    }

    private class RerollPlayerState
    {
        public string userId = string.Empty;
        public int remainingRerolls;
        public double nextRerollTime;
    }

    #endregion
}
