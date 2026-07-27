using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkLobbyManager : MonoBehaviour
{
    #region Fields

    public static NetworkLobbyManager instance;

    private const int BoardCollectionBatchSize = 10;

    private readonly List<Lobby> lobbies = new List<Lobby>();
    private readonly Dictionary<string, string> relayJoinCodeByLobbyId = new Dictionary<string, string>();
    private readonly Dictionary<string, Coroutine> lobbyRuntimeRoutines = new Dictionary<string, Coroutine>();
    private readonly Dictionary<string, long> lobbyRevisions = new Dictionary<string, long>();

    private bool isReady;
    private bool isSubscribedToConnectionRegistry;

    private NetworkRoot networkRoot;
    private NetworkBootstrap networkBootstrap;
    private NetworkConnectionRegistry connectionRegistry;

    public bool IsReady => isReady;
    public IReadOnlyList<Lobby> Lobbies => lobbies;
    public bool HasActiveLobbies => lobbies.Count > 0;

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

        isReady = true;
    }

    private void OnDestroy()
    {
        UnsubscribeFromConnectionRegistry();
        UnsubscribeFromAllLobbyControllers();
        StopAllLobbyRuntimeRoutines();

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

        Lobby existingUserLobby =
            FindUserLobby(registeredUserId);

        if (existingUserLobby != null)
        {
            return BuildNetworkEntryResult(existingUserLobby);
        }

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

    #region Lobby Exit

    public LobbyExitResult ProcessAuthorityLobbyExit(ulong senderClientId)
    {
        if (!TryResolveConnectedUser(senderClientId, out string userId, out LobbyExitResult failureResult))
        {
            return failureResult;
        }

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
            NetworkLobbyConnection.TrySendForcedLobbyExit(targetClientId, LobbyExitNotification.Kicked(lobby.GetLobbyId()));
        }

        return result;
    }
    public void ProcessAuthorityLobbySceneReady(ulong clientId)
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

        LobbyPlayerData playerData = lobby.Controller.GetPlayer(userId);
        bool wasSceneReady = playerData != null && playerData.isLobbySceneReady;

        if (!lobby.Controller.SetLobbySceneReady(userId, true) || wasSceneReady)
        {
            return;
        }

        BroadcastPlayerJoined(lobby, userId, userId);
        SendLobbySyncSnapshot(lobby, clientId);
        BroadcastPlayerBoardUpdate(lobby, userId);
    }

    private LobbyExitResult RemovePlayerFromLobby(string userId, LobbyPlayerExitReason exitReason)
    {
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

        DeleteLobby(lobby);
    }

    private void DeleteLobby(Lobby lobby)
    {
        if (lobby == null)
        {
            return;
        }

        if (lobby.Controller != null)
        {
            lobby.Controller.PlayerExitProcessed -= OnLobbyPlayerExitProcessed;
            lobby.Controller.FinalCountdownStarted -= OnLobbyFinalCountdownStarted;
        }

        StopLobbyRuntime(lobby);
        relayJoinCodeByLobbyId.Remove(lobby.GetLobbyId());
        lobbyRevisions.Remove(lobby.GetLobbyId());
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

        return BuildNetworkEntryResult(lobby);
    }

    private LobbyEntryResult BuildNetworkEntryResult(Lobby lobby)
    {
        if (lobby?.Controller == null)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyNotFound, "The lobby could not be found.");
        }

        LobbyViewData lobbyViewData = lobby.Controller.BuildViewData();
        LobbyBoardCollectionData lobbyBoardData = lobby.Controller.BuildPlayerBoardCollectionData();
        long revision = GetLobbyRevision(lobby);

        if (lobbyViewData == null || lobbyBoardData == null)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyJoinFailed, "The lobby state could not be created.");
        }

        lobbyBoardData.revision = revision;
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
        StartLobbyRuntime(lobby);

        return lobby;
    }

    private void OnLobbyFinalCountdownStarted(LobbyController controller)
    {
        if (controller == null || networkBootstrap == null || !networkBootstrap.IsAuthority || NotificationService.instance == null)
        {
            return;
        }

        Lobby lobby = FindLobbyByController(controller);

        if (lobby == null)
        {
            return;
        }

        IReadOnlyList<LobbyPlayerData> players = controller.Players;
        List<string> userIds = new List<string>();

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];
            string userId = playerData?.userData?.userId;

            if (string.IsNullOrWhiteSpace(userId) || playerData.userData.userTag == UserTag.Bot)
            {
                continue;
            }

            userIds.Add(userId);
        }

        NotificationService.instance.SendToUsers(userIds, UIMessageType.GameAboutToStart);
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
        if (lobby?.Controller == null || connectionRegistry == null || !connectionRegistry.IsReady || sendAction == null)
        {
            return;
        }

        IReadOnlyList<LobbyPlayerData> players = lobby.Controller.Players;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];
            string userId = playerData?.userData?.userId;

            if (playerData == null || !playerData.isLobbySceneReady || string.IsNullOrWhiteSpace(userId) ||
                (!string.IsNullOrWhiteSpace(excludedUserId) && userId == excludedUserId) ||
                !connectionRegistry.TryGetClientId(userId, out ulong clientId))
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
        LobbyViewData viewData = lobby.Controller.BuildViewData();

        if (playerData == null || !playerData.isLobbySceneReady || viewData == null)
        {
            return;
        }

        long revision = GetNextLobbyRevision(lobby);
        LobbyPlayerJoinedData data = new LobbyPlayerJoinedData(lobby.GetLobbyId(), revision, new LobbyPlayerViewData(playerData), viewData.playerCount, viewData.botCount);
        SendToLobbyPlayers(lobby, clientId => NetworkLobbyConnection.TrySendPlayerJoined(clientId, data), excludedUserId);
    }

    private void BroadcastPlayerLeft(Lobby lobby, string userId)
    {
        if (lobby?.Controller == null || string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        LobbyViewData viewData = lobby.Controller.BuildViewData();

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

        viewData ??= lobby.Controller.BuildViewData();

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

        LobbyViewData viewData = lobby.Controller.BuildViewData();

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

    private void BroadcastJoinedPlayers(Lobby lobby, IReadOnlyList<string> userIds, LobbyViewData currentViewData)
    {
        if (lobby?.Controller == null || userIds == null || currentViewData == null)
        {
            return;
        }

        for (int i = 0; i < userIds.Count; i++)
        {
            string userId = userIds[i];
            LobbyPlayerData playerData = lobby.Controller.GetPlayer(userId);

            if (playerData == null)
            {
                continue;
            }

            long revision = GetNextLobbyRevision(lobby);
            LobbyPlayerJoinedData data = new LobbyPlayerJoinedData(lobby.GetLobbyId(), revision, new LobbyPlayerViewData(playerData), currentViewData.playerCount, currentViewData.botCount);
            SendToLobbyPlayers(lobby, clientId => NetworkLobbyConnection.TrySendPlayerJoined(clientId, data));
        }
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
        if (!TryGetConnectedUserId(clientId, out string userId))
        {
            return;
        }

        Lobby lobby = FindUserLobby(userId);

        if (lobby?.Controller == null)
        {
            return;
        }

        SendLobbySyncSnapshot(lobby, clientId);
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

    private void BroadcastPlayerBoardCollection(Lobby lobby, IReadOnlyCollection<string> userIds)
    {
        if (lobby?.Controller == null || connectionRegistry == null || !connectionRegistry.IsReady)
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

        if (selectedBoards.Count == 0)
        {
            return;
        }

        for (int startIndex = 0; startIndex < selectedBoards.Count; startIndex += BoardCollectionBatchSize)
        {
            int batchCount = Mathf.Min(BoardCollectionBatchSize, selectedBoards.Count - startIndex);
            List<LobbyPlayerBoardViewData> batchBoards = new List<LobbyPlayerBoardViewData>(batchCount);

            for (int i = 0; i < batchCount; i++)
            {
                batchBoards.Add(selectedBoards[startIndex + i]);
            }

            long revision = GetNextLobbyRevision(lobby);
            LobbyBoardCollectionData batchData = new LobbyBoardCollectionData(lobby.GetLobbyId(), revision, batchBoards);
            SendToLobbyPlayers(lobby, clientId => NetworkLobbyConnection.TrySendPlayerBoardCollection(clientId, batchData));
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

            if (lobby.playMode == MainMenuPlayMode.Online && lobby.lobbyState == LobbyState.Open)
            {
                LobbyViewData previousViewData = controller.BuildViewData();

                if (controller.TryBeginOnlineFinalCountdown())
                {
                    LobbyViewData currentViewData = controller.BuildViewData();
                    List<string> addedUserIds = GetAddedVisibleUserIds(previousViewData, currentViewData);
                    List<string> removedUserIds = GetRemovedVisibleUserIds(previousViewData, currentViewData);

                    BroadcastRemovedPlayers(lobby, removedUserIds, currentViewData);
                    BroadcastJoinedPlayers(lobby, addedUserIds, currentViewData);

                    if (addedUserIds.Count > 0)
                    {
                        BroadcastPlayerBoardCollection(lobby, addedUserIds);
                    }

                    BroadcastLobbyStateChanged(lobby);
                }
            }

            if (lobby.lobbyState == LobbyState.FinalCountdown && controller.CompleteFinalCountdown())
            {
                BroadcastLobbyStateChanged(lobby);
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
        if (networkBootstrap == null || !networkBootstrap.IsAuthority)
        {
            return;
        }

        RemovePlayerFromLobby(userId, LobbyPlayerExitReason.Disconnected);
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

        if (lobby?.Controller == null)
        {
            return false;
        }

        LobbyController controller = lobby.Controller;
        LobbyViewData previousViewData = controller.BuildViewData();
        BingoBallCountType previousBallCountType = controller.BallCountType;
        bool previousUseFreeCell = controller.UseFreeCell;

        if (!controller.ApplyHostSettings(userId, settingsData))
        {
            return false;
        }

        LobbyViewData currentViewData = controller.BuildViewData();
        List<string> addedUserIds = GetAddedVisibleUserIds(previousViewData, currentViewData);
        List<string> removedUserIds = GetRemovedVisibleUserIds(previousViewData, currentViewData);
        bool boardGenerationChanged = previousBallCountType != controller.BallCountType || previousUseFreeCell != controller.UseFreeCell;

        BroadcastLobbySettingsChanged(lobby, currentViewData);
        BroadcastRemovedPlayers(lobby, removedUserIds, currentViewData);
        BroadcastJoinedPlayers(lobby, addedUserIds, currentViewData);

        if (boardGenerationChanged)
        {
            BroadcastPlayerBoardCollection(lobby, null);
        }
        else if (addedUserIds.Count > 0)
        {
            BroadcastPlayerBoardCollection(lobby, addedUserIds);
        }

        return true;
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

        if (lobbySettings != null && controller.PlayerCount < lobbySettings.MinimumPlayers)
        {
            string message = $"At least {lobbySettings.MinimumPlayers} players are required to start the game.";
            NotificationService.instance?.SendToUser(userId, UIMessageType.NotEnoughPlayers, message);
            return;
        }

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
