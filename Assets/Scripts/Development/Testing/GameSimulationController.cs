using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class GameSimulationController : MonoBehaviour
{
    private const float LobbyEntryRetryDelaySeconds = 0.5f;
    private const float LobbyEntryTimeoutSeconds = 30f;
    private const float NetworkPlayerWaitTimeoutSeconds = 35f;
    private const float NetworkGameEntryTimeoutSeconds = 90f;
    private const float LobbyWorkTimeoutSeconds = 15f;
    private const float FailureCleanupTimeoutSeconds = 5f;

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

    private bool isEndingSimulation;
    private bool networkGameStartRequested;

    public static bool IsNetworkSimulationStartActive()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameSimulationController controller = FindFirstObjectByType<GameSimulationController>();
        bool isSimulationFollower = MultiplayerPlayModeTestContext.IsActive &&
                                    MultiplayerPlayModeTestContext.PlayerNumber > 1;
        return controller != null &&
               controller.isActiveAndEnabled &&
               controller.simulateOnStart &&
               !controller.isEndingSimulation &&
               (isSimulationFollower ||
                controller.playMode == MainMenuPlayMode.Online ||
                controller.playMode == MainMenuPlayMode.Custom);
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
        if (playMode == MainMenuPlayMode.None && !IsSimulationFollower())
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
        if (UsesNetworkSimulationRuntime())
        {
            GameSessionManager.instance.SetGameSimulationCreationPending(true);
        }

        yield return EnterSimulatedLobby();

        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null || !lobbyManager.HasEnteredLobby)
        {
            yield return ReturnSimulationPlayerToMain(
                "The test player could not connect to Player 1's Game simulation lobby before the timeout.");
            yield break;
        }

        MainMenuPlayMode enteredPlayMode = lobbyManager.CurrentLobbyViewData?.playMode ?? playMode;

        if (enteredPlayMode == MainMenuPlayMode.Solo)
        {
            yield return StartLocalGame(lobbyManager);
            yield break;
        }

        if (!GameSessionManager.instance.PrepareForGameSimulationEntry(lobbyManager.CurrentLobbyId))
        {
            yield return ReturnSimulationPlayerToMain(
                "The test player could not prepare the staged Game simulation entry.");
            yield break;
        }

        NetworkLobbyService lobbyService = NetworkLobbyService.instance;

        if (lobbyService == null || !lobbyService.IsReady)
        {
            yield return ReturnSimulationPlayerToMain("The network Lobby service was not ready.");
            yield break;
        }

        lobbyService.NotifyLobbySceneReady();

        while (NetworkBootstrap.instance == null || !NetworkBootstrap.instance.IsReady)
        {
            yield return null;
        }

        if (NetworkBootstrap.instance.IsAuthority)
        {
            yield return StartNetworkGame(lobbyManager.CurrentLobbyId);

            if (!networkGameStartRequested)
            {
                yield return ReturnSimulationPlayerToMain("Player 1 could not start the simulated Game.");
                yield break;
            }
        }

        yield return WaitForNetworkGameEntry();
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

            while (lobbyManager != null &&
                   lobbyManager.IsEnteringLobby &&
                   Time.realtimeSinceStartup < timeoutTime)
            {
                yield return null;
            }

            if (lobbyManager != null && lobbyManager.IsEnteringLobby)
            {
                lobbyManager.CancelPendingLobbyEntry();
                yield break;
            }

            if (lobbyManager == null || lobbyManager.HasEnteredLobby)
            {
                yield break;
            }

            if (!UsesNetworkSimulationRuntime())
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(LobbyEntryRetryDelaySeconds);
        }
    }

    private LobbySetupData BuildLobbySetupData()
    {
        int simulationPlayerNumber = GetSimulationPlayerNumber();
        MainMenuPlayMode requestedPlayMode = simulationPlayerNumber > 1
            ? MainMenuPlayMode.Online
            : playMode;
        LobbySetupData setupData = new LobbySetupData
        {
            playMode = requestedPlayMode,
            isGameSimulation = true,
            gameSimulationPlayerNumber = simulationPlayerNumber,
            userData = UserManager.instance.CurrentUser
        };

        int validRoomSize = GetValidRoomSize();

        switch (requestedPlayMode)
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
                bool shouldHost = setupData.gameSimulationPlayerNumber == 1;
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

    private int GetSimulationPlayerNumber()
    {
        return MultiplayerPlayModeTestContext.IsActive
            ? MultiplayerPlayModeTestContext.PlayerNumber
            : 1;
    }

    private bool IsSimulationFollower()
    {
        return GetSimulationPlayerNumber() > 1;
    }

    private bool UsesNetworkSimulationRuntime()
    {
        return IsSimulationFollower() ||
               playMode == MainMenuPlayMode.Online ||
               playMode == MainMenuPlayMode.Custom;
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
        networkGameStartRequested = false;
        NetworkLobbyManager networkLobbyManager = NetworkLobbyManager.instance;

        while (networkLobbyManager == null || !networkLobbyManager.IsReady)
        {
            yield return null;
            networkLobbyManager = NetworkLobbyManager.instance;
        }

        List<string> expectedTestUserIds = BuildExpectedTestUserIds(out bool hasExactActivePlayerList);
        yield return WaitForRunningNetworkPlayers(
            networkLobbyManager,
            lobbyId,
            expectedTestUserIds,
            hasExactActivePlayerList);

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
            yield break;
        }

        networkGameStartRequested = true;
    }

    private List<string> BuildExpectedTestUserIds(out bool hasExactActivePlayerList)
    {
        List<int> activePlayerNumbers = new List<int>();
        hasExactActivePlayerList = MultiplayerPlayModeTestContext.TryGetActiveTestPlayerNumbers(activePlayerNumbers);
        List<string> expectedUserIds = new List<string>();
        AddExpectedUserId(expectedUserIds, UserManager.instance.UserId);

        if (hasExactActivePlayerList)
        {
            for (int i = 0; i < activePlayerNumbers.Count; i++)
            {
                int playerNumber = activePlayerNumbers[i];

                if (playerNumber == 1)
                {
                    continue;
                }

                AddExpectedUserId(
                    expectedUserIds,
                    MultiplayerPlayModeTestContext.GetUserId(playerNumber));
            }

            Debug.Log($"[GameSimulation] Waiting for {expectedUserIds.Count} active test player(s) before adding bots.");
            return expectedUserIds;
        }

#if UNITY_EDITOR
        for (int playerNumber = 2; playerNumber <= 4; playerNumber++)
        {
            AddExpectedUserId(
                expectedUserIds,
                MultiplayerPlayModeTestContext.GetUserId(playerNumber));
        }

        Debug.LogWarning(
            "[GameSimulation] The active Multiplayer Play Mode player list could not be read. " +
            "The simulation will use the full test-player timeout before adding bots.");
#else
        hasExactActivePlayerList = true;
#endif

        return expectedUserIds;
    }

    private void AddExpectedUserId(List<string> expectedUserIds, string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId) && !expectedUserIds.Contains(userId))
        {
            expectedUserIds.Add(userId);
        }
    }

    private IEnumerator WaitForRunningNetworkPlayers(
        NetworkLobbyManager networkLobbyManager,
        string lobbyId,
        IReadOnlyList<string> expectedUserIds,
        bool hasExactActivePlayerList)
    {
        float timeoutTime = Time.realtimeSinceStartup + NetworkPlayerWaitTimeoutSeconds;
        int connectedTestPlayerCount = 0;
        int joinedTestPlayerCount = 0;
        int sceneReadyTestPlayerCount = 0;

        while (Time.realtimeSinceStartup < timeoutTime)
        {
            if (!networkLobbyManager.TryGetSimulationLobbyState(
                    lobbyId,
                    out _,
                    out int playerCount,
                    out int sceneReadyPlayerCount,
                    out _,
                    out bool hasPendingWork) ||
                !networkLobbyManager.TryGetRunningSimulationTestPlayerState(
                    lobbyId,
                    expectedUserIds,
                    out connectedTestPlayerCount,
                    out joinedTestPlayerCount,
                    out sceneReadyTestPlayerCount))
            {
                yield return null;
                continue;
            }

            int expectedPlayerCount = expectedUserIds?.Count ?? 0;
            bool allExpectedPlayersReady =
                connectedTestPlayerCount >= expectedPlayerCount &&
                joinedTestPlayerCount >= expectedPlayerCount &&
                sceneReadyTestPlayerCount >= expectedPlayerCount;

            if (!hasPendingWork &&
                sceneReadyPlayerCount >= playerCount &&
                allExpectedPlayersReady)
            {
                yield break;
            }

            yield return null;
        }

        string expectationLabel = hasExactActivePlayerList ? "active" : "possible";
        Debug.LogWarning(
            $"[GameSimulation] Timed out waiting for the {expectationLabel} test players. " +
            $"Connected: {connectedTestPlayerCount}, joined: {joinedTestPlayerCount}, ready: {sceneReadyTestPlayerCount}. " +
            "The simulation will continue with the players that connected successfully.");
    }

    private IEnumerator WaitForNetworkGameEntry()
    {
        float timeoutTime = Time.realtimeSinceStartup + NetworkGameEntryTimeoutSeconds;

        while (Time.realtimeSinceStartup < timeoutTime)
        {
            GameSessionManager gameSessionManager = GameSessionManager.instance;

            if (gameSessionManager != null && gameSessionManager.HasEnteredGame)
            {
                yield break;
            }

            if (GameSceneManager.instance == null ||
                GameSceneManager.instance.CurrentSceneType != GameSceneType.Game)
            {
                yield break;
            }

            if (gameSessionManager != null &&
                ((gameSessionManager.LastEntryResult != null && !gameSessionManager.LastEntryResult.success) ||
                 gameSessionManager.EntryState == GameSessionEntryState.Failed))
            {
                yield return ReturnSimulationPlayerToMain("The test player could not enter Player 1's simulated Game.");
                yield break;
            }

            yield return null;
        }

        if (GameSessionManager.instance == null || !GameSessionManager.instance.HasEnteredGame)
        {
            yield return ReturnSimulationPlayerToMain(
                "The test player timed out while waiting to enter Player 1's simulated Game.");
        }
    }

    private IEnumerator ReturnSimulationPlayerToMain(string failureMessage)
    {
        if (isEndingSimulation)
        {
            yield break;
        }

        isEndingSimulation = true;
        Debug.LogWarning($"[GameSimulation] {failureMessage}");

        GameSessionManager gameSessionManager = GameSessionManager.instance;
        gameSessionManager?.SetGameSimulationCreationPending(false);
        gameSessionManager?.ClearCurrentGame(true);

        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager != null && lobbyManager.HasEnteredLobby && !lobbyManager.IsLeavingLobby)
        {
            Task<LobbyExitResult> leaveTask = lobbyManager.LeaveCurrentLobbyAsync(false);
            float leaveTimeoutTime = Time.realtimeSinceStartup + FailureCleanupTimeoutSeconds;

            while (!leaveTask.IsCompleted && Time.realtimeSinceStartup < leaveTimeoutTime)
            {
                yield return null;
            }
        }
        else
        {
            lobbyManager?.ClearPendingLobbySetupData();
        }

        NetworkBootstrap networkBootstrap = NetworkBootstrap.instance;

        if (networkBootstrap != null &&
            (networkBootstrap.IsConnected ||
             networkBootstrap.IsClient ||
             networkBootstrap.IsAuthority ||
             networkBootstrap.ConnectionState != NetworkConnectionState.Offline))
        {
            Task<bool> shutdownTask = networkBootstrap.ShutdownAsync();
            float shutdownTimeoutTime = Time.realtimeSinceStartup + FailureCleanupTimeoutSeconds;

            while (!shutdownTask.IsCompleted && Time.realtimeSinceStartup < shutdownTimeoutTime)
            {
                yield return null;
            }
        }

        GameSceneManager.instance?.ReturnToMainSceneAfterFailure();
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
