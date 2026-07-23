using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbySimulationController : MonoBehaviour
{
    [Header("Simulation")]
    [SerializeField] private bool simulateOnStart = true;
    [SerializeField] private MainMenuPlayMode playMode = MainMenuPlayMode.Solo;

    [Header("Custom Simulation")]
    [SerializeField] private bool isHost = true;

    private IEnumerator Start()
    {
        if (!simulateOnStart)
        {
            yield break;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        yield return WaitForSceneReady();

        if (!CanStartSimulation())
        {
            yield break;
        }

        StartLobbySimulation();
#endif
    }

    private IEnumerator WaitForSceneReady()
    {
        while (LobbyManager.instance == null ||
               UserManager.instance == null ||
               SceneReadyController.instance == null)
        {
            yield return null;
        }

        while (!UserManager.instance.IsReady ||
               !SceneReadyController.instance.AreAllReady())
        {
            yield return null;
        }

        // Allow systems that react to readiness to finish their current frame.
        yield return null;
    }

    private bool CanStartSimulation()
    {
        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager.HasEnteredLobby ||
            lobbyManager.IsEnteringLobby ||
            lobbyManager.HasPendingLobbySetupData)
        {
            return false;
        }

        if (playMode == MainMenuPlayMode.None)
        {
            Debug.LogWarning("[LobbySimulation] Select Solo, Online, or Custom.");
            return false;
        }

        if (!UserManager.instance.HasUser)
        {
            Debug.LogWarning("[LobbySimulation] A current user is required to simulate lobby entry.");
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

        LobbyManager.instance.SetPendingLobbySetupData(lobbySetupData);
        LobbyManager.instance.BeginPendingLobbyEntry();

        StartCoroutine(ApplyPostEntrySimulation());
    }

    private LobbySetupData BuildLobbySetupData()
    {
        LobbySetupData lobbySetupData = new LobbySetupData
        {
            playMode = playMode,
            userData = UserManager.instance.CurrentUser
        };

        ConfigureModeSetup(lobbySetupData);

        return lobbySetupData;
    }

    private void ConfigureModeSetup(LobbySetupData lobbySetupData)
    {
        switch (playMode)
        {
            case MainMenuPlayMode.Solo:
                ConfigureSoloSetup(lobbySetupData.soloSetupData);
                break;

            case MainMenuPlayMode.Online:
                ConfigureOnlineSetup(lobbySetupData.onlineSetupData);
                break;

            case MainMenuPlayMode.Custom:
                ConfigureCustomSetup(lobbySetupData.customSetupData);
                break;
        }
    }

    private void ConfigureSoloSetup(SoloLobbySetupData soloSetupData)
    {
        // Uses the default Solo setup for now.
        // Add future Solo simulation values here.
    }

    private void ConfigureOnlineSetup(OnlineLobbySetupData onlineSetupData)
    {
        // Uses the default Online setup for now.
        // Add future Online simulation values here.
    }

    private void ConfigureCustomSetup(CustomLobbySetupData customSetupData)
    {
        if (customSetupData == null)
        {
            return;
        }

        customSetupData.actionType = CustomLobbyActionType.HostLobby;
    }

    private BingoGameModeType GetGameModeType(LobbySetupData lobbySetupData)
    {
        switch (lobbySetupData.playMode)
        {
            case MainMenuPlayMode.Solo:
                return lobbySetupData.soloSetupData.gameModeType;

            case MainMenuPlayMode.Online:
                return lobbySetupData.onlineSetupData.gameModeType;

            case MainMenuPlayMode.Custom:
                return lobbySetupData.customSetupData.hostSetupData.gameModeType;

            default:
                return BingoGameModeType.Traditional;
        }
    }

    private BingoBallCountType GetBallCountType(LobbySetupData lobbySetupData)
    {
        switch (lobbySetupData.playMode)
        {
            case MainMenuPlayMode.Solo:
                return lobbySetupData.soloSetupData.ballCountType;

            case MainMenuPlayMode.Online:
                return lobbySetupData.onlineSetupData.ballCountType;

            case MainMenuPlayMode.Custom:
                return lobbySetupData.customSetupData.hostSetupData.ballCountType;

            default:
                return BingoBallCountType.Ball75;
        }
    }

    private IEnumerator ApplyPostEntrySimulation()
    {
        while (LobbyManager.instance != null &&
               LobbyManager.instance.IsEnteringLobby)
        {
            yield return null;
        }

        if (LobbyManager.instance == null ||
            !LobbyManager.instance.HasEnteredLobby)
        {
            yield break;
        }

        if (playMode == MainMenuPlayMode.Custom)
        {
            ApplyCustomPlayerSimulation();
        }

        RefreshLobbyUI();
    }

    private void ApplyCustomPlayerSimulation()
    {
        if (UserManager.instance == null ||
            UserManager.instance.CurrentUser == null ||
            LobbyManager.instance.CurrentLobby?.Controller == null)
        {
            return;
        }

        string userId = UserManager.instance.CurrentUser.userId;

        LobbyPlayerData playerData =
            LobbyManager.instance.CurrentLobby.Controller.GetPlayer(userId);

        if (playerData == null)
        {
            return;
        }

        playerData.isHost = isHost;

    }

    private void RefreshLobbyUI()
    {
        if (LobbyManager.instance?.CurrentLobby?.Controller == null)
        {
            return;
        }

        LobbySceneController lobbySceneController =
            FindFirstObjectByType<LobbySceneController>();

        if (lobbySceneController == null)
        {
            return;
        }

        LobbyViewData lobbyViewData =
            LobbyManager.instance.CurrentLobby.Controller.BuildViewData();

        lobbySceneController.DisplayLobbyInfo(lobbyViewData);
    }

}