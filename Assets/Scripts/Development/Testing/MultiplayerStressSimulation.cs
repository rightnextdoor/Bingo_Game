using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum MultiplayerStressLobbyMode
{
    Preset,
    Random
}

[Serializable]
public class MultiplayerStressLobbyPreset
{
    public MainMenuPlayMode playMode = MainMenuPlayMode.Online;
    public BingoGameModeType gameModeType = BingoGameModeType.Traditional;
    public BingoBallCountType ballCountType = BingoBallCountType.Ball75;
    [Min(1)] public int targetPlayers = 50;
    public bool useFreeCell = true;
}

[DisallowMultipleComponent]
public class MultiplayerStressSimulation : MonoBehaviour
{
    #region Fields

    [Header("Stress Setup")]
    [SerializeField] private MultiplayerStressLobbyMode lobbyMode = MultiplayerStressLobbyMode.Random;
    [SerializeField, Min(1)] private int lobbyCount = 4;
    [SerializeField] private bool useMaxLobbySize;
    [SerializeField] private List<MultiplayerStressLobbyPreset> presetLobbies = new List<MultiplayerStressLobbyPreset>();

    [Header("Random Lobby Setup")]
    [SerializeField, Min(1)] private int minimumPlayersPerLobby = 25;
    [SerializeField, Min(1)] private int maximumPlayersPerLobby = 100;
    [SerializeField] private bool includeOnlineLobbies = true;
    [SerializeField] private bool includeCustomLobbies = true;

    [Header("Fake Player Join Wave")]
    [SerializeField, Min(1)] private int minimumJoinBatch = 1;
    [SerializeField, Min(1)] private int maximumJoinBatch = 8;
    [SerializeField, Min(0f)] private float minimumJoinDelaySeconds = 0.2f;
    [SerializeField, Min(0f)] private float maximumJoinDelaySeconds = 1.5f;
    [SerializeField, Min(0f)] private float minimumLoadDelaySeconds = 0.5f;
    [SerializeField, Min(0f)] private float maximumLoadDelaySeconds = 4f;

    [Header("Run")]
    [SerializeField, Min(5f)] private float maximumRunSeconds = 420f;
    [SerializeField] private bool runStress;
    [SerializeField] private bool stopSimulation;

    private readonly List<ActiveStressLobby> activeLobbies = new List<ActiveStressLobby>();

    private int stressRunId;
    private int requestedLobbyCount;
    private double runStartedTime;
    private bool isRunning;

    [SerializeField, HideInInspector] private int inspectorDefaultsVersion;

    private const int CurrentInspectorDefaultsVersion = 1;

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
        if (!isRunning)
        {
            if (stopSimulation)
            {
                stopSimulation = false;
            }

            if (runStress)
            {
                StartStressRun();
            }

            return;
        }

        ProcessStopSimulation();

        if (isRunning)
        {
            MonitorStressRun();
        }
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (isRunning)
        {
            FinishStressRun(StressTestResult.Cancelled, "The stress simulation was disabled before it completed.");
        }
#endif
    }

    #endregion

    #region Stress Run

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void StartStressRun()
    {
        if (!CanRun(out string failureReason))
        {
            runStress = false;
            StressHealthReporter.instance?.ReportFailure("MULTI LOBBY FAILED", failureReason);
            return;
        }

        List<MultiplayerStressLobbyPreset> definitions = BuildLobbyDefinitions();
        string setupSummary = BuildRunSetupSummary(definitions);

        if (!StressSimulationCoordinator.instance.TryBeginRun("Multi Lobby Stress", setupSummary, out stressRunId, out _))
        {
            runStress = false;
            return;
        }

        activeLobbies.Clear();
        requestedLobbyCount = definitions.Count;
        runStartedTime = Time.unscaledTimeAsDouble;
        stopSimulation = false;
        isRunning = true;

        for (int i = 0; i < definitions.Count; i++)
        {
            if (!TryCreateStressLobby(definitions[i], i + 1, out ActiveStressLobby activeLobby, out failureReason))
            {
                FinishStressRun(StressTestResult.Failed, failureReason);
                return;
            }

            activeLobbies.Add(activeLobby);
        }
    }

    private void MonitorStressRun()
    {
        runStress = true;

        if (StressSimulationCoordinator.instance != null && StressSimulationCoordinator.instance.IsStopRequestedFor(stressRunId))
        {
            FinishStressRun(StressTestResult.Cancelled, StressSimulationCoordinator.instance.StopReason);
            return;
        }

        if (Time.unscaledTimeAsDouble - runStartedTime >= maximumRunSeconds)
        {
            FinishStressRun(StressTestResult.Failed, "The multi-lobby stress run exceeded its maximum run time.");
            return;
        }

        bool allGamesCreated = activeLobbies.Count > 0;

        for (int i = 0; i < activeLobbies.Count; i++)
        {
            ActiveStressLobby activeLobby = activeLobbies[i];

            if (!NetworkLobbyManager.instance.TryGetStressLobby(activeLobby.lobbyId, out Lobby lobby) || lobby?.Controller == null)
            {
                FinishStressRun(StressTestResult.Failed, $"Lobby {activeLobby.lobbyId} was removed before creating its game.");
                return;
            }

            if (StressFakePlayerManager.instance.TryGetJoinWaveResult(activeLobby.joinOperationId, out StressFakePlayerJoinResult joinResult) && joinResult.completed)
            {
                if (joinResult.outcome == StressFakePlayerJoinOutcome.Failed)
                {
                    FinishStressRun(StressTestResult.Failed, $"Lobby {activeLobby.lobbyId} fake-player join wave failed: {joinResult.failureReason}");
                    return;
                }

                if (lobby.playMode == MainMenuPlayMode.Custom && !activeLobby.customCountdownRequested && lobby.lobbyState == LobbyState.Open)
                {
                    if (!TryBeginCustomCountdown(activeLobby, lobby, out string countdownFailure))
                    {
                        FinishStressRun(StressTestResult.Failed, countdownFailure);
                        return;
                    }
                }
            }

            if (lobby.lobbyState != LobbyState.InGame)
            {
                allGamesCreated = false;
            }
        }

        if (allGamesCreated)
        {
            FinishStressRun(StressTestResult.Passed, string.Empty);
        }
    }

    private void FinishStressRun(StressTestResult resultType, string reason)
    {
        if (resultType != StressTestResult.Passed)
        {
            CancelActiveJoinWaves(reason);
        }

        StringBuilder summary = new StringBuilder();
        summary.AppendLine($"Lobbies Requested: {requestedLobbyCount}");
        summary.AppendLine($"Lobbies Created: {activeLobbies.Count}");

        int gamesCreated = 0;
        int requestedPlayers = 0;
        int admittedPlayers = 0;
        int readyPlayers = 0;
        int notAdmittedBeforeStart = 0;
        int notAdmittedDueToCancellation = 0;
        int lobbiesStartedBeforeTarget = 0;

        for (int i = 0; i < activeLobbies.Count; i++)
        {
            ActiveStressLobby activeLobby = activeLobbies[i];
            requestedPlayers += activeLobby.targetPlayers;

            if (StressFakePlayerManager.instance != null && StressFakePlayerManager.instance.TryGetJoinWaveResult(activeLobby.joinOperationId, out StressFakePlayerJoinResult joinResult))
            {
                admittedPlayers += joinResult.admitted;
                notAdmittedBeforeStart += joinResult.notAdmittedBeforeStart + joinResult.removedBeforeReady;

                int cancelledPlayers = joinResult.notAdmittedDueToCancellation;

                if (resultType == StressTestResult.Cancelled && !joinResult.completed)
                {
                    cancelledPlayers = Mathf.Max(cancelledPlayers, activeLobby.targetPlayers - joinResult.admitted - joinResult.rejected - joinResult.notAdmittedDueToCapacity - joinResult.notAdmittedBeforeStart);
                }

                notAdmittedDueToCancellation += Mathf.Max(0, cancelledPlayers);

                if (joinResult.outcome == StressFakePlayerJoinOutcome.LobbyStarted)
                {
                    lobbiesStartedBeforeTarget++;
                }
            }

            if (NetworkLobbyManager.instance != null && NetworkLobbyManager.instance.TryGetStressLobby(activeLobby.lobbyId, out Lobby lobby) && lobby?.Controller != null)
            {
                readyPlayers += lobby.Controller.SceneReadyPlayerCount;

                if (lobby.lobbyState == LobbyState.InGame)
                {
                    gamesCreated++;
                }
            }
        }

        summary.AppendLine($"Games Created: {gamesCreated}");
        summary.AppendLine($"Synthetic Players Requested: {requestedPlayers}");
        summary.AppendLine($"Synthetic Players Admitted: {admittedPlayers}");
        summary.AppendLine($"Scene Ready Players At Finish: {readyPlayers}");
        summary.AppendLine($"Lobbies Started Before Join Target: {lobbiesStartedBeforeTarget}");
        summary.AppendLine($"Players Not Ready/Admitted Before Start: {notAdmittedBeforeStart}");
        summary.Append($"Players Not Added Due To Cancellation: {notAdmittedDueToCancellation}");

        if (resultType == StressTestResult.Cancelled)
        {
            StressSimulationCoordinator.instance?.CancelRun(stressRunId, summary.ToString(), reason);
        }
        else
        {
            StressSimulationCoordinator.instance?.CompleteRun(stressRunId, resultType == StressTestResult.Passed, summary.ToString(), reason);
        }

        activeLobbies.Clear();
        stressRunId = 0;
        requestedLobbyCount = 0;
        isRunning = false;
        runStress = false;
        stopSimulation = false;
    }

    private void ProcessStopSimulation()
    {
        if (!stopSimulation || !isRunning)
        {
            return;
        }

        stopSimulation = false;
        StressSimulationCoordinator.instance?.RequestStopRun(stressRunId, "User stopped the simulation.");
    }

    private void CancelActiveJoinWaves(string reason)
    {
        if (StressFakePlayerManager.instance == null)
        {
            return;
        }

        string resolvedReason = string.IsNullOrWhiteSpace(reason) ? "The multi-lobby stress simulation stopped before completion." : reason;

        for (int i = 0; i < activeLobbies.Count; i++)
        {
            ActiveStressLobby activeLobby = activeLobbies[i];

            if (activeLobby.joinOperationId > 0 && StressFakePlayerManager.instance.IsJoinWaveRunning(activeLobby.joinOperationId))
            {
                StressFakePlayerManager.instance.CancelJoinWave(activeLobby.joinOperationId, resolvedReason);
            }
        }
    }

#endif

    #endregion

    #region Lobby Creation

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private bool TryCreateStressLobby(MultiplayerStressLobbyPreset definition, int index, out ActiveStressLobby activeLobby, out string failureReason)
    {
        activeLobby = null;
        failureReason = string.Empty;

        LobbySetupData setupData = BuildLobbySetupData(definition, index);

        if (!NetworkLobbyManager.instance.TryCreateStressLobby(setupData, out Lobby lobby, out failureReason) || lobby?.Controller == null)
        {
            return false;
        }

        int requestedPlayerCount = GetRequestedJoinPlayerCount(lobby, definition.targetPlayers);

        if (requestedPlayerCount <= 0)
        {
            failureReason = $"Lobby {lobby.GetLobbyId()} is already full before its fake-player join wave could start.";
            return false;
        }

        StressFakePlayerJoinRequest joinRequest = BuildJoinRequest(lobby.GetLobbyId(), requestedPlayerCount, definition.playMode == MainMenuPlayMode.Custom);
        int operationId = StressFakePlayerManager.instance.StartJoinWave(joinRequest);

        if (operationId <= 0)
        {
            failureReason = $"The fake-player join wave could not start for Lobby {lobby.GetLobbyId()}.";
            return false;
        }

        activeLobby = new ActiveStressLobby
        {
            lobbyId = lobby.GetLobbyId(),
            targetPlayers = requestedPlayerCount,
            joinOperationId = operationId
        };

        return true;
    }

    private LobbySetupData BuildLobbySetupData(MultiplayerStressLobbyPreset definition, int index)
    {
        int maxPlayerCount = LobbySettings.instance != null ? LobbySettings.instance.MaxPlayerCount : Mathf.Max(definition.targetPlayers, 1000);
        int maximumPlayers = Mathf.Max(definition.targetPlayers, LobbySettings.instance != null ? LobbySettings.instance.MinimumPlayers : 1);
        bool useMax = maximumPlayers >= maxPlayerCount;

        LobbySetupData setupData = new LobbySetupData
        {
            playMode = definition.playMode
        };

        if (definition.playMode == MainMenuPlayMode.Custom)
        {
            setupData.customSetupData.actionType = CustomLobbyActionType.HostLobby;
            setupData.customSetupData.hostSetupData.lobbyName = $"Stress Custom {index:D2}";
            setupData.customSetupData.hostSetupData.gameModeType = definition.gameModeType;
            setupData.customSetupData.hostSetupData.ballCountType = definition.ballCountType;
            setupData.customSetupData.hostSetupData.useFreeCell = definition.useFreeCell;
            setupData.customSetupData.hostSetupData.maxPlayers = useMax;
            setupData.customSetupData.hostSetupData.maxPlayer = useMax ? maxPlayerCount : maximumPlayers;
        }
        else
        {
            setupData.playMode = MainMenuPlayMode.Online;
            setupData.onlineSetupData.searchType = OnlineSearchType.QuickPlay;
            setupData.onlineSetupData.gameModeType = definition.gameModeType;
            setupData.onlineSetupData.ballCountType = definition.ballCountType;
            setupData.onlineSetupData.useFreeCell = definition.useFreeCell;
            setupData.onlineSetupData.maxPlayers = useMax;
            setupData.onlineSetupData.maxPlayer = useMax ? maxPlayerCount : maximumPlayers;
        }

        return setupData;
    }

    private StressFakePlayerJoinRequest BuildJoinRequest(string lobbyId, int playerCount, bool firstPlayerIsHost)
    {
        return new StressFakePlayerJoinRequest
        {
            lobbyId = lobbyId,
            playerCount = Mathf.Max(1, playerCount),
            minimumJoinBatch = minimumJoinBatch,
            maximumJoinBatch = maximumJoinBatch,
            minimumJoinDelaySeconds = minimumJoinDelaySeconds,
            maximumJoinDelaySeconds = maximumJoinDelaySeconds,
            minimumLoadDelaySeconds = minimumLoadDelaySeconds,
            maximumLoadDelaySeconds = maximumLoadDelaySeconds,
            firstPlayerIsHost = firstPlayerIsHost,
            limitToAvailableLobbyCapacity = true
        };
    }

    private int GetRequestedJoinPlayerCount(Lobby lobby, int fallbackPlayerCount)
    {
        if (lobby?.Controller == null)
        {
            return 0;
        }

        if (!useMaxLobbySize)
        {
            return Mathf.Max(1, fallbackPlayerCount);
        }

        return Mathf.Max(0, lobby.Controller.MaxPlayer - lobby.Controller.PlayerCount);
    }

    private bool TryBeginCustomCountdown(ActiveStressLobby activeLobby, Lobby lobby, out string failureReason)
    {
        failureReason = string.Empty;
        List<StressFakePlayerRecord> players = StressFakePlayerManager.instance.GetSceneReadyPlayersForLobby(activeLobby.lobbyId);
        StressFakePlayerRecord host = null;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].isHost)
            {
                host = players[i];
                break;
            }
        }

        if (host == null)
        {
            failureReason = $"Custom Lobby {activeLobby.lobbyId} does not have a scene-ready stress host.";
            return false;
        }

        if (!NetworkLobbyManager.instance.TryBeginStressFinalCountdown(activeLobby.lobbyId, host.userId, out failureReason))
        {
            return false;
        }

        activeLobby.customCountdownRequested = true;
        return true;
    }

#endif

    #endregion

    #region Configuration

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private bool CanRun(out string failureReason)
    {
        failureReason = string.Empty;

        if (isRunning)
        {
            failureReason = "The multi-lobby stress simulation is already running.";
            return false;
        }

        if (StressSimulationCoordinator.instance == null || StressHealthReporter.instance == null)
        {
            failureReason = "The shared stress simulation services are not ready.";
            return false;
        }

        if (NetworkBootstrap.instance == null || !NetworkBootstrap.instance.IsAuthority)
        {
            failureReason = "Run the multi-lobby stress simulation from the authority/host player.";
            return false;
        }

        if (NetworkLobbyManager.instance == null || !NetworkLobbyManager.instance.IsReady || StressFakePlayerManager.instance == null)
        {
            failureReason = "The stress simulation services are not ready.";
            return false;
        }

        if (lobbyMode == MultiplayerStressLobbyMode.Random && !includeOnlineLobbies && !includeCustomLobbies)
        {
            failureReason = "Random stress requires Online, Custom, or both lobby types to be enabled.";
            return false;
        }

        if (lobbyMode == MultiplayerStressLobbyMode.Preset && presetLobbies.Count == 0)
        {
            failureReason = "Add at least one preset Lobby before starting the stress run.";
            return false;
        }

        return true;
    }

    private string BuildRunSetupSummary(List<MultiplayerStressLobbyPreset> definitions)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Mode: {lobbyMode}");
        builder.AppendLine($"Requested Lobbies: {definitions.Count}");
        builder.AppendLine($"Use Max Lobby Size: {useMaxLobbySize}");

        if (lobbyMode == MultiplayerStressLobbyMode.Random)
        {
            int minimum = Mathf.Max(1, Mathf.Min(minimumPlayersPerLobby, maximumPlayersPerLobby));
            int maximum = Mathf.Max(minimum, Mathf.Max(minimumPlayersPerLobby, maximumPlayersPerLobby));
            builder.AppendLine(useMaxLobbySize ? $"Lobby Size Range: {minimum} - {maximum}" : $"Player Range: {minimum} - {maximum}");
            builder.AppendLine($"Online Enabled: {includeOnlineLobbies}");
            builder.Append($"Custom Enabled: {includeCustomLobbies}");
        }
        else
        {
            int totalTargets = 0;

            for (int i = 0; i < definitions.Count; i++)
            {
                totalTargets += definitions[i].targetPlayers;
            }

            builder.Append($"Total Requested Players: {totalTargets}");
        }

        return builder.ToString();
    }

    private List<MultiplayerStressLobbyPreset> BuildLobbyDefinitions()
    {
        if (lobbyMode == MultiplayerStressLobbyMode.Preset)
        {
            List<MultiplayerStressLobbyPreset> definitions = new List<MultiplayerStressLobbyPreset>();

            for (int i = 0; i < presetLobbies.Count; i++)
            {
                MultiplayerStressLobbyPreset preset = presetLobbies[i];

                if (preset != null)
                {
                    definitions.Add(ClonePreset(preset));
                }
            }

            return definitions;
        }

        List<MultiplayerStressLobbyPreset> randomDefinitions = new List<MultiplayerStressLobbyPreset>();
        List<BingoGameModeType> gameModes = GetRandomGameModes();
        Array ballCounts = Enum.GetValues(typeof(BingoBallCountType));
        int minPlayers = Mathf.Max(1, Mathf.Min(minimumPlayersPerLobby, maximumPlayersPerLobby));
        int maxPlayers = Mathf.Max(minPlayers, Mathf.Max(minimumPlayersPerLobby, maximumPlayersPerLobby));

        for (int i = 0; i < Mathf.Max(1, lobbyCount); i++)
        {
            MainMenuPlayMode playMode = GetRandomPlayMode();
            BingoGameModeType gameMode = gameModes.Count > 0 ? gameModes[UnityEngine.Random.Range(0, gameModes.Count)] : BingoGameModeType.Traditional;
            BingoBallCountType ballCount = (BingoBallCountType)ballCounts.GetValue(UnityEngine.Random.Range(0, ballCounts.Length));

            randomDefinitions.Add(new MultiplayerStressLobbyPreset
            {
                playMode = playMode,
                gameModeType = gameMode,
                ballCountType = ballCount,
                targetPlayers = UnityEngine.Random.Range(minPlayers, maxPlayers + 1),
                useFreeCell = UnityEngine.Random.value >= 0.5f
            });
        }

        return randomDefinitions;
    }

    private MainMenuPlayMode GetRandomPlayMode()
    {
        if (includeOnlineLobbies && includeCustomLobbies)
        {
            return UnityEngine.Random.value >= 0.5f ? MainMenuPlayMode.Online : MainMenuPlayMode.Custom;
        }

        return includeCustomLobbies ? MainMenuPlayMode.Custom : MainMenuPlayMode.Online;
    }

    private List<BingoGameModeType> GetRandomGameModes()
    {
        List<BingoGameModeType> values = new List<BingoGameModeType>();

        foreach (BingoGameModeType value in Enum.GetValues(typeof(BingoGameModeType)))
        {
            if (value != BingoGameModeType.Custom)
            {
                values.Add(value);
            }
        }

        return values;
    }

    private MultiplayerStressLobbyPreset ClonePreset(MultiplayerStressLobbyPreset preset)
    {
        return new MultiplayerStressLobbyPreset
        {
            playMode = preset.playMode == MainMenuPlayMode.Custom ? MainMenuPlayMode.Custom : MainMenuPlayMode.Online,
            gameModeType = preset.gameModeType,
            ballCountType = preset.ballCountType,
            targetPlayers = Mathf.Max(1, preset.targetPlayers),
            useFreeCell = preset.useFreeCell
        };
    }

#endif

    #endregion

    private class ActiveStressLobby
    {
        public string lobbyId = string.Empty;
        public int targetPlayers;
        public int joinOperationId;
        public bool customCountdownRequested;
    }
}
