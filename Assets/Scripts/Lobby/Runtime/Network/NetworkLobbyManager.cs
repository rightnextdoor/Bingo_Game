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

        if (!lobby.Controller.SetLobbySceneReady(userId, true))
        {
            return;
        }

        BroadcastLobbyView(lobby);
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

        BroadcastLobbyView(lobby);
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

        BroadcastLobbyView(lobby);

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

        if (lobbyViewData == null || lobbyBoardData == null)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyJoinFailed, "The lobby state could not be created.");
        }

        return LobbyEntryResult.Succeeded(lobby.GetLobbyId(), lobbyViewData, lobbyBoardData);
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

    private void BroadcastLobbyView(Lobby lobby)
    {
        if (lobby?.Controller == null || networkBootstrap == null || !networkBootstrap.IsAuthority || connectionRegistry == null)
        {
            return;
        }

        LobbyViewData lobbyViewData = lobby.Controller.BuildViewData();
        IReadOnlyList<LobbyPlayerData> players = lobby.Controller.Players;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];
            string userId = playerData?.userData?.userId;

            if (string.IsNullOrWhiteSpace(userId) || !connectionRegistry.TryGetClientId(userId, out ulong clientId))
            {
                continue;
            }

            NetworkLobbyConnection.TrySendLobbyView(clientId, lobbyViewData);
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

    private void BroadcastPlayerBoardUpdate(Lobby lobby, string userId)
    {
        if (lobby?.Controller == null || connectionRegistry == null || !connectionRegistry.IsReady)
        {
            return;
        }

        LobbyPlayerBoardViewData playerBoard =
            lobby.Controller.GetPlayerBoardViewData(userId);

        if (playerBoard == null)
        {
            return;
        }

        LobbyPlayerBoardUpdateData updateData =
            new LobbyPlayerBoardUpdateData(lobby.GetLobbyId(), userId, playerBoard.boardData);

        IReadOnlyList<LobbyPlayerData> players = lobby.Controller.Players;

        for (int i = 0; i < players.Count; i++)
        {
            string targetUserId = players[i]?.userData?.userId;

            if (string.IsNullOrWhiteSpace(targetUserId) || !connectionRegistry.TryGetClientId(targetUserId, out ulong clientId))
            {
                continue;
            }

            NetworkLobbyConnection.TrySendPlayerBoardUpdate(clientId, updateData);
        }
    }

    private void BroadcastPlayerBoardCollection(Lobby lobby)
    {
        if (lobby?.Controller == null || connectionRegistry == null || !connectionRegistry.IsReady)
        {
            return;
        }

        LobbyBoardCollectionData fullCollection = lobby.Controller.BuildPlayerBoardCollectionData();

        if (fullCollection?.boards == null || fullCollection.boards.Count == 0)
        {
            return;
        }

        IReadOnlyList<LobbyPlayerData> players = lobby.Controller.Players;

        for (int startIndex = 0; startIndex < fullCollection.boards.Count; startIndex += BoardCollectionBatchSize)
        {
            int batchCount = Mathf.Min(BoardCollectionBatchSize, fullCollection.boards.Count - startIndex);
            List<LobbyPlayerBoardViewData> batchBoards = new List<LobbyPlayerBoardViewData>(batchCount);

            for (int i = 0; i < batchCount; i++)
            {
                batchBoards.Add(fullCollection.boards[startIndex + i]);
            }

            LobbyBoardCollectionData batchData = new LobbyBoardCollectionData(lobby.GetLobbyId(), batchBoards);
            string batchJson = JsonUtility.ToJson(batchData);

            for (int i = 0; i < players.Count; i++)
            {
                string targetUserId = players[i]?.userData?.userId;

                if (string.IsNullOrWhiteSpace(targetUserId) || !connectionRegistry.TryGetClientId(targetUserId, out ulong clientId))
                {
                    continue;
                }

                NetworkLobbyConnection.TrySendPlayerBoardCollection(clientId, batchData);
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

            bool stateChanged = false;
            int playerCountBeforeUpdate = controller.PlayerCount;

            if (lobby.playMode == MainMenuPlayMode.Online && lobby.lobbyState == LobbyState.Open && controller.TryBeginOnlineFinalCountdown())
            {
                stateChanged = true;
            }

            bool playerCollectionChanged = controller.PlayerCount != playerCountBeforeUpdate;

            if (lobby.lobbyState == LobbyState.FinalCountdown && controller.CompleteFinalCountdown())
            {
                stateChanged = true;
            }

            if (stateChanged)
            {
                BroadcastLobbyView(lobby);

                if (playerCollectionChanged)
                {
                    BroadcastPlayerBoardCollection(lobby);
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

        if (!lobby.Controller.SetPlayerReady(userId, isReady))
        {
            return;
        }

        BroadcastLobbyView(lobby);
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
        BingoBallCountType previousBallCountType = controller.BallCountType;
        bool previousUseFreeCell = controller.UseFreeCell;
        int previousPlayerCount = controller.PlayerCount;

        if (!controller.ApplyHostSettings(userId, settingsData))
        {
            return false;
        }

        bool boardCollectionChanged = previousBallCountType != controller.BallCountType || previousUseFreeCell != controller.UseFreeCell || controller.PlayerCount > previousPlayerCount;

        BroadcastLobbyView(lobby);

        if (boardCollectionChanged)
        {
            BroadcastPlayerBoardCollection(lobby);
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
            BroadcastLobbyView(lobby);
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
