using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbyBoardRerollStressSimulation : MonoBehaviour
{
    #region Fields

    [Header("Target Lobby")]
    [SerializeField] private MultiplayerStressTargetPlayer targetPlayer = MultiplayerStressTargetPlayer.Player1;

    [Header("Fake Player Join")]
    [SerializeField, Min(1)] private int playersToAdd = 50;
    [SerializeField, Min(1)] private int minimumJoinBatch = 1;
    [SerializeField, Min(1)] private int maximumJoinBatch = 8;
    [SerializeField, Min(0f)] private float minimumJoinDelaySeconds = 0.2f;
    [SerializeField, Min(0f)] private float maximumJoinDelaySeconds = 1.5f;
    [SerializeField, Min(0f)] private float minimumLoadDelaySeconds = 0.5f;
    [SerializeField, Min(0f)] private float maximumLoadDelaySeconds = 4f;
    [SerializeField] private bool addPlayers;

    [Header("Board Reroll Stress")]
    [SerializeField, Min(1)] private int rerollsPerPlayer = 20;
    [SerializeField, Min(0f)] private float minimumRerollDelaySeconds = 0.2f;
    [SerializeField, Min(0f)] private float maximumRerollDelaySeconds = 2f;
    [SerializeField] private bool runRerolls;

    private readonly List<RerollPlayerState> rerollPlayers = new List<RerollPlayerState>();

    private int joinOperationId;
    private int joinStressRunId;
    private int rerollStressRunId;
    private string activeJoinLobbyId = string.Empty;
    private string activeRerollLobbyId = string.Empty;
    private int totalRerollsRequested;
    private int totalRerollsCompleted;
    private int failedRerolls;
    private bool rerollRunning;

    #endregion

    #region Unity Methods

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ProcessJoinTrigger();
        ProcessRerollTrigger();
        ProcessRerolls();
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (joinOperationId > 0)
        {
            StressFakePlayerManager.instance?.CancelJoinWave(joinOperationId, "The fake-player join stress simulation was disabled before completion.");
            FinishJoinStress(false, null, "The fake-player join stress simulation was disabled before completion.");
        }

        if (rerollRunning)
        {
            FinishRerollStress(false, "The board reroll stress simulation was disabled before completion.");
        }
#endif
    }

    #endregion

    #region Fake Player Join

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void ProcessJoinTrigger()
    {
        if (joinOperationId > 0)
        {
            addPlayers = true;

            if (!StressFakePlayerManager.instance.TryGetJoinWaveResult(joinOperationId, out StressFakePlayerJoinResult result) || !result.completed)
            {
                return;
            }

            bool success = result.outcome != StressFakePlayerJoinOutcome.Failed;
            FinishJoinStress(success, result, success ? string.Empty : result.failureReason);
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

        string setupSummary = BuildJoinSetupSummary(lobby);

        if (!StressSimulationCoordinator.instance.TryBeginRun("Current Lobby Fake Player Join", setupSummary, out joinStressRunId, out _))
        {
            addPlayers = false;
            return;
        }

        StressFakePlayerJoinRequest request = new StressFakePlayerJoinRequest
        {
            lobbyId = lobby.GetLobbyId(),
            playerCount = Mathf.Max(1, playersToAdd),
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

        if (joinOperationId <= 0)
        {
            FinishJoinStress(false, null, "The fake-player join wave could not start.");
            return;
        }

        activeJoinLobbyId = lobby.GetLobbyId();
    }

    private void FinishJoinStress(bool success, StressFakePlayerJoinResult result, string failureReason)
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
            summary.AppendLine($"Removed Before Ready: {result.removedBeforeReady}");
            summary.Append($"Outcome: {result.outcome}");
        }
        else
        {
            summary.Append($"Requested: {Mathf.Max(1, playersToAdd)}");
        }

        StressSimulationCoordinator.instance?.CompleteRun(joinStressRunId, success, summary.ToString(), failureReason);

        joinOperationId = 0;
        joinStressRunId = 0;
        activeJoinLobbyId = string.Empty;
        addPlayers = false;
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

        List<StressFakePlayerRecord> fakePlayers = StressFakePlayerManager.instance.GetSceneReadyPlayersForLobby(lobby.GetLobbyId());

        if (fakePlayers.Count == 0)
        {
            runRerolls = false;
            StressHealthReporter.instance?.ReportTestNotStarted("Board Reroll Stress", StressSimulationCoordinator.instance.ActiveRunName, "The target Lobby does not contain any scene-ready fake players.");
            return;
        }

        string setupSummary = BuildRerollSetupSummary(lobby, fakePlayers.Count);

        if (!StressSimulationCoordinator.instance.TryBeginRun("Board Reroll Stress", setupSummary, out rerollStressRunId, out _))
        {
            runRerolls = false;
            return;
        }

        rerollPlayers.Clear();
        activeRerollLobbyId = lobby.GetLobbyId();
        totalRerollsRequested = fakePlayers.Count * Mathf.Max(1, rerollsPerPlayer);
        totalRerollsCompleted = 0;
        failedRerolls = 0;

        double now = Time.unscaledTimeAsDouble;

        for (int i = 0; i < fakePlayers.Count; i++)
        {
            rerollPlayers.Add(new RerollPlayerState
            {
                userId = fakePlayers[i].userId,
                remainingRerolls = Mathf.Max(1, rerollsPerPlayer),
                nextRerollTime = now + GetRandomRerollDelay()
            });
        }

        rerollRunning = true;
    }

    private void ProcessRerolls()
    {
        if (!rerollRunning)
        {
            return;
        }

        runRerolls = true;

        if (NetworkLobbyManager.instance == null || !NetworkLobbyManager.instance.TryGetStressLobby(activeRerollLobbyId, out Lobby lobby) || lobby?.Controller == null)
        {
            FinishRerollStress(false, "The target Lobby became unavailable during board reroll stress.");
            return;
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
            FinishRerollStress(failedRerolls == 0, failedRerolls == 0 ? string.Empty : "One or more fake-player board rerolls failed.");
        }
    }

    private void FinishRerollStress(bool success, string failureReason)
    {
        StringBuilder summary = new StringBuilder();
        summary.AppendLine($"Lobby: {activeRerollLobbyId}");
        summary.AppendLine($"Synthetic Players: {rerollPlayers.Count}");
        summary.AppendLine($"Rerolls Per Player: {Mathf.Max(1, rerollsPerPlayer)}");
        summary.AppendLine($"Requested Rerolls: {totalRerollsRequested}");
        summary.AppendLine($"Completed Rerolls: {totalRerollsCompleted}");
        summary.Append($"Failed Rerolls: {failedRerolls}");

        StressSimulationCoordinator.instance?.CompleteRun(rerollStressRunId, success, summary.ToString(), failureReason);

        rerollPlayers.Clear();
        activeRerollLobbyId = string.Empty;
        totalRerollsRequested = 0;
        totalRerollsCompleted = 0;
        failedRerolls = 0;
        rerollStressRunId = 0;
        rerollRunning = false;
        runRerolls = false;
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

        string userId = MultiplayerPlayModeTestContext.GetUserId((int)targetPlayer);

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

        if (lobby.playMode != MainMenuPlayMode.Online && lobby.playMode != MainMenuPlayMode.Custom)
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

    private string BuildJoinSetupSummary(Lobby lobby)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Target Player: {targetPlayer}");
        builder.AppendLine($"Lobby: {lobby.GetLobbyId()}");
        builder.AppendLine($"Players To Add: {Mathf.Max(1, playersToAdd)}");
        builder.AppendLine($"Current Players: {lobby.Controller.PlayerCount}");
        builder.AppendLine(lobby.Controller.UnlimitedPlayers ? "Lobby Capacity: Unlimited" : $"Lobby Capacity: {lobby.Controller.MaxPlayers}");
        builder.AppendLine($"Join Batch: {Mathf.Max(1, minimumJoinBatch)} - {Mathf.Max(1, maximumJoinBatch)}");
        builder.AppendLine($"Join Delay: {Mathf.Max(0f, minimumJoinDelaySeconds):F2}s - {Mathf.Max(0f, maximumJoinDelaySeconds):F2}s");
        builder.Append($"Load Delay: {Mathf.Max(0f, minimumLoadDelaySeconds):F2}s - {Mathf.Max(0f, maximumLoadDelaySeconds):F2}s");
        return builder.ToString();
    }

    private string BuildRerollSetupSummary(Lobby lobby, int syntheticPlayerCount)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Target Player: {targetPlayer}");
        builder.AppendLine($"Lobby: {lobby.GetLobbyId()}");
        builder.AppendLine($"Synthetic Players: {syntheticPlayerCount}");
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
