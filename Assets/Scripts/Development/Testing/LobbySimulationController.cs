using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BingoGame.Development.Testing;

[DisallowMultipleComponent]
public class LobbySimulationController : MonoBehaviour
{
    private enum SimulationGameModeType
    {
        Traditional = (int)BingoGameModeType.Traditional,
        Blackout = (int)BingoGameModeType.Blackout,
        Risk = (int)BingoGameModeType.Risk,
        Death = (int)BingoGameModeType.Death
    }

    #region Fields

    [Header("Simulation Controls")]
    [SerializeField] private bool simulateOnStart = true;

    [Space]
    [Header("Game Setup")]
    [Tooltip("Mode to create when Play starts directly in this Lobby scene.")]
    [SerializeField] private SimulationPlayMode playMode = SimulationPlayMode.Solo;
    [SerializeField] private SimulationGameModeType gameModeType = SimulationGameModeType.Traditional;
    [SerializeField] private BingoBallCountType ballCountType = BingoBallCountType.Ball75;
    [Tooltip("Include the free center cell on simulated boards.")]
    [SerializeField] private bool useFreeCell = true;
    [Tooltip("Use ranking instead of the selected game mode's default rank setting.")]
    [SerializeField] private bool useRank;

    [Space]
    [Header("Room Setup")]
    [Tooltip("Room capacity, limited by Lobby Settings.")]
    [SerializeField, Min(1)] private int roomSize = 9;
    [Tooltip("Total bots to add, limited by available bots and room capacity.")]
    [SerializeField, Min(0)] private int botCount = 5;

    private bool isSimulationRunning;
    private LobbyManager simulationLobbyManager;
    private LobbySetupData simulationSetupData;

    #endregion

    #region Unity Methods

    private void Awake()
    {
#if UNITY_EDITOR
        BingoGame.Development.MultiplayerTesting.SimulationStartupSettings.Apply(this);
#endif
    }

    public static bool IsSoloSimulationStartActive()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GameSceneManager.instance == null || !GameSceneManager.instance.IsInitialScene(GameSceneType.Lobby))
        {
            return false;
        }

        LobbySimulationController controller = FindFirstObjectByType<LobbySimulationController>();
#if UNITY_EDITOR
        if (controller != null)
        {
            BingoGame.Development.MultiplayerTesting.SimulationStartupSettings.Apply(controller);
        }
#endif
        return controller != null && controller.isActiveAndEnabled &&
               controller.simulateOnStart && controller.playMode == SimulationPlayMode.Solo;
#else
        return false;
#endif
    }

    private IEnumerator Start()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        while (GameSceneManager.instance == null)
        {
            yield return null;
        }

        yield return null;

        if (!GameSceneManager.instance.IsInitialScene(GameSceneType.Lobby))
        {
            yield break;
        }

#if UNITY_EDITOR
        yield return BingoGame.Development.MultiplayerTesting.SimulationStartupSettings.WaitForSettings(this);
#endif

        if (!CanRunInCurrentScene())
        {
            yield break;
        }

        isSimulationRunning = true;
        yield return WaitForSceneReady();

        if (!CanStartSimulation())
        {
            isSimulationRunning = false;
            yield break;
        }

        StartLobbySimulation();
        yield return ApplyPostEntrySimulation();
        isSimulationRunning = false;
#endif
    }

    private void Update()
    {
        if (isSimulationRunning && (!CanRunInCurrentScene() ||
            (simulationSetupData != null && (simulationLobbyManager == null || UserManager.instance == null)) ||
            (simulationLobbyManager != null && simulationLobbyManager.PendingLobbySetupData != null &&
             simulationLobbyManager.PendingLobbySetupData != simulationSetupData)))
        {
            StopSimulation();
        }
    }

    private void OnDisable()
    {
        StopSimulation();
    }

    private bool CanRunInCurrentScene()
    {
        return isActiveAndEnabled && simulateOnStart && GameSceneManager.instance != null &&
               GameSceneManager.instance.IsInitialScene(GameSceneType.Lobby);
    }

    private void StopSimulation()
    {
        StopAllCoroutines();
        isSimulationRunning = false;
        if (simulationLobbyManager != null && simulationSetupData != null &&
            simulationLobbyManager.PendingLobbySetupData == simulationSetupData)
        {
            simulationLobbyManager.CancelPendingLobbyEntry();
        }
        simulationSetupData = null;
    }

    #endregion

    #region Simulation Setup

    private IEnumerator WaitForSceneReady()
    {
        while (GameManager.instance == null ||
               !GameManager.instance.HasCompletedSessionStartupCleanup ||
               LobbyManager.instance == null ||
               LobbySettings.instance == null ||
               UserManager.instance == null ||
               !UserManager.instance.IsReady ||
               SceneReadyController.instance == null || !SceneReadyController.instance.AreAllReady())
        {
            yield return null;
        }

        yield return null;
    }

    private bool CanStartSimulation()
    {
        if (!CanRunInCurrentScene() || LobbyManager.instance == null || UserManager.instance == null)
        {
            return false;
        }

        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager.HasEnteredLobby || lobbyManager.IsEnteringLobby || lobbyManager.HasPendingLobbySetupData)
        {
            return false;
        }

        if (playMode != SimulationPlayMode.Solo && playMode != SimulationPlayMode.Online && playMode != SimulationPlayMode.Custom)
        {
            return false;
        }

        if (!UserManager.instance.HasUser)
        {
            return false;
        }

        return true;
    }

    private void StartLobbySimulation()
    {
        LobbySetupData lobbySetupData = BuildLobbySetupData();

        if (lobbySetupData == null)
        {
            return;
        }

        simulationLobbyManager = LobbyManager.instance;
        simulationSetupData = lobbySetupData;
        simulationLobbyManager.SetPendingLobbySetupData(lobbySetupData, false);
        simulationLobbyManager.BeginPendingLobbyEntry();
    }

    private LobbySetupData BuildLobbySetupData()
    {
        LobbySetupData lobbySetupData = new LobbySetupData
        {
            playMode = (MainMenuPlayMode)playMode,
            usesSimulationSettings = true,
            userData = UserManager.instance.CurrentUser
        };

        ConfigureModeSetup(lobbySetupData);
        return lobbySetupData;
    }

    private void ConfigureModeSetup(LobbySetupData lobbySetupData)
    {
        switch (playMode)
        {
            case SimulationPlayMode.Solo:
                ConfigureSoloSetup(lobbySetupData.soloSetupData);
                break;

            case SimulationPlayMode.Online:
                ConfigureOnlineSetup(lobbySetupData.onlineSetupData);
                break;

            case SimulationPlayMode.Custom:
                bool shouldHost = !MultiplayerPlayModeTestContext.IsActive || MultiplayerPlayModeTestContext.IsHost;
                ConfigureCustomSetup(lobbySetupData.customSetupData, shouldHost);
                break;
        }
    }

    private void ConfigureSoloSetup(SoloLobbySetupData soloSetupData)
    {
        if (soloSetupData == null)
        {
            return;
        }

        soloSetupData.gameModeType = GetSelectedGameModeType();
        soloSetupData.ballCountType = ballCountType;
        soloSetupData.useFreeCell = useFreeCell;
        soloSetupData.usesDefaultRank = false;
        soloSetupData.useRank = useRank;
        soloSetupData.maxPlayers = false;
        soloSetupData.maxPlayer = GetValidRoomSize();
        soloSetupData.botCount = Mathf.Clamp(botCount, 0, soloSetupData.maxPlayer - 1);
    }

    private void ConfigureOnlineSetup(OnlineLobbySetupData onlineSetupData)
    {
        if (onlineSetupData == null)
        {
            return;
        }

        onlineSetupData.gameModeType = GetSelectedGameModeType();
        onlineSetupData.ballCountType = ballCountType;
        onlineSetupData.useFreeCell = useFreeCell;
        onlineSetupData.hasUseRankOverride = true;
        onlineSetupData.useRank = useRank;
        onlineSetupData.maxPlayers = false;
        onlineSetupData.maxPlayer = GetValidRoomSize();
    }

    private void ConfigureCustomSetup(CustomLobbySetupData customSetupData, bool shouldHost)
    {
        if (customSetupData == null)
        {
            return;
        }

        customSetupData.actionType = shouldHost ? CustomLobbyActionType.HostLobby : CustomLobbyActionType.SearchLobby;

        if (!shouldHost || customSetupData.hostSetupData == null)
        {
            return;
        }

        customSetupData.hostSetupData.gameModeType = GetSelectedGameModeType();
        customSetupData.hostSetupData.ballCountType = ballCountType;
        customSetupData.hostSetupData.useFreeCell = useFreeCell;
        customSetupData.hostSetupData.usesDefaultRank = false;
        customSetupData.hostSetupData.useRank = useRank;
        customSetupData.hostSetupData.maxPlayers = false;
        customSetupData.hostSetupData.maxPlayer = GetValidRoomSize();
    }

    private BingoGameModeType GetSelectedGameModeType()
    {
        return gameModeType switch
        {
            SimulationGameModeType.Blackout => BingoGameModeType.Blackout,
            SimulationGameModeType.Risk => BingoGameModeType.Risk,
            SimulationGameModeType.Death => BingoGameModeType.Death,
            _ => BingoGameModeType.Traditional
        };
    }

    private int GetValidRoomSize()
    {
        LobbySettings settings = LobbySettings.instance;
        return Mathf.Clamp(roomSize, settings.MinimumPlayers, settings.MaxPlayerCount);
    }

    #endregion

    #region Post Entry

    private IEnumerator ApplyPostEntrySimulation()
    {
        while (LobbyManager.instance != null && LobbyManager.instance.IsEnteringLobby)
        {
            yield return null;
        }

        if (!CanRunInCurrentScene() || LobbyManager.instance == null || !LobbyManager.instance.HasEnteredLobby)
        {
            yield break;
        }

        if (playMode != SimulationPlayMode.Online && playMode != SimulationPlayMode.Custom)
        {
            yield break;
        }

        NetworkLobbyService lobbyService = NetworkLobbyService.instance;

        if (lobbyService == null || !lobbyService.IsReady)
        {
            Debug.LogWarning("[LobbySimulation] The network lobby service was not available to mark the simulated player scene-ready.");
            yield break;
        }

        lobbyService.NotifyLobbySceneReady();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        yield return ApplyNetworkBots();
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private IEnumerator ApplyNetworkBots()
    {
        if (MultiplayerPlayModeTestContext.IsActive && !MultiplayerPlayModeTestContext.IsHost)
        {
            yield break;
        }

        NetworkLobbyManager networkLobbyManager = NetworkLobbyManager.instance;
        string lobbyId = simulationLobbyManager.CurrentLobbyId;

        if (networkLobbyManager == null || !networkLobbyManager.IsReady ||
            !networkLobbyManager.TryGetStressLobby(lobbyId, out Lobby lobby) ||
            !lobby.usesSimulationSettings)
        {
            yield break;
        }

        List<string> expectedUserIds = new List<string> { UserManager.instance.UserId };

        if (MultiplayerPlayModeTestContext.IsActive)
        {
            List<int> activePlayerNumbers = new List<int>();

            if (!MultiplayerPlayModeTestContext.TryGetActiveTestPlayerNumbers(activePlayerNumbers))
            {
                activePlayerNumbers.Clear();
                activePlayerNumbers.AddRange(new[] { 1, 2, 3, 4 });
            }

            for (int i = 0; i < activePlayerNumbers.Count; i++)
            {
                string userId = MultiplayerPlayModeTestContext.GetUserId(activePlayerNumbers[i]);

                if (!string.IsNullOrWhiteSpace(userId) && !expectedUserIds.Contains(userId))
                {
                    expectedUserIds.Add(userId);
                }
            }
        }

        float timeoutTime = Time.realtimeSinceStartup + 35f;

        while (CanRunInCurrentScene() && simulationLobbyManager != null &&
               simulationLobbyManager.HasEnteredLobby && simulationLobbyManager.CurrentLobbyId == lobbyId &&
               networkLobbyManager != null && networkLobbyManager.IsReady && lobby.lobbyState == LobbyState.Open)
        {
            bool playersReady = networkLobbyManager.TryGetRunningSimulationTestPlayerState(
                lobbyId, expectedUserIds, out int connectedPlayers, out int joinedPlayers, out int readyPlayers) &&
                connectedPlayers >= expectedUserIds.Count && joinedPlayers >= expectedUserIds.Count &&
                readyPlayers >= expectedUserIds.Count;

            if (playersReady || Time.realtimeSinceStartup >= timeoutTime)
            {
                LobbyController controller = lobby.Controller;

                if (!controller.HasPendingWork && controller.SceneReadyPlayerCount >= controller.PlayerCount)
                {
                    int humanPlayerCount = controller.PlayerCount - controller.BotCount;
                    int availableBotSlots = Mathf.Max(0, controller.MaxPlayer - humanPlayerCount);
                    int requestedBotCount = Mathf.Clamp(botCount, 0, availableBotSlots);
                    networkLobbyManager.TrySetSimulationBotCount(lobbyId, requestedBotCount, out _, out _);
                    yield break;
                }

                if (Time.realtimeSinceStartup >= timeoutTime)
                {
                    yield break;
                }
            }

            yield return null;
        }
    }
#endif

    #endregion
}
