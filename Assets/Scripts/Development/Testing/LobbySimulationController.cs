using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbySimulationController : MonoBehaviour
{
    [Header("Simulation")]
    [SerializeField] private bool simulateOnStart = true;
    [SerializeField] private MainMenuPlayMode playMode = MainMenuPlayMode.Solo;

    [Header("Game Setup")]
    [SerializeField] private BingoGameModeType gameModeType = BingoGameModeType.Traditional;
    [SerializeField] private BingoBallCountType ballCountType = BingoBallCountType.Ball75;

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
                bool shouldHost =
                    !MultiplayerPlayModeTestContext.IsActive ||
                    MultiplayerPlayModeTestContext.IsHost;

                ConfigureCustomSetup(
                    lobbySetupData.customSetupData,
                    shouldHost);

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

        customSetupData.actionType = shouldHost
            ? CustomLobbyActionType.HostLobby
            : CustomLobbyActionType.SearchLobby;

        if (!shouldHost || customSetupData.hostSetupData == null)
        {
            return;
        }

        customSetupData.hostSetupData.gameModeType = gameModeType;
        customSetupData.hostSetupData.ballCountType = ballCountType;
    }

    public void ApplyNetworkSimulationSetup(LobbySetupData lobbySetupData, bool isHostPlayer)
    {
        if (lobbySetupData == null)
        {
            return;
        }

        if (playMode != MainMenuPlayMode.Online &&
            playMode != MainMenuPlayMode.Custom)
        {
            return;
        }

        lobbySetupData.playMode = playMode;

        switch (playMode)
        {
            case MainMenuPlayMode.Online:
                ConfigureOnlineSetup(
                    lobbySetupData.onlineSetupData);

                break;

            case MainMenuPlayMode.Custom:
                ConfigureCustomSetup(
                    lobbySetupData.customSetupData,
                    isHostPlayer);

                break;
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

        if (playMode != MainMenuPlayMode.Online &&
            playMode != MainMenuPlayMode.Custom)
        {
            yield break;
        }

        NetworkLobbyConnection lobbyConnection =
            NetworkLobbyConnection.GetLocalConnection();

        if (lobbyConnection == null)
        {
            Debug.LogWarning(
                "[LobbySimulation] The network lobby connection was not available to mark the simulated player scene-ready.");

            yield break;
        }

        lobbyConnection.NotifyLobbySceneReady();
    }

}