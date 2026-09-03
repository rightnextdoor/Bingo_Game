using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkLobbyManager : MonoBehaviour
{
    #region Fields

    public static NetworkLobbyManager instance;

    private const int BoardCollectionBatchSize = 10;
    private const int LobbyWorkBatchSize = 10;
    private const int InitialSyncBatchSize = 10;

    private readonly List<Lobby> lobbies = new List<Lobby>();
    private readonly Dictionary<string, string> relayJoinCodeByLobbyId = new Dictionary<string, string>();
    private readonly Dictionary<string, Coroutine> lobbyRuntimeRoutines = new Dictionary<string, Coroutine>();
    private readonly Dictionary<string, Queue<string>> pendingMemberPublicationsByLobbyId = new Dictionary<string, Queue<string>>();
    private readonly Dictionary<string, HashSet<string>> pendingMemberPublicationIdsByLobbyId = new Dictionary<string, HashSet<string>>();
    private readonly Dictionary<ulong, Coroutine> initialSyncRoutines = new Dictionary<ulong, Coroutine>();
    private readonly HashSet<ulong> initialSyncClientIds = new HashSet<ulong>();
    private readonly Dictionary<string, long> lobbyRevisions = new Dictionary<string, long>();
    private readonly Dictionary<string, double> pendingJoinStartedTimeByUserId = new Dictionary<string, double>();

    private bool isReady;
    private bool isSubscribedToConnectionRegistry;
    private bool isSubscribedToPlayerProfileConnection;

    private NetworkRoot networkRoot;
    private NetworkBootstrap networkBootstrap;
    private NetworkConnectionRegistry connectionRegistry;

    public bool IsReady => isReady;
    public IReadOnlyList<Lobby> Lobbies => lobbies;
    public bool HasActiveLobbies => lobbies.Count > 0;

    public event Action<Lobby> LobbyCreated;
    public event Action<Lobby> LobbyFinalCountdownCompleted;
    public event Action<Lobby> LobbyGameCreated;
    public event Action<Lobby, LobbyCloseReason> LobbyClosed;

    #endregion

    #region Unity Methods

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        networkRoot = GetComponentInParent<NetworkRoot>();

        if (networkRoot == null || !networkRoot.IsPrimaryInstance)
        {
            enabled = false;
            return;
        }

        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }

        instance = this;
        isReady = false;
    }

    private IEnumerator Start()
    {
        while (!CanInitialize())
        {
            yield return null;
        }

        networkRoot = NetworkRoot.instance;
        networkBootstrap = NetworkBootstrap.instance;
        connectionRegistry = NetworkConnectionRegistry.instance;

        SubscribeToConnectionRegistry();
        SubscribeToPlayerProfileConnection();

        isReady = true;
    }

    private void OnDestroy()
    {
        UnsubscribeFromConnectionRegistry();
        UnsubscribeFromPlayerProfileConnection();
        UnsubscribeFromAllLobbyControllers();
        StopAllLobbyRuntimeRoutines();
        StopAllInitialSyncRoutines();

        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Lobby Entry

    public LobbyEntryResult ProcessAuthorityLobbyEntry(LobbySetupData lobbySetupData, ulong senderClientId)
    {
        if (networkBootstrap == null || !networkBootstrap.IsAuthority)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.ServiceUnavailable, "This process is not the network authority.");
        }

        if (connectionRegistry == null || !connectionRegistry.IsReady)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.ServiceUnavailable, "The network connection registry is not ready.");
        }

        if (!connectionRegistry.TryGetBingoUserId(senderClientId, out string registeredUserId))
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.UserMissing, "The connected user could not be resolved.");
        }

        if (lobbySetupData?.userData == null || !lobbySetupData.userData.HasUser)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.UserMissing, "The user information is missing.");
        }

        if (lobbySetupData.userData.userId != registeredUserId)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.UserMissing, "The lobby user does not match the connected user.");
        }

        Lobby existingUserLobby = FindUserLobby(registeredUserId);

        if (lobbySetupData.startFreshEntry)
        {
            NetworkGameSessionManager.instance?.RemovePlayerFromAnyGame(registeredUserId);

            LobbyExitResult exitResult = RemovePlayerFromLobby(
                registeredUserId,
                LobbyPlayerExitReason.VoluntaryLeave);

            if (exitResult == null || !exitResult.success)
            {
                return LobbyEntryResult.Failed(
                    LobbyEntryFailureType.LobbyJoinFailed,
                    exitResult?.failureMessage ?? "The previous network lobby could not be cleared.");
            }

            existingUserLobby = null;
        }

        if (existingUserLobby != null)
        {
            return BuildNetworkEntryResult(existingUserLobby);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (lobbySetupData.isGameSimulation)
        {
            return ProcessGameSimulationLobbyEntry(lobbySetupData);
        }
#endif

        switch (lobbySetupData.playMode)
        {
            case MainMenuPlayMode.Online:
                Lobby selectedLobby =
                    FindOrCreateOnlineLobby(lobbySetupData);

                if (selectedLobby == null)
                {
                    return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyCreationFailed, "The Online lobby could not be created.");
                }

                return AddPlayerToLobby(selectedLobby, lobbySetupData.userData, false);

            case MainMenuPlayMode.Custom:
                return ProcessCustomLobbyEntry(lobbySetupData);

            default:
                return LobbyEntryResult.Failed(LobbyEntryFailureType.InvalidSetupData, "The network lobby mode is not valid.");
        }
    }

    #endregion

    #region Game Simulation Lobby Entry

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private LobbyEntryResult ProcessGameSimulationLobbyEntry(LobbySetupData lobbySetupData)
    {
        int simulationPlayerNumber = lobbySetupData.gameSimulationPlayerNumber;

        if (simulationPlayerNumber < 1 || simulationPlayerNumber > 4)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.InvalidSetupData,
                "The Game simulation player number is invalid.");
        }

        if (simulationPlayerNumber != 1)
        {
            Lobby authoritySimulationLobby = FindOpenGameSimulationLobby();

            if (authoritySimulationLobby == null)
            {
                return LobbyEntryResult.Failed(
                    LobbyEntryFailureType.LobbyNotFound,
                    "Player 1's Game simulation lobby is not ready yet.");
            }

            return AddPlayerToLobby(
                authoritySimulationLobby,
                lobbySetupData.userData,
                false);
        }

        if (lobbySetupData.playMode == MainMenuPlayMode.Custom)
        {
            return ProcessCustomLobbyEntry(lobbySetupData);
        }

        if (lobbySetupData.playMode == MainMenuPlayMode.Online)
        {
            Lobby selectedLobby = FindOrCreateOnlineLobby(lobbySetupData);

            if (selectedLobby == null)
            {
                return LobbyEntryResult.Failed(
                    LobbyEntryFailureType.LobbyCreationFailed,
                    "Player 1's Online Game simulation lobby could not be created.");
            }

            return AddPlayerToLobby(selectedLobby, lobbySetupData.userData, false);
        }

        return LobbyEntryResult.Failed(
            LobbyEntryFailureType.InvalidSetupData,
            "The network Game simulation lobby mode is not valid.");
    }

    private Lobby FindOpenGameSimulationLobby()
    {
        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby lobby = lobbies[i];

            if (lobby == null ||
                !lobby.isGameSimulation ||
                (lobby.playMode != MainMenuPlayMode.Online &&
                 lobby.playMode != MainMenuPlayMode.Custom) ||
                lobby.lobbyState != LobbyState.Open ||
                lobby.Controller == null ||
                lobby.Controller.IsJoinLocked ||
                lobby.Controller.IsFull)
            {
                continue;
            }

            return lobby;
        }

        return null;
    }

#endif

    #endregion

    #region Lobby Exit

    public LobbyExitResult ProcessAuthorityLobbyExit(ulong senderClientId)
    {
        if (!TryResolveConnectedUser(senderClientId, out string userId, out LobbyExitResult failureResult))
        {
            return failureResult;
        }

        StopInitialSync(senderClientId);
        return RemovePlayerFromLobby(userId, LobbyPlayerExitReason.VoluntaryLeave);
    }



    public LobbyExitResult ProcessAuthorityKickPlayer(ulong senderClientId, string targetUserId)
    {
        if (!TryResolveConnectedUser(senderClientId, out string requesterUserId, out LobbyExitResult failureResult))
        {
            return failureResult;
        }

        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            return LobbyExitResult.Failed(targetUserId, LobbyPlayerExitReason.Kicked, "The target player UserId is missing.");
        }

        Lobby lobby = FindUserLobby(requesterUserId);

        if (lobby?.Controller == null)
        {
            return LobbyExitResult.Failed(targetUserId, LobbyPlayerExitReason.Kicked, "The requester is not in a lobby.");
        }

        ulong targetClientId = default;
        bool hasTargetConnection = connectionRegistry != null && connectionRegistry.TryGetClientId(targetUserId, out targetClientId);

        LobbyExitResult result = lobby.Controller.KickPlayer(requesterUserId, targetUserId);

        if (result.success && hasTargetConnection)
        {
            StopInitialSync(targetClientId);
            NetworkLobbyConnection.TrySendForcedLobbyExit(targetClientId, LobbyExitNotification.Kicked(lobby.GetLobbyId()));
        }

        return result;
    }
    public void ProcessAuthorityLobbySceneReady(ulong clientId)
    {
        if (!TryGetConnectedUserId(clientId, out string userId))
        {
            return;
        }

        Lobby lobby = FindUserLobby(userId);

        if (lobby?.Controller == null)
        {
            return;
        }

        LobbyPlayerData playerData = lobby.Controller.GetPlayer(userId);
        bool wasSceneReady = playerData != null && playerData.isLobbySceneReady;

        if (!lobby.Controller.SetLobbySceneReady(userId, true) || wasSceneReady)
        {
            return;
        }

        pendingJoinStartedTimeByUserId.Remove(userId);
        QueueMemberPublication(lobby, userId);
    }

    public void ProcessAuthorityLobbyInitialSync(ulong clientId)
    {
        if (!TryGetConnectedUserId(clientId, out string userId))
        {
            return;
        }

        Lobby lobby = FindUserLobby(userId);

        if (lobby?.Controller == null)
        {
            return;
        }

        StartInitialSync(lobby, clientId);
    }

    private LobbyExitResult RemovePlayerFromLobby(string userId, LobbyPlayerExitReason exitReason)
    {
        pendingJoinStartedTimeByUserId.Remove(userId);

        Lobby lobby = FindUserLobby(userId);

        if (lobby?.Controller == null)
        {
            return LobbyExitResult.Succeeded(userId, exitReason, false, 0, false, LobbyCloseReason.None);
        }

        return lobby.Controller.RemovePlayer(userId, exitReason);
    }

    private void OnLobbyPlayerExitProcessed(LobbyController controller, LobbyExitResult exitResult)
    {
        if (controller == null || exitResult == null || !exitResult.success)
        {
            return;
        }

        Lobby lobby = FindLobbyByController(controller);

        if (lobby == null)
        {
            return;
        }

        if (exitResult.shouldCloseLobby)
        {
            CloseAndDeleteLobby(lobby, exitResult.closeReason);
            return;
        }

        BroadcastPlayerLeft(lobby, exitResult.userId);
    }

    private void CloseAndDeleteLobby(Lobby lobby, LobbyCloseReason closeReason)
    {
        if (lobby?.Controller == null)
        {
            return;
        }

        string lobbyId = lobby.GetLobbyId();

        ClearPendingJoinTrackingForLobby(lobby);
        NetworkGameSessionManager.instance?.DeleteGameForLobby(lobbyId);

        List<string> remainingUserIds =
            lobby.Controller.CloseLobby(closeReason);

        if (closeReason == LobbyCloseReason.HostLeft)
        {
            for (int i = 0; i < remainingUserIds.Count; i++)
            {
                string userId = remainingUserIds[i];

                if (connectionRegistry == null || !connectionRegistry.TryGetClientId(userId, out ulong clientId))
                {
                    continue;
                }

                NetworkLobbyConnection.TrySendForcedLobbyExit(clientId, LobbyExitNotification.LobbyClosed(lobbyId, LobbyCloseReason.HostLeft));
            }
        }

        LobbyClosed?.Invoke(lobby, closeReason);
        DeleteLobby(lobby);
    }

    private void DeleteLobby(Lobby lobby)
    {
        if (lobby == null)
        {
            return;
        }

        ClearPendingJoinTrackingForLobby(lobby);

        if (lobby.Controller != null)
        {
            lobby.Controller.PlayerExitProcessed -= OnLobbyPlayerExitProcessed;
            lobby.Controller.FinalCountdownStarted -= OnLobbyFinalCountdownStarted;
        }

        StopLobbyRuntime(lobby);
        NetworkGameSessionManager.instance?.DeleteGameForLobby(lobby.GetLobbyId(), false);
        MultiplayerNetworkScheduler.instance?.ClearSession(lobby.GetLobbyId());
        relayJoinCodeByLobbyId.Remove(lobby.GetLobbyId());
        lobbyRevisions.Remove(lobby.GetLobbyId());
        pendingMemberPublicationsByLobbyId.Remove(lobby.GetLobbyId());
        pendingMemberPublicationIdsByLobbyId.Remove(lobby.GetLobbyId());
        lobbies.Remove(lobby);
    }

    private bool TryResolveConnectedUser(ulong clientId, out string userId, out LobbyExitResult failureResult)
    {
        userId = string.Empty;
        failureResult = null;

        if (networkBootstrap == null || !networkBootstrap.IsAuthority)
        {
            failureResult = LobbyExitResult.Failed(userId, LobbyPlayerExitReason.VoluntaryLeave, "This process is not the network authority.");

            return false;
        }

        if (connectionRegistry == null || !connectionRegistry.IsReady)
        {
            failureResult = LobbyExitResult.Failed(userId, LobbyPlayerExitReason.VoluntaryLeave, "The network connection registry is not ready.");

            return false;
        }

        if (!connectionRegistry.TryGetBingoUserId(clientId, out userId))
        {
            failureResult = LobbyExitResult.Failed(userId, LobbyPlayerExitReason.VoluntaryLeave, "The connected user could not be resolved.");

            return false;
        }

        return true;
    }

    #endregion

    #region Custom Lobby Entry

    public bool TryPrepareCustomLobbySearch(LobbySetupData lobbySetupData, out string relayJoinCode, out LobbyEntryResult failureResult)
    {
        relayJoinCode = string.Empty;
        failureResult = null;

        if (!IsCustomLobbySearch(lobbySetupData))
        {
            return true;
        }

        if (MultiplayerPlayModeTestContext.IsActive)
        {
            return true;
        }

        CustomSearchLobbySetupData searchSetupData =
            lobbySetupData.customSetupData.searchSetupData;

        Lobby lobby = FindCustomLobby(searchSetupData?.lobbyCode);

        if (lobby?.Controller == null)
        {
            failureResult = LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyNotFound, "The Custom lobby could not be found.");

            return false;
        }

        LobbyController controller = lobby.Controller;

        if (!controller.IsPasswordValid(searchSetupData?.password))
        {
            failureResult = LobbyEntryResult.Failed(LobbyEntryFailureType.InvalidPassword, "The Custom lobby password is incorrect.");

            return false;
        }

        if (controller.IsJoinLocked)
        {
            failureResult = LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyStarted, "The lobby has already started.");
            return false;
        }

        if (controller.IsFull)
        {
            failureResult = LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyFull, "The Custom lobby is full.");

            return false;
        }

        if (!TryGetRelayJoinCode(lobby, out relayJoinCode))
        {
            failureResult = LobbyEntryResult.Failed(
                LobbyEntryFailureType.NetworkConnectionFailed,
                "The Custom lobby connection information was not available.");

            return false;
        }

        return true;
    }

    private LobbyEntryResult ProcessCustomLobbyEntry(LobbySetupData lobbySetupData)
    {
        CustomLobbySetupData customSetupData =
            lobbySetupData.customSetupData;

        if (customSetupData == null)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.InvalidSetupData, "The Custom lobby setup data is missing.");
        }

        switch (customSetupData.actionType)
        {
            case CustomLobbyActionType.HostLobby:
                Lobby newLobby =
                    CreateCustomHostLobby(lobbySetupData);

                if (newLobby == null)
                {
                    return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyCreationFailed, "The Custom lobby could not be created.");
                }

                return AddPlayerToLobby(newLobby, lobbySetupData.userData, true);

            case CustomLobbyActionType.SearchLobby:
                return ProcessCustomLobbySearch(lobbySetupData);

            default:
                return LobbyEntryResult.Failed(LobbyEntryFailureType.InvalidSetupData, "The Custom lobby action is invalid.");
        }
    }

    private LobbyEntryResult ProcessCustomLobbySearch(LobbySetupData lobbySetupData)
    {
        CustomSearchLobbySetupData searchSetupData =
            lobbySetupData?.customSetupData?.searchSetupData;

        if (searchSetupData == null)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.InvalidSetupData, "The Custom lobby search data is missing.");
        }

        Lobby lobby = FindCustomLobby(searchSetupData.lobbyCode);

        if (lobby == null && MultiplayerPlayModeTestContext.IsActive && string.IsNullOrWhiteSpace(searchSetupData.lobbyCode))
        {
            lobby = FindSimulationCustomLobby();
        }

        if (lobby?.Controller == null)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyNotFound, "The Custom lobby could not be found.");
        }

        LobbyController controller = lobby.Controller;

        if (!controller.IsPasswordValid(searchSetupData.password))
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.InvalidPassword, "The Custom lobby password is incorrect.");
        }

        if (controller.IsJoinLocked)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyStarted, "The lobby has already started.");
        }

        if (controller.IsFull)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyFull, "The Custom lobby is full.");
        }

        return AddPlayerToLobby(lobby, lobbySetupData.userData, false);
    }

    private Lobby FindSimulationCustomLobby()
    {
        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby lobby = lobbies[i];

            if (lobby == null ||
                lobby.playMode != MainMenuPlayMode.Custom ||
                lobby.lobbyState != LobbyState.Open ||
                lobby.Controller == null ||
                lobby.Controller.IsJoinLocked ||
                lobby.Controller.IsFull)
            {
                continue;
            }

            return lobby;
        }

        return null;
    }

    private Lobby CreateCustomHostLobby(LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null || networkBootstrap == null)
        {
            return null;
        }

        Lobby lobby =
            CreateLobby(lobbySetupData);

        if (lobby?.Controller == null || string.IsNullOrWhiteSpace(lobby.Controller.RoomCode))
        {
            DeleteLobby(lobby);
            return null;
        }

        string relayJoinCode =
            networkBootstrap.RelayJoinCode;

        if (!string.IsNullOrWhiteSpace(relayJoinCode))
        {
            relayJoinCodeByLobbyId[
                lobby.GetLobbyId()] =
                    relayJoinCode;
        }

        return lobby;
    }

    private Lobby FindCustomLobby(string requestedRoomCode)
    {
        if (lobbies.Count == 0 || string.IsNullOrWhiteSpace(requestedRoomCode))
        {
            return null;
        }

        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby lobby = lobbies[i];

            if (lobby == null || lobby.playMode != MainMenuPlayMode.Custom)
            {
                continue;
            }

            LobbyController controller = lobby.Controller;

            if (controller != null && controller.MatchesRoomCode(requestedRoomCode))
            {
                return lobby;
            }
        }

        return null;
    }

    private bool IsCustomRoomCodeAvailable(string roomCode)
    {
        return FindCustomLobby(roomCode) == null;
    }

    private bool TryGetRelayJoinCode(Lobby lobby, out string relayJoinCode)
    {
        relayJoinCode = string.Empty;

        if (lobby == null)
        {
            return false;
        }

        if (!relayJoinCodeByLobbyId.TryGetValue(lobby.GetLobbyId(), out string storedRelayJoinCode))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(storedRelayJoinCode))
        {
            return false;
        }

        relayJoinCode = storedRelayJoinCode;
        return true;
    }

    private bool IsCustomLobbySearch(LobbySetupData lobbySetupData)
    {
        return lobbySetupData != null &&
               lobbySetupData.playMode ==
                   MainMenuPlayMode.Custom &&
               lobbySetupData.customSetupData != null &&
               lobbySetupData.customSetupData.actionType ==
                   CustomLobbyActionType.SearchLobby;
    }

    #endregion

    #region Online Lobby Entry

    private Lobby FindOrCreateOnlineLobby(LobbySetupData lobbySetupData)
    {
        OnlineLobbySetupData onlineSetupData = lobbySetupData?.onlineSetupData;

        if (onlineSetupData == null)
        {
            return null;
        }

        switch (onlineSetupData.searchType)
        {
            case OnlineSearchType.QuickPlay:
                return FindOrCreateQuickPlayLobby(lobbySetupData);

            case OnlineSearchType.CustomSearch:
                return FindOrCreateOnlineCustomSearchLobby(lobbySetupData);

            default:
                return null;
        }
    }

    private Lobby FindOrCreateQuickPlayLobby(LobbySetupData lobbySetupData)
    {
        OnlineLobbySetupData onlineSetupData = lobbySetupData?.onlineSetupData;

        if (onlineSetupData == null)
        {
            return null;
        }

        List<Lobby> matchingGameModeLobbies = FindEligibleOnlineLobbiesByGameMode(onlineSetupData.gameModeType);

        if (matchingGameModeLobbies.Count > 0)
        {
            return GetRandomLobby(matchingGameModeLobbies);
        }

        return CreateLobby(lobbySetupData);
    }

    private Lobby FindOrCreateOnlineCustomSearchLobby(LobbySetupData lobbySetupData)
    {
        OnlineLobbySetupData onlineSetupData = lobbySetupData?.onlineSetupData;

        if (onlineSetupData == null)
        {
            return null;
        }

        List<Lobby> matchingGameModeLobbies = FindEligibleOnlineLobbiesByGameMode(onlineSetupData.gameModeType);

        List<Lobby> matchingBallCountLobbies = FindOnlineLobbiesByBallCount(matchingGameModeLobbies, onlineSetupData.ballCountType);

        if (matchingBallCountLobbies.Count > 0)
        {
            return GetRandomLobby(matchingBallCountLobbies);
        }

        return CreateLobby(lobbySetupData);
    }

    private List<Lobby> FindEligibleOnlineLobbiesByGameMode(BingoGameModeType gameModeType)
    {
        List<Lobby> matchingLobbies = new List<Lobby>();

        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby lobby = lobbies[i];

            if (!IsEligibleOnlineLobby(lobby))
            {
                continue;
            }

            if (lobby.Controller.GameModeType != gameModeType)
            {
                continue;
            }

            matchingLobbies.Add(lobby);
        }

        return matchingLobbies;
    }

    private List<Lobby> FindOnlineLobbiesByBallCount(List<Lobby> gameModeLobbies, BingoBallCountType ballCountType)
    {
        List<Lobby> matchingLobbies = new List<Lobby>();

        if (gameModeLobbies == null)
        {
            return matchingLobbies;
        }

        for (int i = 0; i < gameModeLobbies.Count; i++)
        {
            Lobby lobby = gameModeLobbies[i];

            if (lobby?.Controller == null || lobby.Controller.BallCountType != ballCountType)
            {
                continue;
            }

            matchingLobbies.Add(lobby);
        }

        return matchingLobbies;
    }

    private Lobby GetRandomLobby(List<Lobby> matchingLobbies)
    {
        if (matchingLobbies == null || matchingLobbies.Count == 0)
        {
            return null;
        }

        int randomIndex = UnityEngine.Random.Range(0, matchingLobbies.Count);
        return matchingLobbies[randomIndex];
    }

    private bool IsEligibleOnlineLobby(Lobby lobby)
    {
        return lobby != null &&
               lobby.playMode == MainMenuPlayMode.Online &&
               lobby.lobbyState == LobbyState.Open &&
               lobby.Controller != null &&
               !lobby.Controller.IsJoinLocked &&
               !lobby.Controller.IsFull;
    }

    #endregion

    #region Lobby Data

    private LobbyEntryResult AddPlayerToLobby(Lobby lobby, UserData userData, bool isHost)
    {
        if (lobby?.Controller == null)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyNotFound, "The lobby could not be found.");
        }

        if (userData == null || !userData.HasUser)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.UserMissing, "The user information is missing.");
        }

        LobbyController controller = lobby.Controller;

        if (controller.HasPlayer(userData.userId))
        {
            return BuildNetworkEntryResult(lobby);
        }

        if (controller.IsJoinLocked)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyStarted, "The lobby has already started.");
        }

        if (controller.IsFull)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyFull, "The lobby is full.");
        }

        LobbyPlayerData playerData = new LobbyPlayerData(userData, isHost);
        playerData.isLobbySceneReady = false;

        if (!controller.AddPlayer(playerData))
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyJoinFailed, "The player could not be added to the lobby.");
        }

        if (userData.userTag != UserTag.Bot)
        {
            pendingJoinStartedTimeByUserId[userData.userId] = LobbyTimer.GetCurrentTime();
        }

        return BuildNetworkEntryResult(lobby);
    }

    private LobbyEntryResult BuildNetworkEntryResult(Lobby lobby)
    {
        if (lobby?.Controller == null)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyNotFound, "The lobby could not be found.");
        }

        LobbyViewData lobbyViewData = lobby.Controller.BuildViewData(false);
        long revision = GetLobbyRevision(lobby);
        LobbyBoardCollectionData lobbyBoardData = new LobbyBoardCollectionData(lobby.GetLobbyId(), revision, new List<LobbyPlayerBoardViewData>());

        if (lobbyViewData == null)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyJoinFailed, "The lobby state could not be created.");
        }

        return LobbyEntryResult.Succeeded(lobby.GetLobbyId(), revision, lobbyViewData, lobbyBoardData);
    }

    private Lobby CreateLobby(LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null)
        {
            return null;
        }

        Lobby lobby = new Lobby(lobbySetupData, IsCustomRoomCodeAvailable);

        lobby.Controller.PlayerExitProcessed += OnLobbyPlayerExitProcessed;
        lobby.Controller.FinalCountdownStarted += OnLobbyFinalCountdownStarted;
        lobby.Controller.SetBotUserProvider(GetNetworkBotUsers);

        lobbies.Add(lobby);
        lobbyRevisions[lobby.GetLobbyId()] = 0;
        pendingMemberPublicationsByLobbyId[lobby.GetLobbyId()] = new Queue<string>();
        pendingMemberPublicationIdsByLobbyId[lobby.GetLobbyId()] = new HashSet<string>();
        StartLobbyRuntime(lobby);
        LobbyCreated?.Invoke(lobby);

        return lobby;
    }

    private void OnLobbyFinalCountdownStarted(LobbyController controller)
    {
        if (controller == null || networkBootstrap == null || !networkBootstrap.IsAuthority)
        {
            return;
        }

        Lobby lobby = FindLobbyByController(controller);

        if (lobby == null)
        {
            return;
        }

        if (GameSessionManager.instance != null &&
            LobbyManager.instance != null &&
            string.Equals(LobbyManager.instance.CurrentLobbyId, lobby.GetLobbyId(), StringComparison.Ordinal))
        {
            GameSessionManager.instance.PrepareForGameCreation(lobby.GetLobbyId(), SessionRuntimeType.Network);
        }

        if (NetworkGameSessionManager.instance == null || !NetworkGameSessionManager.instance.IsReady)
        {
            HandleNetworkGameCreationFailure(lobby, GameSessionResult.Failed(
                GameSessionOperationType.Create,
                GameSessionFailureType.ServiceUnavailable,
                "The authoritative network Game session manager is not ready.",
                lobbyId: lobby.GetLobbyId()));
            return;
        }

        GameSessionSetupData setupData = GameSessionSetupData.FromLobby(lobby);
        GameSessionResult result = NetworkGameSessionManager.instance.CreateGame(setupData);

        if (result == null || !result.success)
        {
            HandleNetworkGameCreationFailure(lobby, result);
            return;
        }

        BroadcastGameCreationResult(lobby, result);
        LobbyGameCreated?.Invoke(lobby);
    }

    private void HandleNetworkGameCreationFailure(Lobby lobby, GameSessionResult result)
    {
        if (lobby?.Controller == null)
        {
            return;
        }

        GameSessionResult failureResult = result ?? GameSessionResult.Failed(
            GameSessionOperationType.Create,
            GameSessionFailureType.GameCreationFailed,
            "The network Game could not be created.",
            lobbyId: lobby.GetLobbyId());

        BroadcastGameCreationResult(lobby, failureResult);

        if (!lobby.Controller.ResetAfterGameCreationFailure())
        {
            return;
        }

        IReadOnlyList<LobbyPlayerData> players = lobby.Controller.Players;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData?.userData == null || playerData.userData.userTag == UserTag.Bot)
            {
                continue;
            }

            BroadcastPlayerReadyChanged(lobby, playerData.userData.userId, false);
        }
    }

    private void BroadcastGameCreationResult(Lobby lobby, GameSessionResult result)
    {
        if (lobby == null || result == null)
        {
            return;
        }

        SendToLobbyPlayers(lobby, clientId => NetworkGameSessionConnection.TrySendGameCreationResult(clientId, result));
    }

    private IReadOnlyList<UserData> GetNetworkBotUsers()
    {
        if (NetworkBotManager.instance == null || !NetworkBotManager.instance.IsReady)
        {
            return new List<UserData>();
        }

        return NetworkBotManager.instance.CreateBotCandidateCopies();
    }

    private long GetLobbyRevision(Lobby lobby)
    {
        if (lobby == null || !lobbyRevisions.TryGetValue(lobby.GetLobbyId(), out long revision))
        {
            return 0;
        }

        return revision;
    }

    private long GetNextLobbyRevision(Lobby lobby)
    {
        if (lobby == null)
        {
            return 0;
        }

        string lobbyId = lobby.GetLobbyId();
        long nextRevision = GetLobbyRevision(lobby) + 1;
        lobbyRevisions[lobbyId] = nextRevision;
        return nextRevision;
    }

    private void SendToLobbyPlayers(Lobby lobby, System.Action<ulong> sendAction, string excludedUserId = null)
    {
        HashSet<string> excludedUserIds = string.IsNullOrWhiteSpace(excludedUserId) ? null : new HashSet<string> { excludedUserId };
        SendToLobbyPlayers(lobby, sendAction, excludedUserIds);
    }

    private void SendToLobbyPlayers(Lobby lobby, System.Action<ulong> sendAction, ISet<string> excludedUserIds)
    {
        if (lobby?.Controller == null || connectionRegistry == null || !connectionRegistry.IsReady || sendAction == null)
        {
            return;
        }

        IReadOnlyList<LobbyPlayerData> players = lobby.Controller.Players;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];
            string userId = playerData?.userData?.userId;

            if (playerData == null || string.IsNullOrWhiteSpace(userId) ||
                (excludedUserIds != null && excludedUserIds.Contains(userId)) ||
                !connectionRegistry.TryGetClientId(userId, out ulong clientId) ||
                initialSyncClientIds.Contains(clientId))
            {
                continue;
            }

            sendAction(clientId);
        }
    }

    private void BroadcastPlayerJoined(Lobby lobby, string userId, string excludedUserId = null)
    {
        if (lobby?.Controller == null || string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        LobbyPlayerData playerData = lobby.Controller.GetPlayer(userId);

        if (playerData == null)
        {
            return;
        }

        BroadcastJoinedPlayers(lobby, new List<LobbyPlayerViewData> { new LobbyPlayerViewData(playerData) }, excludedUserId);
    }

    private void BroadcastPlayerLeft(Lobby lobby, string userId)
    {
        if (lobby?.Controller == null || string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        LobbyViewData viewData = lobby.Controller.BuildViewData(false);

        if (viewData == null)
        {
            return;
        }

        long revision = GetNextLobbyRevision(lobby);
        LobbyPlayerLeftData data = new LobbyPlayerLeftData(lobby.GetLobbyId(), revision, userId, viewData.playerCount, viewData.botCount);
        SendToLobbyPlayers(lobby, clientId => NetworkLobbyConnection.TrySendPlayerLeft(clientId, data));
    }

    private void BroadcastPlayerReadyChanged(Lobby lobby, string userId, bool isReady)
    {
        if (lobby == null || string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        long revision = GetNextLobbyRevision(lobby);
        LobbyPlayerReadyChangedData data = new LobbyPlayerReadyChangedData(lobby.GetLobbyId(), revision, userId, isReady);
        SendToLobbyPlayers(lobby, clientId => NetworkLobbyConnection.TrySendPlayerReadyChanged(clientId, data));
    }

    private void BroadcastLobbySettingsChanged(Lobby lobby, LobbyViewData viewData = null)
    {
        if (lobby?.Controller == null)
        {
            return;
        }

        viewData ??= lobby.Controller.BuildViewData(false);

        if (viewData == null)
        {
            return;
        }

        long revision = GetNextLobbyRevision(lobby);
        LobbySettingsChangedData data = new LobbySettingsChangedData(lobby.GetLobbyId(), revision, viewData);
        SendToLobbyPlayers(lobby, clientId => NetworkLobbyConnection.TrySendLobbySettingsChanged(clientId, data));
    }

    private void BroadcastLobbyStateChanged(Lobby lobby)
    {
        if (lobby?.Controller == null)
        {
            return;
        }

        LobbyViewData viewData = lobby.Controller.BuildViewData(false);

        if (viewData == null)
        {
            return;
        }

        long revision = GetNextLobbyRevision(lobby);
        LobbyStateChangedData data = new LobbyStateChangedData(lobby.GetLobbyId(), revision, viewData);
        SendToLobbyPlayers(lobby, clientId => NetworkLobbyConnection.TrySendLobbyStateChanged(clientId, data));
    }

    private bool HasLobbyStateChanged(Lobby lobby, LobbyController controller, LobbyState previousState, bool previousTimerActive, double previousTimerEndTime)
    {
        return lobby != null && controller != null &&
               (lobby.lobbyState != previousState || controller.IsTimerActive != previousTimerActive || controller.TimerEndTime != previousTimerEndTime);
    }

    private List<string> GetAddedVisibleUserIds(LobbyViewData previousViewData, LobbyViewData currentViewData)
    {
        HashSet<string> previousUserIds = BuildVisibleUserIdSet(previousViewData);
        List<string> addedUserIds = new List<string>();

        if (currentViewData?.players == null)
        {
            return addedUserIds;
        }

        for (int i = 0; i < currentViewData.players.Count; i++)
        {
            string userId = currentViewData.players[i]?.userId;

            if (!string.IsNullOrWhiteSpace(userId) && !previousUserIds.Contains(userId))
            {
                addedUserIds.Add(userId);
            }
        }

        return addedUserIds;
    }

    private List<string> GetRemovedVisibleUserIds(LobbyViewData previousViewData, LobbyViewData currentViewData)
    {
        HashSet<string> currentUserIds = BuildVisibleUserIdSet(currentViewData);
        List<string> removedUserIds = new List<string>();

        if (previousViewData?.players == null)
        {
            return removedUserIds;
        }

        for (int i = 0; i < previousViewData.players.Count; i++)
        {
            string userId = previousViewData.players[i]?.userId;

            if (!string.IsNullOrWhiteSpace(userId) && !currentUserIds.Contains(userId))
            {
                removedUserIds.Add(userId);
            }
        }

        return removedUserIds;
    }

    private HashSet<string> BuildVisibleUserIdSet(LobbyViewData viewData)
    {
        HashSet<string> userIds = new HashSet<string>();

        if (viewData?.players == null)
        {
            return userIds;
        }

        for (int i = 0; i < viewData.players.Count; i++)
        {
            string userId = viewData.players[i]?.userId;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                userIds.Add(userId);
            }
        }

        return userIds;
    }

    private void QueueMemberPublication(Lobby lobby, string userId)
    {
        if (lobby == null || string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        string lobbyId = lobby.GetLobbyId();

        if (!pendingMemberPublicationsByLobbyId.TryGetValue(lobbyId, out Queue<string> pendingUserIds))
        {
            pendingUserIds = new Queue<string>();
            pendingMemberPublicationsByLobbyId[lobbyId] = pendingUserIds;
        }

        if (!pendingMemberPublicationIdsByLobbyId.TryGetValue(lobbyId, out HashSet<string> queuedUserIds))
        {
            queuedUserIds = new HashSet<string>();
            pendingMemberPublicationIdsByLobbyId[lobbyId] = queuedUserIds;
        }

        if (queuedUserIds.Add(userId))
        {
            pendingUserIds.Enqueue(userId);
        }
    }

    private void ProcessPendingMemberPublications(Lobby lobby)
    {
        if (lobby?.Controller == null)
        {
            return;
        }

        string lobbyId = lobby.GetLobbyId();

        if (!pendingMemberPublicationsByLobbyId.TryGetValue(lobbyId, out Queue<string> pendingUserIds) || pendingUserIds.Count == 0)
        {
            return;
        }

        pendingMemberPublicationIdsByLobbyId.TryGetValue(lobbyId, out HashSet<string> queuedUserIds);

        List<LobbyPlayerViewData> playerBatch = new List<LobbyPlayerViewData>();
        List<LobbyPlayerBoardViewData> boardBatch = new List<LobbyPlayerBoardViewData>();
        int processedCount = 0;

        while (pendingUserIds.Count > 0 && processedCount < LobbyWorkBatchSize)
        {
            string userId = pendingUserIds.Dequeue();
            processedCount++;
            queuedUserIds?.Remove(userId);

            LobbyPlayerData playerData = lobby.Controller.GetPlayer(userId);

            if (playerData == null || !playerData.isLobbySceneReady)
            {
                continue;
            }

            playerBatch.Add(new LobbyPlayerViewData(playerData));

            LobbyPlayerBoardViewData boardData = lobby.Controller.GetPlayerBoardViewData(userId);

            if (boardData != null)
            {
                boardBatch.Add(boardData);
            }
        }

        if (playerBatch.Count == 0)
        {
            return;
        }

        BroadcastJoinedPlayers(lobby, playerBatch, (ISet<string>)null);
        BroadcastPlayerBoardCollection(lobby, boardBatch, (ISet<string>)null);
    }

    private void BroadcastJoinedPlayers(Lobby lobby, IReadOnlyList<LobbyPlayerViewData> players, string excludedUserId = null)
    {
        HashSet<string> excludedUserIds = string.IsNullOrWhiteSpace(excludedUserId) ? null : new HashSet<string> { excludedUserId };
        BroadcastJoinedPlayers(lobby, players, excludedUserIds);
    }

    private void BroadcastJoinedPlayers(Lobby lobby, IReadOnlyList<LobbyPlayerViewData> players, ISet<string> excludedUserIds)
    {
        if (lobby?.Controller == null || players == null || players.Count == 0)
        {
            return;
        }

        LobbyViewData currentViewData = lobby.Controller.BuildViewData(false);

        if (currentViewData == null)
        {
            return;
        }

        long revision = GetNextLobbyRevision(lobby);
        LobbyPlayerJoinedBatchData data = new LobbyPlayerJoinedBatchData(lobby.GetLobbyId(), revision, players, currentViewData.playerCount, currentViewData.botCount);
        SendToLobbyPlayers(lobby, clientId => NetworkLobbyConnection.TrySendPlayerJoinedBatch(clientId, data), excludedUserIds);
    }

    private void BroadcastRemovedPlayers(Lobby lobby, IReadOnlyList<string> userIds, LobbyViewData currentViewData)
    {
        if (lobby == null || userIds == null || currentViewData == null)
        {
            return;
        }

        for (int i = 0; i < userIds.Count; i++)
        {
            long revision = GetNextLobbyRevision(lobby);
            LobbyPlayerLeftData data = new LobbyPlayerLeftData(lobby.GetLobbyId(), revision, userIds[i], currentViewData.playerCount, currentViewData.botCount);
            SendToLobbyPlayers(lobby, clientId => NetworkLobbyConnection.TrySendPlayerLeft(clientId, data));
        }
    }

    private Lobby FindUserLobby(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby lobby = lobbies[i];

            if (lobby?.Controller != null && lobby.Controller.HasPlayer(userId))
            {
                return lobby;
            }
        }

        return null;
    }

    private Lobby FindLobbyByController(LobbyController controller)
    {
        if (controller == null)
        {
            return null;
        }

        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby lobby = lobbies[i];

            if (lobby?.Controller == controller)
            {
                return lobby;
            }
        }

        return null;
    }

    public void ProcessAuthorityRerollBoard(ulong clientId)
    {
        if (networkBootstrap == null || !networkBootstrap.IsAuthority || connectionRegistry == null || !connectionRegistry.IsReady)
        {
            return;
        }

        if (!connectionRegistry.TryGetBingoUserId(clientId, out string userId))
        {
            return;
        }

        Lobby lobby = FindUserLobby(userId);

        if (lobby?.Controller == null)
        {
            return;
        }

        if (!lobby.Controller.RerollPlayerBoard(userId))
        {
            return;
        }

        BroadcastPlayerBoardUpdate(lobby, userId);
    }

    public void ProcessAuthorityLobbyResync(ulong clientId)
    {
        ProcessAuthorityLobbyInitialSync(clientId);
    }

    private void SendLobbySyncSnapshot(Lobby lobby, ulong clientId)
    {
        if (lobby?.Controller == null)
        {
            return;
        }

        long revision = GetLobbyRevision(lobby);
        LobbyViewData lobbyViewData = lobby.Controller.BuildViewData();
        LobbyBoardCollectionData lobbyBoardData = lobby.Controller.BuildPlayerBoardCollectionData();

        if (lobbyViewData == null || lobbyBoardData == null)
        {
            return;
        }

        lobbyBoardData.revision = revision;
        LobbySyncSnapshotData snapshotData = new LobbySyncSnapshotData(lobby.GetLobbyId(), revision, lobbyViewData, lobbyBoardData);
        NetworkLobbyConnection.TrySendLobbySyncSnapshot(clientId, snapshotData);
    }

    private void BroadcastPlayerBoardUpdate(Lobby lobby, string userId)
    {
        if (lobby?.Controller == null || connectionRegistry == null || !connectionRegistry.IsReady)
        {
            return;
        }

        LobbyPlayerBoardViewData playerBoard = lobby.Controller.GetPlayerBoardViewData(userId);

        if (playerBoard == null)
        {
            return;
        }

        long revision = GetNextLobbyRevision(lobby);
        LobbyPlayerBoardUpdateData updateData = new LobbyPlayerBoardUpdateData(lobby.GetLobbyId(), revision, userId, playerBoard.boardData);
        SendToLobbyPlayers(lobby, clientId => NetworkLobbyConnection.TrySendPlayerBoardUpdate(clientId, updateData));
    }

    private void BroadcastPlayerBoardCollection(Lobby lobby, IReadOnlyCollection<string> userIds, string excludedUserId = null)
    {
        if (lobby?.Controller == null)
        {
            return;
        }

        List<LobbyPlayerBoardViewData> selectedBoards = new List<LobbyPlayerBoardViewData>();

        if (userIds == null)
        {
            LobbyBoardCollectionData fullCollection = lobby.Controller.BuildPlayerBoardCollectionData();

            if (fullCollection?.boards != null)
            {
                selectedBoards.AddRange(fullCollection.boards);
            }
        }
        else
        {
            foreach (string userId in userIds)
            {
                LobbyPlayerBoardViewData playerBoard = lobby.Controller.GetPlayerBoardViewData(userId);

                if (playerBoard != null)
                {
                    selectedBoards.Add(playerBoard);
                }
            }
        }

        BroadcastPlayerBoardCollection(lobby, selectedBoards, excludedUserId);
    }

    private void BroadcastPlayerBoardCollection(Lobby lobby, IReadOnlyList<LobbyPlayerBoardViewData> selectedBoards, string excludedUserId = null)
    {
        HashSet<string> excludedUserIds = string.IsNullOrWhiteSpace(excludedUserId) ? null : new HashSet<string> { excludedUserId };
        BroadcastPlayerBoardCollection(lobby, selectedBoards, excludedUserIds);
    }

    private void BroadcastPlayerBoardCollection(Lobby lobby, IReadOnlyList<LobbyPlayerBoardViewData> selectedBoards, ISet<string> excludedUserIds)
    {
        if (lobby?.Controller == null || selectedBoards == null || selectedBoards.Count == 0)
        {
            return;
        }

        for (int startIndex = 0; startIndex < selectedBoards.Count; startIndex += BoardCollectionBatchSize)
        {
            int batchCount = Mathf.Min(BoardCollectionBatchSize, selectedBoards.Count - startIndex);
            List<LobbyPlayerBoardViewData> batchBoards = new List<LobbyPlayerBoardViewData>(batchCount);

            for (int i = 0; i < batchCount; i++)
            {
                LobbyPlayerBoardViewData boardData = selectedBoards[startIndex + i];

                if (boardData != null)
                {
                    batchBoards.Add(boardData);
                }
            }

            if (batchBoards.Count == 0)
            {
                continue;
            }

            long revision = GetNextLobbyRevision(lobby);
            LobbyBoardCollectionData batchData = new LobbyBoardCollectionData(lobby.GetLobbyId(), revision, batchBoards);
            SendToLobbyPlayers(lobby, clientId => NetworkLobbyConnection.TrySendPlayerBoardCollection(clientId, batchData), excludedUserIds);
        }
    }



    #endregion

    #region Initial Sync

    private void StartInitialSync(Lobby lobby, ulong clientId)
    {
        StopInitialSync(clientId);
        initialSyncClientIds.Add(clientId);
        initialSyncRoutines[clientId] = StartCoroutine(RunInitialSync(lobby, clientId));
    }

    private IEnumerator RunInitialSync(Lobby lobby, ulong clientId)
    {
        yield return null;

        while (lobby?.Controller != null && lobbies.Contains(lobby))
        {
            LobbyController controller = lobby.Controller;
            long snapshotRevision = GetLobbyRevision(lobby);
            LobbyViewData snapshotView = controller.BuildViewData(false);
            List<string> userIds = new List<string>();
            IReadOnlyList<LobbyPlayerData> players = controller.Players;

            for (int i = 0; i < players.Count; i++)
            {
                string userId = players[i]?.userData?.userId;

                if (!string.IsNullOrWhiteSpace(userId))
                {
                    userIds.Add(userId);
                }
            }

            if (userIds.Count == 0)
            {
                if (snapshotRevision != GetLobbyRevision(lobby))
                {
                    yield return null;
                    continue;
                }

                LobbyInitialSyncBatchData emptyBatch = new LobbyInitialSyncBatchData(lobby.GetLobbyId(), snapshotRevision, true, true, snapshotView, null, null);
                NetworkLobbyConnection.TrySendLobbyInitialSyncBatch(clientId, emptyBatch);
                break;
            }

            bool restartSync = false;

            for (int startIndex = 0; startIndex < userIds.Count; startIndex += InitialSyncBatchSize)
            {
                List<LobbyPlayerViewData> playerBatch = new List<LobbyPlayerViewData>();
                List<LobbyPlayerBoardViewData> boardBatch = new List<LobbyPlayerBoardViewData>();
                int batchCount = Mathf.Min(InitialSyncBatchSize, userIds.Count - startIndex);

                for (int i = 0; i < batchCount; i++)
                {
                    string userId = userIds[startIndex + i];
                    LobbyPlayerData playerData = controller.GetPlayer(userId);

                    if (playerData == null)
                    {
                        continue;
                    }

                    if (playerData.isLobbySceneReady)
                    {
                        playerBatch.Add(new LobbyPlayerViewData(playerData));
                    }

                    LobbyPlayerBoardViewData boardData = controller.GetPlayerBoardViewData(userId);

                    if (boardData != null)
                    {
                        boardBatch.Add(boardData);
                    }
                }

                bool isLastBatch = startIndex + batchCount >= userIds.Count;

                if (isLastBatch && snapshotRevision != GetLobbyRevision(lobby))
                {
                    restartSync = true;
                    break;
                }

                LobbyInitialSyncBatchData batchData = new LobbyInitialSyncBatchData(
                    lobby.GetLobbyId(),
                    snapshotRevision,
                    startIndex == 0,
                    isLastBatch,
                    startIndex == 0 ? snapshotView : null,
                    playerBatch,
                    boardBatch);

                if (!NetworkLobbyConnection.TrySendLobbyInitialSyncBatch(clientId, batchData))
                {
                    restartSync = false;
                    break;
                }

                if (!isLastBatch)
                {
                    yield return null;
                }
            }

            if (!restartSync)
            {
                break;
            }

            yield return null;
        }

        initialSyncClientIds.Remove(clientId);
        initialSyncRoutines.Remove(clientId);
    }

    private void StopInitialSync(ulong clientId)
    {
        if (initialSyncRoutines.TryGetValue(clientId, out Coroutine routine) && routine != null)
        {
            StopCoroutine(routine);
        }

        initialSyncRoutines.Remove(clientId);
        initialSyncClientIds.Remove(clientId);
    }

    private void StopAllInitialSyncRoutines()
    {
        foreach (Coroutine routine in initialSyncRoutines.Values)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        initialSyncRoutines.Clear();
        initialSyncClientIds.Clear();
    }

    #endregion

    #region Pending Lobby Entry

    private void ProcessPendingJoinTimeouts(Lobby lobby)
    {
        if (lobby?.Controller == null || LobbySettings.instance == null)
        {
            return;
        }

        double currentTime = LobbyTimer.GetCurrentTime();
        float timeoutSeconds = LobbySettings.instance.PendingJoinTimeoutSeconds;
        List<string> timedOutUserIds = new List<string>();
        IReadOnlyList<LobbyPlayerData> players = lobby.Controller.Players;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];
            string userId = playerData?.userData?.userId;

            if (playerData == null ||
                playerData.isLobbySceneReady ||
                playerData.userData == null ||
                playerData.userData.userTag == UserTag.Bot ||
                string.IsNullOrWhiteSpace(userId) ||
                !pendingJoinStartedTimeByUserId.TryGetValue(userId, out double startedTime))
            {
                continue;
            }

            if (currentTime - startedTime >= timeoutSeconds)
            {
                timedOutUserIds.Add(userId);
            }
        }

        for (int i = 0; i < timedOutUserIds.Count; i++)
        {
            RemovePendingPlayer(lobby, timedOutUserIds[i], false);
        }
    }

    private void RemovePendingPlayersForLobbyStart(Lobby lobby)
    {
        if (lobby?.Controller == null)
        {
            return;
        }

        List<string> pendingUserIds = new List<string>();
        IReadOnlyList<LobbyPlayerData> players = lobby.Controller.Players;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];
            string userId = playerData?.userData?.userId;

            if (playerData == null ||
                playerData.isLobbySceneReady ||
                playerData.userData == null ||
                playerData.userData.userTag == UserTag.Bot ||
                string.IsNullOrWhiteSpace(userId))
            {
                continue;
            }

            pendingUserIds.Add(userId);
        }

        for (int i = 0; i < pendingUserIds.Count; i++)
        {
            RemovePendingPlayer(lobby, pendingUserIds[i], true);
        }
    }

    private void RemovePendingPlayer(Lobby lobby, string userId, bool lobbyStarted)
    {
        if (lobby?.Controller == null || string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        pendingJoinStartedTimeByUserId.Remove(userId);

        if (connectionRegistry != null && connectionRegistry.TryGetClientId(userId, out ulong clientId))
        {
            StopInitialSync(clientId);

            LobbyExitNotification notification = lobbyStarted
                ? LobbyExitNotification.LobbyStarted(lobby.GetLobbyId())
                : LobbyExitNotification.JoinTimedOut(lobby.GetLobbyId());

            NetworkLobbyConnection.TrySendForcedLobbyExit(clientId, notification);
        }

        lobby.Controller.RemovePlayer(
            userId,
            lobbyStarted ? LobbyPlayerExitReason.LobbyStarted : LobbyPlayerExitReason.JoinTimedOut);
    }

    private void ClearPendingJoinTrackingForLobby(Lobby lobby)
    {
        if (lobby?.Controller == null)
        {
            return;
        }

        IReadOnlyList<LobbyPlayerData> players = lobby.Controller.Players;

        for (int i = 0; i < players.Count; i++)
        {
            string userId = players[i]?.userData?.userId;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                pendingJoinStartedTimeByUserId.Remove(userId);
            }
        }
    }

    #endregion

    #region Lobby Runtime

    private void StartLobbyRuntime(Lobby lobby)
    {
        if (lobby == null || networkBootstrap == null || !networkBootstrap.IsAuthority)
        {
            return;
        }

        string lobbyId = lobby.GetLobbyId();

        if (lobbyRuntimeRoutines.ContainsKey(lobbyId))
        {
            return;
        }

        lobbyRuntimeRoutines[lobbyId] = StartCoroutine(RunLobbyRuntime(lobby, lobbyId));
    }

    private IEnumerator RunLobbyRuntime(Lobby lobby, string lobbyId)
    {
        while (lobby != null && lobbies.Contains(lobby))
        {
            if (lobby.lobbyState == LobbyState.Closed || lobby.lobbyState == LobbyState.InGame)
            {
                break;
            }

            LobbyController controller = lobby.Controller;

            if (controller == null)
            {
                break;
            }

            if (controller.HasPendingWork && controller.ProcessPendingWorkBatch(LobbyWorkBatchSize, out LobbyWorkBatchResult workBatch))
            {
                HashSet<string> addedUserIds = new HashSet<string>();

                for (int i = 0; i < workBatch.addedPlayers.Count; i++)
                {
                    string addedUserId = workBatch.addedPlayers[i]?.userId;

                    if (!string.IsNullOrWhiteSpace(addedUserId))
                    {
                        addedUserIds.Add(addedUserId);
                        QueueMemberPublication(lobby, addedUserId);
                    }
                }

                if (workBatch.changedBoards.Count > 0)
                {
                    List<LobbyPlayerBoardViewData> changedExistingBoards = new List<LobbyPlayerBoardViewData>();

                    for (int i = 0; i < workBatch.changedBoards.Count; i++)
                    {
                        LobbyPlayerBoardViewData boardData = workBatch.changedBoards[i];

                        if (boardData != null && !addedUserIds.Contains(boardData.userId))
                        {
                            changedExistingBoards.Add(boardData);
                        }
                    }

                    BroadcastPlayerBoardCollection(lobby, changedExistingBoards);
                }
            }

            ProcessPendingMemberPublications(lobby);
            ProcessPendingJoinTimeouts(lobby);

            if (lobby.playMode == MainMenuPlayMode.Online &&
                lobby.lobbyState == LobbyState.Open &&
                !lobby.isGameSimulation)
            {
                if (controller.IsOnlineFinalCountdownDue())
                {
                    RemovePendingPlayersForLobbyStart(lobby);
                }

                LobbyViewData previousViewData = controller.BuildViewData();

                if (controller.TryBeginOnlineFinalCountdown())
                {
                    LobbyViewData currentViewData = controller.BuildViewData();
                    List<string> addedUserIds = GetAddedVisibleUserIds(previousViewData, currentViewData);
                    List<string> removedUserIds = GetRemovedVisibleUserIds(previousViewData, currentViewData);

                    BroadcastRemovedPlayers(lobby, removedUserIds, currentViewData);

                    List<LobbyPlayerViewData> addedPlayers = new List<LobbyPlayerViewData>();

                    for (int i = 0; i < addedUserIds.Count; i++)
                    {
                        LobbyPlayerData addedPlayer = controller.GetPlayer(addedUserIds[i]);

                        if (addedPlayer != null)
                        {
                            addedPlayers.Add(new LobbyPlayerViewData(addedPlayer));
                        }
                    }

                    for (int i = 0; i < addedPlayers.Count; i++)
                    {
                        string addedUserId = addedPlayers[i]?.userId;

                        if (!string.IsNullOrWhiteSpace(addedUserId))
                        {
                            QueueMemberPublication(lobby, addedUserId);
                        }
                    }

                    BroadcastLobbyStateChanged(lobby);
                }
            }

            if (lobby.lobbyState == LobbyState.FinalCountdown && controller.Timer.HasExpired())
            {
                if (NetworkGameSessionManager.instance != null &&
                    NetworkGameSessionManager.instance.HasGameForLobby(lobby.GetLobbyId()) &&
                    controller.CompleteFinalCountdown())
                {
                    LobbyFinalCountdownCompleted?.Invoke(lobby);
                    BroadcastLobbyStateChanged(lobby);
                }
                else
                {
                    HandleNetworkGameCreationFailure(lobby, GameSessionResult.Failed(
                        GameSessionOperationType.Create,
                        GameSessionFailureType.GameCreationFailed,
                        "The network Game was not created before the final countdown ended.",
                        lobbyId: lobby.GetLobbyId()));
                    BroadcastLobbyStateChanged(lobby);
                }
            }

            if (lobby.lobbyState == LobbyState.InGame)
            {
                break;
            }

            yield return null;
        }

        lobbyRuntimeRoutines.Remove(lobbyId);
    }

    private void StopLobbyRuntime(Lobby lobby)
    {
        if (lobby == null)
        {
            return;
        }

        string lobbyId = lobby.GetLobbyId();

        if (!lobbyRuntimeRoutines.TryGetValue(lobbyId, out Coroutine routine))
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        lobbyRuntimeRoutines.Remove(lobbyId);
    }

    private void StopAllLobbyRuntimeRoutines()
    {
        foreach (Coroutine routine in lobbyRuntimeRoutines.Values)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        lobbyRuntimeRoutines.Clear();
    }

    #endregion

    #region Disconnect Handling

    private void SubscribeToConnectionRegistry()
    {
        if (isSubscribedToConnectionRegistry)
        {
            return;
        }

        if (connectionRegistry == null)
        {
            connectionRegistry =
                NetworkConnectionRegistry.instance;
        }

        if (connectionRegistry == null)
        {
            return;
        }

        connectionRegistry.ConnectionRemoved +=
            OnConnectionRemoved;

        isSubscribedToConnectionRegistry = true;
    }

    private void UnsubscribeFromConnectionRegistry()
    {
        if (!isSubscribedToConnectionRegistry)
        {
            return;
        }

        if (connectionRegistry != null)
        {
            connectionRegistry.ConnectionRemoved -=
                OnConnectionRemoved;
        }

        isSubscribedToConnectionRegistry = false;
    }

    private void OnConnectionRemoved(ulong clientId, string userId)
    {
        StopInitialSync(clientId);

        if (networkBootstrap == null || !networkBootstrap.IsAuthority)
        {
            return;
        }

        RemovePlayerFromLobby(userId, LobbyPlayerExitReason.Disconnected);
    }

    #endregion

    #region Player Profiles

    private void SubscribeToPlayerProfileConnection()
    {
        if (isSubscribedToPlayerProfileConnection)
        {
            return;
        }

        NetworkPlayerProfileConnection.AuthorityProfileUpdateRequested += OnAuthorityPlayerProfileUpdateRequested;
        isSubscribedToPlayerProfileConnection = true;
    }

    private void UnsubscribeFromPlayerProfileConnection()
    {
        if (!isSubscribedToPlayerProfileConnection)
        {
            return;
        }

        NetworkPlayerProfileConnection.AuthorityProfileUpdateRequested -= OnAuthorityPlayerProfileUpdateRequested;
        isSubscribedToPlayerProfileConnection = false;
    }

    private void OnAuthorityPlayerProfileUpdateRequested(ulong clientId, PlayerProfileData profile)
    {
        if (profile == null || !profile.IsValid || !TryGetConnectedUserId(clientId, out string userId) ||
            !string.Equals(profile.userId, userId, StringComparison.Ordinal))
        {
            return;
        }

        Lobby lobby = FindUserLobby(userId);

        if (lobby?.Controller == null || !lobby.Controller.UpdatePlayerProfile(profile))
        {
            return;
        }

        BroadcastPlayerProfileUpdate(lobby, profile);
    }

    private void BroadcastPlayerProfileUpdate(Lobby lobby, PlayerProfileData profile)
    {
        if (lobby?.Controller == null || profile == null || !profile.IsValid || connectionRegistry == null || !connectionRegistry.IsReady)
        {
            return;
        }

        IReadOnlyList<LobbyPlayerData> players = lobby.Controller.Players;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];
            string userId = playerData?.userData?.userId;

            if (string.IsNullOrWhiteSpace(userId) || !connectionRegistry.TryGetClientId(userId, out ulong targetClientId))
            {
                continue;
            }

            NetworkPlayerProfileConnection.TrySendProfileUpdate(targetClientId, profile);
        }
    }

    #endregion

    #region Setup

    private bool CanInitialize()
    {
        if (NetworkRoot.instance == null || !NetworkRoot.instance.IsReady)
        {
            return false;
        }

        if (NetworkBootstrap.instance == null || !NetworkBootstrap.instance.IsReady)
        {
            return false;
        }

        if (NetworkConnectionRegistry.instance == null || !NetworkConnectionRegistry.instance.IsReady)
        {
            return false;
        }

        if (NetworkBotManager.instance == null || !NetworkBotManager.instance.IsReady)
        {
            return false;
        }

        return true;
    }

    private void UnsubscribeFromAllLobbyControllers()
    {
        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby lobby = lobbies[i];

            if (lobby?.Controller == null)
            {
                continue;
            }

            lobby.Controller.PlayerExitProcessed -= OnLobbyPlayerExitProcessed;
            lobby.Controller.FinalCountdownStarted -= OnLobbyFinalCountdownStarted;
        }
    }

    #endregion

    #region Stress Testing API

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    public bool TryGetStressLobby(string lobbyId, out Lobby lobby)
    {
        lobby = null;

        if (string.IsNullOrWhiteSpace(lobbyId))
        {
            return false;
        }

        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby candidate = lobbies[i];

            if (candidate != null && string.Equals(candidate.GetLobbyId(), lobbyId, StringComparison.Ordinal))
            {
                lobby = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetStressLobbyForUser(string userId, out Lobby lobby)
    {
        lobby = FindUserLobby(userId);
        return lobby?.Controller != null;
    }

    public bool TryCreateStressLobby(LobbySetupData lobbySetupData, out Lobby lobby, out string failureReason)
    {
        lobby = null;
        failureReason = string.Empty;

        if (networkBootstrap == null || !networkBootstrap.IsAuthority)
        {
            failureReason = "This process is not the network authority.";
            return false;
        }

        if (lobbySetupData == null ||
            (lobbySetupData.playMode != MainMenuPlayMode.Online && lobbySetupData.playMode != MainMenuPlayMode.Custom))
        {
            failureReason = "Stress lobbies must use Online or Custom multiplayer mode.";
            return false;
        }

        lobby = lobbySetupData.playMode == MainMenuPlayMode.Custom
            ? CreateCustomHostLobby(lobbySetupData)
            : CreateLobby(lobbySetupData);

        if (lobby?.Controller == null)
        {
            failureReason = "The stress lobby could not be created.";
            lobby = null;
            return false;
        }

        return true;
    }

    public bool TryAddStressPlayer(string lobbyId, UserData userData, bool isHost, out string failureReason)
    {
        failureReason = string.Empty;

        if (!TryGetStressLobby(lobbyId, out Lobby lobby) || lobby.Controller == null)
        {
            failureReason = "The target lobby could not be found.";
            return false;
        }

        LobbyEntryResult result = AddPlayerToLobby(lobby, userData, isHost);

        if (result == null || !result.success)
        {
            failureReason = result?.failureMessage ?? "The fake player could not join the lobby.";
            return false;
        }

        return true;
    }

    public bool TryRemoveStressPlayer(string lobbyId, string userId, out string failureReason)
    {
        failureReason = string.Empty;

        if (!TryGetStressLobby(lobbyId, out Lobby lobby) || lobby.Controller == null)
        {
            failureReason = "The target lobby could not be found.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(userId) || !lobby.Controller.HasPlayer(userId))
        {
            failureReason = "The fake player is no longer in the target lobby.";
            return false;
        }

        LobbyExitResult result = RemovePlayerFromLobby(userId, LobbyPlayerExitReason.VoluntaryLeave);

        if (result == null || !result.success)
        {
            failureReason = result?.failureMessage ?? "The fake player could not leave the lobby.";
            return false;
        }

        return true;
    }

    public bool TrySetStressPlayerSceneReady(string lobbyId, string userId, out string failureReason)
    {
        failureReason = string.Empty;

        if (!TryGetStressLobby(lobbyId, out Lobby lobby) || lobby.Controller == null)
        {
            failureReason = "The target lobby could not be found.";
            return false;
        }

        LobbyPlayerData playerData = lobby.Controller.GetPlayer(userId);

        if (playerData == null)
        {
            failureReason = "The fake player is no longer in the lobby.";
            return false;
        }

        if (playerData.isLobbySceneReady)
        {
            return true;
        }

        if (!lobby.Controller.SetLobbySceneReady(userId, true))
        {
            failureReason = "The fake player could not finish Lobby loading.";
            return false;
        }

        pendingJoinStartedTimeByUserId.Remove(userId);
        QueueMemberPublication(lobby, userId);
        return true;
    }

    public bool TryRerollStressPlayerBoard(string lobbyId, string userId, out string failureReason)
    {
        failureReason = string.Empty;

        if (!TryGetStressLobby(lobbyId, out Lobby lobby) || lobby.Controller == null)
        {
            failureReason = "The target lobby could not be found.";
            return false;
        }

        if (!lobby.Controller.RerollPlayerBoard(userId))
        {
            failureReason = "The fake player's board could not be rerolled.";
            return false;
        }

        BroadcastPlayerBoardUpdate(lobby, userId);
        return true;
    }

    public bool TryBroadcastStressChatMessage(
        string lobbyId,
        ChatParticipantData sender,
        string message,
        bool isPrivate,
        string recipientUserId,
        out int recipientCount,
        out string failureReason)
    {
        recipientCount = 0;
        failureReason = string.Empty;

        if (networkBootstrap == null || !networkBootstrap.IsAuthority)
        {
            failureReason = "This process is not the network authority.";
            return false;
        }

        if (!TryGetStressLobby(lobbyId, out Lobby lobby) || lobby?.Controller == null)
        {
            failureReason = "The target lobby could not be found.";
            return false;
        }

        if (sender == null || !sender.IsValid || string.IsNullOrWhiteSpace(message))
        {
            failureReason = "The synthetic chat message is invalid.";
            return false;
        }

        if (connectionRegistry == null || !connectionRegistry.IsReady)
        {
            failureReason = "The connection registry is not ready.";
            return false;
        }

        string resolvedRecipientUserId = isPrivate ? recipientUserId?.Trim() ?? string.Empty : string.Empty;

        if (isPrivate)
        {
            if (string.IsNullOrWhiteSpace(resolvedRecipientUserId) || !lobby.Controller.HasPlayer(resolvedRecipientUserId) ||
                !connectionRegistry.TryGetClientId(resolvedRecipientUserId, out ulong recipientClientId))
            {
                failureReason = "The private-message recipient is not connected to the target lobby.";
                return false;
            }

            if (!NetworkLobbyConnection.TrySendStressChatMessage(
                    recipientClientId,
                    lobbyId,
                    sender.userId,
                    sender.playerName,
                    sender.iconId,
                    message.Trim(),
                    true,
                    resolvedRecipientUserId))
            {
                failureReason = "The synthetic private message could not be sent to the target client.";
                return false;
            }

            recipientCount = 1;
            return true;
        }

        int sentCount = 0;

        SendToLobbyPlayers(lobby, clientId =>
        {
            if (NetworkLobbyConnection.TrySendStressChatMessage(
                    clientId,
                    lobbyId,
                    sender.userId,
                    sender.playerName,
                    sender.iconId,
                    message.Trim(),
                    false,
                    string.Empty))
            {
                sentCount++;
            }
        });

        recipientCount = sentCount;

        if (recipientCount <= 0)
        {
            failureReason = "The synthetic public message did not have any connected Lobby recipients.";
            return false;
        }

        return true;
    }

    public bool TryBeginStressFinalCountdown(string lobbyId, string requesterUserId, out string failureReason)
    {
        failureReason = string.Empty;

        if (!TryGetStressLobby(lobbyId, out Lobby lobby) || lobby.Controller == null)
        {
            failureReason = "The target lobby could not be found.";
            return false;
        }

        if (!lobby.Controller.BeginFinalCountdown(requesterUserId))
        {
            failureReason = "The stress lobby could not begin its final countdown.";
            return false;
        }

        BroadcastLobbyStateChanged(lobby);
        return true;
    }

    public bool TryGetSimulationLobbyState(
        string lobbyId,
        out int humanPlayerCount,
        out int playerCount,
        out int sceneReadyPlayerCount,
        out int botCount,
        out bool hasPendingWork)
    {
        humanPlayerCount = 0;
        playerCount = 0;
        sceneReadyPlayerCount = 0;
        botCount = 0;
        hasPendingWork = false;

        if (!TryGetStressLobby(lobbyId, out Lobby lobby) || lobby.Controller == null)
        {
            return false;
        }

        LobbyController controller = lobby.Controller;
        playerCount = controller.PlayerCount;
        sceneReadyPlayerCount = controller.SceneReadyPlayerCount;
        botCount = controller.BotCount;
        humanPlayerCount = Mathf.Max(0, playerCount - botCount);
        hasPendingWork = controller.HasPendingWork;
        return true;
    }

    public bool TryGetRunningSimulationTestPlayerState(
        string lobbyId,
        IReadOnlyList<string> expectedUserIds,
        out int connectedTestPlayerCount,
        out int joinedTestPlayerCount,
        out int sceneReadyTestPlayerCount)
    {
        connectedTestPlayerCount = 0;
        joinedTestPlayerCount = 0;
        sceneReadyTestPlayerCount = 0;

        if (!TryGetStressLobby(lobbyId, out Lobby lobby) ||
            lobby.Controller == null ||
            connectionRegistry == null ||
            !connectionRegistry.IsReady ||
            expectedUserIds == null)
        {
            return false;
        }

        for (int i = 0; i < expectedUserIds.Count; i++)
        {
            string userId = expectedUserIds[i];

            if (string.IsNullOrWhiteSpace(userId) || !connectionRegistry.IsBingoUserConnected(userId))
            {
                continue;
            }

            connectedTestPlayerCount++;
            LobbyPlayerData playerData = lobby.Controller.GetPlayer(userId);

            if (playerData?.userData == null || playerData.userData.userTag == UserTag.Bot)
            {
                continue;
            }

            joinedTestPlayerCount++;

            if (playerData.isLobbySceneReady)
            {
                sceneReadyTestPlayerCount++;
            }
        }

        return true;
    }

    public bool TrySetSimulationBotCount(string lobbyId, int desiredBotCount, out int appliedBotCount, out string failureReason)
    {
        appliedBotCount = 0;
        failureReason = string.Empty;

        if (!TryGetStressLobby(lobbyId, out Lobby lobby) || lobby.Controller == null)
        {
            failureReason = "The simulation lobby could not be found.";
            return false;
        }

        appliedBotCount = lobby.Controller.SetSimulationBotCount(desiredBotCount);
        return true;
    }

    public bool TryBeginSimulationFinalCountdown(string lobbyId, string requesterUserId, out string failureReason)
    {
        failureReason = string.Empty;

        if (!TryGetStressLobby(lobbyId, out Lobby lobby) || lobby.Controller == null)
        {
            failureReason = "The simulation lobby could not be found.";
            return false;
        }

        bool started = lobby.playMode == MainMenuPlayMode.Online
            ? lobby.Controller.BeginFinalCountdown()
            : lobby.Controller.BeginFinalCountdown(requesterUserId);

        if (!started)
        {
            failureReason = "The simulation lobby could not begin its final countdown.";
            return false;
        }

        BroadcastLobbyStateChanged(lobby);
        return true;
    }

#endif

    #endregion

    #region Authority Commands

    public void ProcessAuthoritySetPlayerReady(ulong clientId, bool isReady)
    {
        if (networkBootstrap == null || !networkBootstrap.IsAuthority || connectionRegistry == null || !connectionRegistry.IsReady)
        {
            return;
        }

        if (!connectionRegistry.TryGetBingoUserId(clientId, out string userId))
        {
            return;
        }

        Lobby lobby = FindUserLobby(userId);

        if (lobby?.Controller == null)
        {
            return;
        }

        LobbyController controller = lobby.Controller;
        LobbyPlayerData playerData = controller.GetPlayer(userId);
        bool previousReady = playerData != null && playerData.isReady;
        LobbyState previousState = lobby.lobbyState;
        bool previousTimerActive = controller.IsTimerActive;
        double previousTimerEndTime = controller.TimerEndTime;

        if (!controller.SetPlayerReady(userId, isReady))
        {
            return;
        }

        if (playerData != null && playerData.isReady != previousReady)
        {
            BroadcastPlayerReadyChanged(lobby, userId, playerData.isReady);
        }

        if (HasLobbyStateChanged(lobby, controller, previousState, previousTimerActive, previousTimerEndTime))
        {
            BroadcastLobbyStateChanged(lobby);
        }
    }



    public bool ProcessAuthorityApplyHostSettings(ulong clientId, LobbyHostSettingsData settingsData)
    {
        if (settingsData == null || !TryGetConnectedUserId(clientId, out string userId))
        {
            return false;
        }

        Lobby lobby = FindUserLobby(userId);

        if (lobby?.Controller == null || !lobby.Controller.ApplyHostSettings(userId, settingsData, out List<LobbyExitResult> capacityTrimResults))
        {
            return false;
        }

        ProcessCapacityTrimmedPlayers(lobby, capacityTrimResults);
        BroadcastLobbySettingsChanged(lobby, lobby.Controller.BuildViewData(false));
        return true;
    }

    private void ProcessCapacityTrimmedPlayers(Lobby lobby, IReadOnlyList<LobbyExitResult> capacityTrimResults)
    {
        if (lobby == null || capacityTrimResults == null || capacityTrimResults.Count == 0)
        {
            return;
        }

        for (int i = 0; i < capacityTrimResults.Count; i++)
        {
            LobbyExitResult exitResult = capacityTrimResults[i];

            if (exitResult == null || !exitResult.success || string.IsNullOrWhiteSpace(exitResult.userId))
            {
                continue;
            }

            pendingJoinStartedTimeByUserId.Remove(exitResult.userId);

            if (connectionRegistry == null || !connectionRegistry.TryGetClientId(exitResult.userId, out ulong targetClientId))
            {
                continue;
            }

            StopInitialSync(targetClientId);
            NetworkLobbyConnection.TrySendForcedLobbyExit(targetClientId, LobbyExitNotification.Kicked(lobby.GetLobbyId()));
        }
    }

    public void ProcessAuthorityStartLobby(ulong clientId)
    {
        if (!TryGetConnectedUserId(clientId, out string userId))
        {
            return;
        }

        Lobby lobby = FindUserLobby(userId);

        if (lobby?.Controller == null)
        {
            return;
        }

        LobbyController controller = lobby.Controller;
        LobbyPlayerData requesterPlayer = controller.GetPlayer(userId);

        if (requesterPlayer == null || !requesterPlayer.isHost)
        {
            return;
        }

        LobbySettings lobbySettings = LobbySettings.instance;

        if (lobbySettings != null && controller.SceneReadyPlayerCount < lobbySettings.MinimumPlayers)
        {
            string message = $"At least {lobbySettings.MinimumPlayers} players are required to start the game.";
            NotificationService.instance?.SendToUser(userId, UIMessageType.NotEnoughPlayers, message);
            return;
        }

        RemovePendingPlayersForLobbyStart(lobby);

        if (controller.BeginFinalCountdown(userId))
        {
            BroadcastLobbyStateChanged(lobby);
        }
    }

    private bool TryGetConnectedUserId(ulong clientId, out string userId)
    {
        userId = string.Empty;

        return networkBootstrap != null &&
               networkBootstrap.IsAuthority &&
               connectionRegistry != null &&
               connectionRegistry.IsReady &&
               connectionRegistry.TryGetBingoUserId(clientId, out userId);
    }
    #endregion
}
