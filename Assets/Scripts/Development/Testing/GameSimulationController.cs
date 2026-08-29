using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class GameSimulationController : MonoBehaviour
{
    private const float LobbyEntryRetryDelaySeconds = 0.5f;
    private const float LobbyEntryTimeoutSeconds = 30f;
    private const float NetworkPlayerSettleSeconds = 2f;
    private const float LobbyWorkTimeoutSeconds = 15f;

    [Header("Simulation")]
    [SerializeField] private bool simulateOnStart = true;
    [SerializeField] private MainMenuPlayMode playMode = MainMenuPlayMode.Solo;

    [Header("Game Setup")]
    [SerializeField] private BingoGameModeType gameModeType = BingoGameModeType.Traditional;
    [SerializeField] private BingoBallCountType ballCountType = BingoBallCountType.Ball75;
    [SerializeField] private bool useFreeCell = true;

    [Header("Room Setup")]
    [SerializeField, Min(1)] private int roomSize = 9;
    [SerializeField, Min(0)] private int botCount = 5;

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

        if (GameSceneManager.instance.CurrentSceneType != GameSceneType.Game)
        {
            yield break;
        }

        yield return WaitForStartupReady();

        if (!CanStartSimulation())
        {
            yield break;
        }

        yield return CreateLinkedLobbyAndGame();
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private IEnumerator WaitForStartupReady()
    {
        while (GameManager.instance == null ||
               !GameManager.instance.HasCompletedSessionStartupCleanup ||
               LobbyManager.instance == null ||
               GameSessionManager.instance == null ||
               UserManager.instance == null ||
               !UserManager.instance.IsReady ||
               LobbySettings.instance == null)
        {
            yield return null;
        }
    }

    private bool CanStartSimulation()
    {
        if (playMode == MainMenuPlayMode.None)
        {
            Debug.LogWarning("[GameSimulation] Select Solo, Online, or Custom.");
            return false;
        }

        if (!UserManager.instance.HasUser)
        {
            Debug.LogWarning("[GameSimulation] A current user is required to create a simulated Game.");
            return false;
        }

        GameSessionManager gameSessionManager = GameSessionManager.instance;

        if (gameSessionManager.HasEnteredGame ||
            gameSessionManager.IsEnteringGame ||
            gameSessionManager.EntryState != GameSessionEntryState.Idle)
        {
            return false;
        }

        LobbyManager lobbyManager = LobbyManager.instance;
        return !lobbyManager.HasEnteredLobby &&
               !lobbyManager.IsEnteringLobby &&
               !lobbyManager.HasPendingLobbySetupData;
    }

    private IEnumerator CreateLinkedLobbyAndGame()
    {
        yield return EnterSimulatedLobby();

        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null || !lobbyManager.HasEnteredLobby)
        {
            Debug.LogWarning("[GameSimulation] The linked Lobby could not be created, so the simulated Game was not started.");
            yield break;
        }

        if (playMode == MainMenuPlayMode.Solo)
        {
            yield return StartLocalGame(lobbyManager);
            yield break;
        }

        NetworkLobbyService lobbyService = NetworkLobbyService.instance;

        if (lobbyService == null || !lobbyService.IsReady)
        {
            Debug.LogWarning("[GameSimulation] The network Lobby service was not ready.");
            yield break;
        }

        lobbyService.NotifyLobbySceneReady();

        while (NetworkBootstrap.instance == null || !NetworkBootstrap.instance.IsReady)
        {
            yield return null;
        }

        if (!NetworkBootstrap.instance.IsAuthority)
        {
            yield break;
        }

        yield return StartNetworkGame(lobbyManager.CurrentLobbyId);
    }

    private IEnumerator EnterSimulatedLobby()
    {
        LobbyManager lobbyManager = LobbyManager.instance;
        float timeoutTime = Time.realtimeSinceStartup + LobbyEntryTimeoutSeconds;

        while (lobbyManager != null &&
               Time.realtimeSinceStartup < timeoutTime)
        {
            LobbySetupData setupData = BuildLobbySetupData();
            lobbyManager.SetPendingLobbySetupData(setupData, false);
            lobbyManager.BeginPendingLobbyEntry(false);

            while (lobbyManager != null && lobbyManager.IsEnteringLobby)
            {
                yield return null;
            }

            if (lobbyManager == null || lobbyManager.HasEnteredLobby)
            {
                yield break;
            }

            if (playMode == MainMenuPlayMode.Solo)
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(LobbyEntryRetryDelaySeconds);
        }
    }

    private LobbySetupData BuildLobbySetupData()
    {
        LobbySetupData setupData = new LobbySetupData
        {
            playMode = playMode,
            userData = UserManager.instance.CurrentUser
        };

        int validRoomSize = GetValidRoomSize();

        switch (playMode)
        {
            case MainMenuPlayMode.Solo:
                setupData.soloSetupData.gameModeType = gameModeType;
                setupData.soloSetupData.ballCountType = ballCountType;
                setupData.soloSetupData.useFreeCell = useFreeCell;
                setupData.soloSetupData.maxPlayers = false;
                setupData.soloSetupData.maxPlayer = validRoomSize;
                break;

            case MainMenuPlayMode.Online:
                setupData.onlineSetupData.gameModeType = gameModeType;
                setupData.onlineSetupData.ballCountType = ballCountType;
                setupData.onlineSetupData.useFreeCell = useFreeCell;
                setupData.onlineSetupData.maxPlayers = false;
                setupData.onlineSetupData.maxPlayer = validRoomSize;
                break;

            case MainMenuPlayMode.Custom:
                bool shouldHost = !MultiplayerPlayModeTestContext.IsActive || MultiplayerPlayModeTestContext.IsHost;
                setupData.customSetupData.actionType = shouldHost
                    ? CustomLobbyActionType.HostLobby
                    : CustomLobbyActionType.SearchLobby;

                if (shouldHost)
                {
                    setupData.customSetupData.hostSetupData.gameModeType = gameModeType;
                    setupData.customSetupData.hostSetupData.ballCountType = ballCountType;
                    setupData.customSetupData.hostSetupData.useFreeCell = useFreeCell;
                    setupData.customSetupData.hostSetupData.maxPlayers = false;
                    setupData.customSetupData.hostSetupData.maxPlayer = validRoomSize;
                }
                break;
        }

        return setupData;
    }

    private int GetValidRoomSize()
    {
        LobbySettings settings = LobbySettings.instance;
        return Mathf.Clamp(roomSize, settings.MinimumPlayers, settings.MaxPlayerCount);
    }

    private IEnumerator StartLocalGame(LobbyManager lobbyManager)
    {
        LobbyController controller = lobbyManager.CurrentLobby?.Controller;

        if (controller == null)
        {
            Debug.LogWarning("[GameSimulation] The local Lobby controller was not available.");
            yield break;
        }

        float serviceTimeoutTime = Time.realtimeSinceStartup + LobbyWorkTimeoutSeconds;

        while ((LocalGameSessionManager.instance == null || !LocalGameSessionManager.instance.IsReady) &&
               Time.realtimeSinceStartup < serviceTimeoutTime)
        {
            yield return null;
        }

        if (LocalGameSessionManager.instance == null || !LocalGameSessionManager.instance.IsReady)
        {
            Debug.LogWarning("[GameSimulation] The local Game session manager was not ready.");
            yield break;
        }

        int availableBotSlots = Mathf.Max(0, controller.MaxPlayer - (controller.PlayerCount - controller.BotCount));
        int requestedBotCount = Mathf.Min(Mathf.Max(0, botCount), availableBotSlots);
        int appliedBotCount = controller.SetSimulationBotCount(requestedBotCount);
        float timeoutTime = Time.realtimeSinceStartup + LobbyWorkTimeoutSeconds;

        while (controller.HasPendingWork && Time.realtimeSinceStartup < timeoutTime)
        {
            yield return null;
        }

        if (controller.BotCount != appliedBotCount)
        {
            Debug.LogWarning($"[GameSimulation] Requested {appliedBotCount} bots, but only {controller.BotCount} were available.");
        }

        if (controller.PlayerCount < LobbySettings.instance.MinimumPlayers)
        {
            Debug.LogWarning("[GameSimulation] The simulated Lobby does not have enough players and bots to create a Game.");
            yield break;
        }

        if (!controller.BeginFinalCountdown(UserManager.instance.UserId))
        {
            Debug.LogWarning("[GameSimulation] The local Lobby could not begin its final countdown.");
        }
    }

    private IEnumerator StartNetworkGame(string lobbyId)
    {
        NetworkLobbyManager networkLobbyManager = NetworkLobbyManager.instance;

        while (networkLobbyManager == null || !networkLobbyManager.IsReady)
        {
            yield return null;
            networkLobbyManager = NetworkLobbyManager.instance;
        }

        yield return WaitForRunningNetworkPlayers(networkLobbyManager, lobbyId);

        if (!CanAddNetworkSimulationBots(networkLobbyManager, lobbyId, out int currentPlayerCount))
        {
            yield break;
        }

        int availableBotSlots = Mathf.Max(0, GetValidRoomSize() - currentPlayerCount);
        int requestedBotCount = Mathf.Min(Mathf.Max(0, botCount), availableBotSlots);

        if (!networkLobbyManager.TrySetSimulationBotCount(lobbyId, requestedBotCount, out int appliedBotCount, out string failureReason))
        {
            Debug.LogWarning($"[GameSimulation] {failureReason}");
            yield break;
        }

        float timeoutTime = Time.realtimeSinceStartup + LobbyWorkTimeoutSeconds;
        int actualBotCount = 0;
        int playerCount = 0;
        int sceneReadyPlayerCount = 0;
        bool hasPendingWork = true;

        while (Time.realtimeSinceStartup < timeoutTime)
        {
            if (networkLobbyManager.TryGetSimulationLobbyState(
                    lobbyId,
                    out _,
                    out playerCount,
                    out sceneReadyPlayerCount,
                    out actualBotCount,
                    out hasPendingWork) &&
                !hasPendingWork &&
                sceneReadyPlayerCount >= playerCount)
            {
                break;
            }

            yield return null;
        }

        if (actualBotCount != appliedBotCount)
        {
            Debug.LogWarning($"[GameSimulation] Requested {appliedBotCount} bots, but only {actualBotCount} were available.");
        }

        if (hasPendingWork || sceneReadyPlayerCount < playerCount)
        {
            Debug.LogWarning("[GameSimulation] The simulated Lobby did not finish loading every player and bot.");
            yield break;
        }

        if (playerCount < LobbySettings.instance.MinimumPlayers)
        {
            Debug.LogWarning("[GameSimulation] The simulated Lobby does not have enough players and bots to create a Game.");
            yield break;
        }

        float serviceTimeoutTime = Time.realtimeSinceStartup + LobbyWorkTimeoutSeconds;

        while ((NetworkGameSessionManager.instance == null || !NetworkGameSessionManager.instance.IsReady) &&
               Time.realtimeSinceStartup < serviceTimeoutTime)
        {
            yield return null;
        }

        if (NetworkGameSessionManager.instance == null || !NetworkGameSessionManager.instance.IsReady)
        {
            Debug.LogWarning("[GameSimulation] The network Game session manager was not ready.");
            yield break;
        }

        if (!networkLobbyManager.TryBeginSimulationFinalCountdown(lobbyId, UserManager.instance.UserId, out failureReason))
        {
            Debug.LogWarning($"[GameSimulation] {failureReason}");
        }
    }

    private IEnumerator WaitForRunningNetworkPlayers(NetworkLobbyManager networkLobbyManager, string lobbyId)
    {
        float timeoutTime = Time.realtimeSinceStartup + LobbyEntryTimeoutSeconds;
        float stableSince = Time.realtimeSinceStartup;
        int previousHumanPlayerCount = -1;
        int previousConnectedTestPlayerCount = -1;

        while (Time.realtimeSinceStartup < timeoutTime)
        {
            if (!networkLobbyManager.TryGetSimulationLobbyState(
                    lobbyId,
                    out int humanPlayerCount,
                    out int playerCount,
                    out int sceneReadyPlayerCount,
                    out _,
                    out bool hasPendingWork) ||
                !networkLobbyManager.TryGetRunningSimulationTestPlayerState(
                    lobbyId,
                    out int connectedTestPlayerCount,
                    out int joinedTestPlayerCount,
                    out int sceneReadyTestPlayerCount))
            {
                yield return null;
                continue;
            }

            if (humanPlayerCount != previousHumanPlayerCount ||
                connectedTestPlayerCount != previousConnectedTestPlayerCount)
            {
                previousHumanPlayerCount = humanPlayerCount;
                previousConnectedTestPlayerCount = connectedTestPlayerCount;
                stableSince = Time.realtimeSinceStartup;
            }

            bool allConnectedTestPlayersReady =
                joinedTestPlayerCount >= connectedTestPlayerCount &&
                sceneReadyTestPlayerCount >= connectedTestPlayerCount;

            if (!hasPendingWork &&
                sceneReadyPlayerCount >= playerCount &&
                allConnectedTestPlayersReady &&
                Time.realtimeSinceStartup - stableSince >= NetworkPlayerSettleSeconds)
            {
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning(
            "[GameSimulation] Timed out while waiting for the running real test players to finish Lobby loading.");
    }

    private bool CanAddNetworkSimulationBots(NetworkLobbyManager networkLobbyManager, string lobbyId, out int playerCount)
    {
        playerCount = 0;

        if (!networkLobbyManager.TryGetSimulationLobbyState(
                lobbyId,
                out _,
                out playerCount,
                out int sceneReadyPlayerCount,
                out int currentBotCount,
                out bool hasPendingWork))
        {
            Debug.LogWarning("[GameSimulation] The simulated Lobby could not be read before adding bots.");
            return false;
        }

        if (currentBotCount > 0 || hasPendingWork || sceneReadyPlayerCount < playerCount)
        {
            Debug.LogWarning("[GameSimulation] Bots were not added because the real-player list was not fully loaded first.");
            return false;
        }

        return true;
    }
#endif
}
