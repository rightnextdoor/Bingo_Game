using System.Collections;
using UnityEngine;
using BingoGame.Development.Testing;

[DisallowMultipleComponent]
public class LobbySimulationController : MonoBehaviour
{
    #region Fields

    [Header("Simulation Controls")]
    [SerializeField] private bool simulateOnStart = true;

    [Space]
    [Header("Game Setup")]
    [Tooltip("Mode to create when Play starts directly in this Lobby scene.")]
    [SerializeField] private SimulationPlayMode playMode = SimulationPlayMode.Solo;
    [SerializeField] private BingoGameModeType gameModeType = BingoGameModeType.Traditional;
    [SerializeField] private BingoBallCountType ballCountType = BingoBallCountType.Ball75;

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
        if (!simulateOnStart)
        {
            yield break;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        while (GameSceneManager.instance == null)
        {
            yield return null;
        }

        yield return null;

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

        soloSetupData.gameModeType = gameModeType;
        soloSetupData.ballCountType = ballCountType;
    }

    private void ConfigureOnlineSetup(OnlineLobbySetupData onlineSetupData)
    {
        if (onlineSetupData == null)
        {
            return;
        }

        onlineSetupData.gameModeType = gameModeType;
        onlineSetupData.ballCountType = ballCountType;
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

        customSetupData.hostSetupData.gameModeType = gameModeType;
        customSetupData.hostSetupData.ballCountType = ballCountType;
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
    }

    #endregion
}
