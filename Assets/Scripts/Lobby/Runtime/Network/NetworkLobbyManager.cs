using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkLobbyManager : MonoBehaviour, ILobbyService
{
    public static NetworkLobbyManager instance;

    private const float ConnectionTimeoutSeconds = 30f;
    private const float LobbyConnectionTimeoutSeconds = 15f;
    private const float ExitNotificationDeliverySeconds = 0.15f;

    private readonly List<Lobby> lobbies = new List<Lobby>();
    private readonly Dictionary<string, string> relayJoinCodeByLobbyId = new Dictionary<string, string>();

    private bool isReady;
    private bool isSubscribedToConnectionRegistry;

    private NetworkRoot networkRoot;
    private NetworkBootstrap networkBootstrap;
    private NetworkConnectionRegistry connectionRegistry;

    public SessionRuntimeType RuntimeType => SessionRuntimeType.Network;
    public bool IsReady => isReady;
    public IReadOnlyList<Lobby> Lobbies => lobbies;

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

        if (instance == this)
        {
            instance = null;
        }
    }

    #region Lobby Entry

    public async Task<LobbyEntryResult> EnterLobbyAsync(LobbySetupData lobbySetupData)
    {
        if (!isReady)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.ServiceUnavailable,
                "The network lobby manager is not ready.");
        }

        if (!IsValidNetworkSetup(lobbySetupData))
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.InvalidSetupData,
                "The network lobby setup data is invalid.");
        }

        string relayJoinCode = string.Empty;

        if (!HasUsableNetworkConnection() &&
            !TryPrepareCustomLobbySearch(
                lobbySetupData,
                out relayJoinCode,
                out LobbyEntryResult customSearchFailure))
        {
            return customSearchFailure;
        }

        bool connectionReady = await EnsureNetworkConnectionAsync(
            lobbySetupData,
            relayJoinCode);

        if (!connectionReady)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.NetworkConnectionFailed,
                "The network connection could not be created.");
        }

        NetworkLobbyConnection lobbyConnection =
            await WaitForLocalLobbyConnectionAsync();

        if (lobbyConnection == null)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.NetworkLobbyConnectionUnavailable,
                "The network lobby connection was not available.");
        }

        LobbyEntryResult result = await lobbyConnection.RequestEnterLobbyAsync(lobbySetupData);

        if (result == null)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.Unknown, "The network lobby did not return a result.");
        }

        if (!result.success)
        {
            _ = TryRollbackFailedLobbyEntryAsync();
        }

        return result;
    }

    private bool HasUsableNetworkConnection()
    {
        return networkBootstrap != null &&
               networkBootstrap.IsConnected &&
               NetworkLobbyConnection.GetLocalConnection() != null;
    }

    public LobbyEntryResult ProcessAuthorityLobbyEntry(LobbySetupData lobbySetupData, ulong senderClientId)
    {
        if (networkBootstrap == null ||
            !networkBootstrap.IsAuthority)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.ServiceUnavailable,
                "This process is not the network authority.");
        }

        if (connectionRegistry == null ||
            !connectionRegistry.IsReady)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.ServiceUnavailable,
                "The network connection registry is not ready.");
        }

        if (!connectionRegistry.TryGetBingoUserId(
                senderClientId,
                out string registeredUserId))
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.UserMissing,
                "The connected user could not be resolved.");
        }

        if (lobbySetupData?.userData == null ||
            !lobbySetupData.userData.HasUser)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.UserMissing,
                "The user information is missing.");
        }

        if (lobbySetupData.userData.userId != registeredUserId)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.UserMissing,
                "The lobby user does not match the connected user.");
        }

        ApplyMultiplayerPlayModeSimulationSetup(
            lobbySetupData,
            registeredUserId);

        Lobby existingUserLobby =
            FindUserLobby(registeredUserId);

        if (existingUserLobby != null)
        {
            return LobbyEntryResult.Succeeded(
                existingUserLobby);
        }

        switch (lobbySetupData.playMode)
        {
            case MainMenuPlayMode.Online:
                Lobby selectedLobby =
                    FindOrCreateOnlineLobby(
                        lobbySetupData);

                if (selectedLobby == null)
                {
                    return LobbyEntryResult.Failed(
                        LobbyEntryFailureType.LobbyCreationFailed,
                        "The Online lobby could not be created.");
                }

                return AddPlayerToLobby(
                    selectedLobby,
                    lobbySetupData.userData,
                    false);

            case MainMenuPlayMode.Custom:
                return ProcessCustomLobbyEntry(
                    lobbySetupData);

            default:
                return LobbyEntryResult.Failed(
                    LobbyEntryFailureType.InvalidSetupData,
                    "The network lobby mode is not valid.");
        }
    }

    private void ApplyMultiplayerPlayModeSimulationSetup(LobbySetupData lobbySetupData, string registeredUserId)
    {
        if (!MultiplayerPlayModeTestContext.IsActive ||
            lobbySetupData == null)
        {
            return;
        }

        LobbySimulationController simulationController =
            FindFirstObjectByType<LobbySimulationController>();

        if (simulationController == null)
        {
            return;
        }

        bool isHostPlayer =
            registeredUserId ==
            MultiplayerPlayModeTestContext.UserId;

        simulationController.ApplyNetworkSimulationSetup(
            lobbySetupData,
            isHostPlayer);
    }

    #endregion

    #region Lobby Exit

    public async Task<LobbyExitResult> LeaveLobbyAsync(string userId)
    {
        if (!isReady)
        {
            return LobbyExitResult.Failed(
                userId,
                LobbyPlayerExitReason.VoluntaryLeave,
                "The network lobby manager is not ready.");
        }

        LobbyExitResult result;
        NetworkLobbyConnection lobbyConnection = NetworkLobbyConnection.GetLocalConnection();

        if (lobbyConnection != null)
        {
            result = await lobbyConnection.RequestLeaveLobbyAsync();
        }
        else if (networkBootstrap != null && networkBootstrap.IsAuthority)
        {
            result = RemovePlayerFromLobby(
                userId,
                LobbyPlayerExitReason.VoluntaryLeave);
        }
        else
        {
            return LobbyExitResult.Failed(
                userId,
                LobbyPlayerExitReason.VoluntaryLeave,
                "The network lobby connection was not available.");
        }

        if (result != null && result.success)
        {
            await ShutdownLocalNetworkAfterExitIfPossible();
        }

        return result ?? LobbyExitResult.Failed(
            userId,
            LobbyPlayerExitReason.VoluntaryLeave,
            "The network lobby did not return a leave result.");
    }

    public async Task<LobbyExitResult> KickPlayerAsync(string targetUserId)
    {
        NetworkLobbyConnection lobbyConnection = NetworkLobbyConnection.GetLocalConnection();

        if (lobbyConnection == null)
        {
            return LobbyExitResult.Failed(targetUserId, LobbyPlayerExitReason.Kicked, "The network lobby connection was not available.");
        }

        return await lobbyConnection.RequestKickPlayerAsync(targetUserId);
    }

    public LobbyExitResult ProcessAuthorityLobbyExit(ulong senderClientId)
    {
        if (!TryResolveConnectedUser(
                senderClientId,
                out string userId,
                out LobbyExitResult failureResult))
        {
            return failureResult;
        }

        return RemovePlayerFromLobby(
            userId,
            LobbyPlayerExitReason.VoluntaryLeave);
    }

    public LobbyExitResult ProcessAuthorityKickPlayer(
        ulong senderClientId,
        string targetUserId)
    {
        if (!TryResolveConnectedUser(
                senderClientId,
                out string requesterUserId,
                out LobbyExitResult failureResult))
        {
            return failureResult;
        }

        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            return LobbyExitResult.Failed(
                targetUserId,
                LobbyPlayerExitReason.Kicked,
                "The target player UserId is missing.");
        }

        Lobby lobby = FindUserLobby(requesterUserId);

        if (lobby?.Controller == null ||
            lobby.playMode != MainMenuPlayMode.Custom)
        {
            return LobbyExitResult.Failed(
                targetUserId,
                LobbyPlayerExitReason.Kicked,
                "Only a Custom lobby host can remove a player.");
        }

        LobbyPlayerData requesterPlayer =
            lobby.Controller.GetPlayer(requesterUserId);

        if (requesterPlayer == null || !requesterPlayer.isHost)
        {
            return LobbyExitResult.Failed(
                targetUserId,
                LobbyPlayerExitReason.Kicked,
                "Only the Custom lobby host can remove a player.");
        }

        if (requesterUserId == targetUserId)
        {
            return LobbyExitResult.Failed(
                targetUserId,
                LobbyPlayerExitReason.Kicked,
                "The host cannot kick themselves. Use Leave Lobby instead.");
        }

        LobbyPlayerData targetPlayer =
            lobby.Controller.GetPlayer(targetUserId);

        if (targetPlayer == null)
        {
            return LobbyExitResult.Failed(
                targetUserId,
                LobbyPlayerExitReason.Kicked,
                "The target player was not found in the lobby.");
        }

        if (targetPlayer.isHost)
        {
            return LobbyExitResult.Failed(
                targetUserId,
                LobbyPlayerExitReason.Kicked,
                "The Custom lobby host cannot be kicked.");
        }

        ulong targetClientId = default;
        bool hasTargetConnection =
            connectionRegistry != null &&
            connectionRegistry.TryGetClientId(
                targetUserId,
                out targetClientId);

        LobbyExitResult result = lobby.Controller.KickPlayer(
            requesterUserId,
            targetUserId);

        if (result.success && hasTargetConnection)
        {
            NetworkLobbyConnection.TrySendForcedLobbyExit(
                targetClientId,
                LobbyExitNotification.Kicked(
                    lobby.GetLobbyId()));
        }

        return result;
    }

    public void ProcessAuthorityLobbyConnectionDespawn(ulong clientId)
    {
        if (networkBootstrap == null || !networkBootstrap.IsAuthority || connectionRegistry == null)
        {
            return;
        }

        if (!connectionRegistry.TryGetBingoUserId(clientId, out string userId))
        {
            return;
        }

        RemovePlayerFromLobby(userId, LobbyPlayerExitReason.Disconnected);
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
    }

    private LobbyExitResult RemovePlayerFromLobby(string userId, LobbyPlayerExitReason exitReason)
    {
        Lobby lobby = FindUserLobby(userId);

        if (lobby?.Controller == null)
        {
            return LobbyExitResult.Succeeded(
                userId,
                exitReason,
                false,
                0,
                false,
                LobbyCloseReason.None);
        }

        return lobby.Controller.RemovePlayer(
            userId,
            exitReason);
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

                if (connectionRegistry == null ||
                    !connectionRegistry.TryGetClientId(
                        userId,
                        out ulong clientId))
                {
                    continue;
                }

                NetworkLobbyConnection.TrySendForcedLobbyExit(
                    clientId,
                    LobbyExitNotification.LobbyClosed(
                        lobbyId,
                        LobbyCloseReason.HostLeft));
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

        relayJoinCodeByLobbyId.Remove(lobby.GetLobbyId());
        lobbies.Remove(lobby);
    }

    private bool TryResolveConnectedUser(
        ulong clientId,
        out string userId,
        out LobbyExitResult failureResult)
    {
        userId = string.Empty;
        failureResult = null;

        if (networkBootstrap == null ||
            !networkBootstrap.IsAuthority)
        {
            failureResult = LobbyExitResult.Failed(
                userId,
                LobbyPlayerExitReason.VoluntaryLeave,
                "This process is not the network authority.");

            return false;
        }

        if (connectionRegistry == null ||
            !connectionRegistry.IsReady)
        {
            failureResult = LobbyExitResult.Failed(
                userId,
                LobbyPlayerExitReason.VoluntaryLeave,
                "The network connection registry is not ready.");

            return false;
        }

        if (!connectionRegistry.TryGetBingoUserId(
                clientId,
                out userId))
        {
            failureResult = LobbyExitResult.Failed(
                userId,
                LobbyPlayerExitReason.VoluntaryLeave,
                "The connected user could not be resolved.");

            return false;
        }

        return true;
    }

    private async Task TryRollbackFailedLobbyEntryAsync()
    {
        NetworkLobbyConnection lobbyConnection = NetworkLobbyConnection.GetLocalConnection();

        if (lobbyConnection == null || networkBootstrap == null || !networkBootstrap.IsConnected)
        {
            return;
        }

        try
        {
            await lobbyConnection.RequestLeaveLobbyAsync();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[NetworkLobbyManager] Failed lobby entry rollback could not complete: {exception.Message}");
        }
    }

    private async Task ShutdownLocalNetworkAfterExitIfPossible()
    {
        if (MultiplayerPlayModeTestContext.IsActive)
        {
            return;
        }

        if (networkBootstrap == null ||
            !networkBootstrap.IsConnected)
        {
            return;
        }

        if (networkBootstrap.IsAuthority &&
            lobbies.Count > 0)
        {
            return;
        }

        float deliveryTime =
            Time.realtimeSinceStartup +
            ExitNotificationDeliverySeconds;

        while (Time.realtimeSinceStartup < deliveryTime)
        {
            await Task.Yield();
        }

        await networkBootstrap.ShutdownAsync();
    }

    #endregion

    #region Custom Lobby Entry

    private bool TryPrepareCustomLobbySearch(
    LobbySetupData lobbySetupData,
    out string relayJoinCode,
    out LobbyEntryResult failureResult)
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

        Lobby lobby = FindCustomLobby(
            searchSetupData?.lobbyCode);

        if (lobby?.Controller == null)
        {
            failureResult = LobbyEntryResult.Failed(
                LobbyEntryFailureType.LobbyNotFound,
                "The Custom lobby could not be found.");

            return false;
        }

        LobbyController controller = lobby.Controller;

        if (controller.HasPassword &&
            !controller.IsPasswordValid(
                searchSetupData?.password))
        {
            failureResult = LobbyEntryResult.Failed(
                LobbyEntryFailureType.InvalidPassword,
                "The Custom lobby password is incorrect.");

            return false;
        }

        if (controller.IsFull)
        {
            failureResult = LobbyEntryResult.Failed(
                LobbyEntryFailureType.LobbyFull,
                "The Custom lobby is full.");

            return false;
        }

        if (!TryGetRelayJoinCode(
                lobby,
                out relayJoinCode))
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
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.InvalidSetupData,
                "The Custom lobby setup data is missing.");
        }

        switch (customSetupData.actionType)
        {
            case CustomLobbyActionType.HostLobby:
                Lobby newLobby =
                    CreateCustomHostLobby(lobbySetupData);

                if (newLobby == null)
                {
                    return LobbyEntryResult.Failed(
                        LobbyEntryFailureType.LobbyCreationFailed,
                        "The Custom lobby could not be created.");
                }

                return AddPlayerToLobby(
                    newLobby,
                    lobbySetupData.userData,
                    true);

            case CustomLobbyActionType.SearchLobby:
                return ProcessCustomLobbySearch(
                    lobbySetupData);

            default:
                return LobbyEntryResult.Failed(
                    LobbyEntryFailureType.InvalidSetupData,
                    "The Custom lobby action is invalid.");
        }
    }

    private LobbyEntryResult ProcessCustomLobbySearch(LobbySetupData lobbySetupData)
    {
        CustomSearchLobbySetupData searchSetupData =
            lobbySetupData?.customSetupData?.searchSetupData;

        if (searchSetupData == null)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.InvalidSetupData,
                "The Custom lobby search data is missing.");
        }

        Lobby lobby = FindCustomLobby(
            searchSetupData.lobbyCode);

        if (lobby == null &&
            MultiplayerPlayModeTestContext.IsActive &&
            string.IsNullOrWhiteSpace(searchSetupData.lobbyCode))
        {
            lobby = FindSimulationCustomLobby();
        }

        if (lobby?.Controller == null)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.LobbyNotFound,
                "The Custom lobby could not be found.");
        }

        LobbyController controller = lobby.Controller;

        if (controller.HasPassword &&
            !controller.IsPasswordValid(
                searchSetupData.password))
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.InvalidPassword,
                "The Custom lobby password is incorrect.");
        }

        if (controller.IsFull)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.LobbyFull,
                "The Custom lobby is full.");
        }

        return AddPlayerToLobby(
            lobby,
            lobbySetupData.userData,
            false);
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
        if (lobbySetupData == null ||
            networkBootstrap == null)
        {
            return null;
        }

        Lobby lobby =
            CreateLobby(
                lobbySetupData);

        if (lobby?.Controller == null ||
            string.IsNullOrWhiteSpace(
                lobby.Controller.RoomCode))
        {
            DeleteLobby(lobby);
            return null;
        }

        string relayJoinCode =
            networkBootstrap.RelayJoinCode;

        if (!string.IsNullOrWhiteSpace(
                relayJoinCode))
        {
            relayJoinCodeByLobbyId[
                lobby.GetLobbyId()] =
                    relayJoinCode;
        }

        return lobby;
    }

    private Lobby FindCustomLobby(string requestedRoomCode)
    {
        if (lobbies.Count == 0 ||
            string.IsNullOrWhiteSpace(requestedRoomCode))
        {
            return null;
        }

        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby lobby = lobbies[i];

            if (lobby == null ||
                lobby.playMode != MainMenuPlayMode.Custom)
            {
                continue;
            }

            LobbyController controller = lobby.Controller;

            if (controller != null &&
                controller.MatchesRoomCode(
                    requestedRoomCode))
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

        if (!relayJoinCodeByLobbyId.TryGetValue(
                lobby.GetLobbyId(),
                out string storedRelayJoinCode))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                storedRelayJoinCode))
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

        List<Lobby> matchingBallCountLobbies = FindOnlineLobbiesByBallCount(
            matchingGameModeLobbies,
            onlineSetupData.ballCountType);

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

    private List<Lobby> FindOnlineLobbiesByBallCount(
    List<Lobby> gameModeLobbies,
    BingoBallCountType ballCountType)
    {
        List<Lobby> matchingLobbies = new List<Lobby>();

        if (gameModeLobbies == null)
        {
            return matchingLobbies;
        }

        for (int i = 0; i < gameModeLobbies.Count; i++)
        {
            Lobby lobby = gameModeLobbies[i];

            if (lobby?.Controller == null ||
                lobby.Controller.BallCountType != ballCountType)
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

    private LobbyEntryResult AddPlayerToLobby(
        Lobby lobby,
        UserData userData,
        bool isHost)
    {
        if (lobby?.Controller == null)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.LobbyNotFound,
                "The lobby could not be found.");
        }

        if (userData == null || !userData.HasUser)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.UserMissing,
                "The user information is missing.");
        }

        LobbyController controller = lobby.Controller;

        if (controller.HasPlayer(userData.userId))
        {
            return LobbyEntryResult.Succeeded(lobby);
        }

        if (controller.IsFull)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.LobbyFull,
                "The lobby is full.");
        }

        LobbyPlayerData playerData = new LobbyPlayerData(userData, isHost);
        playerData.isLobbySceneReady = false;

        if (!controller.AddPlayer(playerData))
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.LobbyJoinFailed, "The player could not be added to the lobby.");
        }

        BroadcastLobbyView(lobby);

        return LobbyEntryResult.Succeeded(lobby);
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

            if (lobby?.Controller != null &&
                lobby.Controller.HasPlayer(userId))
            {
                return lobby;
            }
        }

        return null;
    }

    private Lobby FindLobbyByController(
        LobbyController controller)
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
        if (networkBootstrap == null ||
            !networkBootstrap.IsAuthority ||
            connectionRegistry == null ||
            !connectionRegistry.IsReady)
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
        if (lobby?.Controller == null ||
            connectionRegistry == null ||
            !connectionRegistry.IsReady)
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
            new LobbyPlayerBoardUpdateData(
                lobby.GetLobbyId(),
                userId,
                playerBoard.boardData);

        IReadOnlyList<LobbyPlayerData> players = lobby.Controller.Players;

        for (int i = 0; i < players.Count; i++)
        {
            string targetUserId = players[i]?.userData?.userId;

            if (string.IsNullOrWhiteSpace(targetUserId) ||
                !connectionRegistry.TryGetClientId(targetUserId, out ulong clientId))
            {
                continue;
            }

            NetworkLobbyConnection.TrySendPlayerBoardUpdate(clientId, updateData);
        }
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

    private void OnConnectionRemoved(
        ulong clientId,
        string userId)
    {
        if (networkBootstrap == null ||
            !networkBootstrap.IsAuthority)
        {
            return;
        }

        RemovePlayerFromLobby(
            userId,
            LobbyPlayerExitReason.Disconnected);
    }

    #endregion

    #region Network Connection

    private bool IsValidNetworkSetup(
        LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null ||
            lobbySetupData.userData == null ||
            !lobbySetupData.userData.HasUser)
        {
            return false;
        }

        switch (lobbySetupData.playMode)
        {
            case MainMenuPlayMode.Online:
                return lobbySetupData.onlineSetupData != null;

            case MainMenuPlayMode.Custom:
                return lobbySetupData.customSetupData != null;

            default:
                return false;
        }
    }

    private async Task<bool> EnsureNetworkConnectionAsync(
        LobbySetupData lobbySetupData,
        string customRelayJoinCode)
    {
        if (networkBootstrap == null ||
            !networkBootstrap.IsReady)
        {
            return false;
        }

        if (MultiplayerPlayModeTestContext.IsActive)
        {
            float timeoutTimes =
                Time.realtimeSinceStartup +
                ConnectionTimeoutSeconds;

            while (!HasUsableNetworkConnection())
            {
                if (Time.realtimeSinceStartup >= timeoutTimes)
                {
                    Debug.LogWarning(
                        "[Bingo] Multiplayer Play Mode connection timed out.\n" +
                        $"Player: {MultiplayerPlayModeTestContext.PlayerNumber}\n" +
                        $"Connection State: {networkBootstrap.ConnectionState}\n" +
                        $"Is Connected: {networkBootstrap.IsConnected}\n" +
                        $"Is Client: {networkBootstrap.IsClient}\n" +
                        $"Is Authority: {networkBootstrap.IsAuthority}\n" +
                        $"Has Lobby Connection: {NetworkLobbyConnection.GetLocalConnection() != null}");

                    return false;
                }

                await Task.Yield();
            }

            return true;
        }

        if (networkBootstrap.IsConnected)
        {
            if (NetworkLobbyConnection.local != null)
            {
                return true;
            }

            if (networkBootstrap.IsAuthority &&
                lobbies.Count > 0)
            {
                Debug.LogWarning(
                    "[NetworkLobbyManager] The network is connected, but the local lobby connection is missing while authority lobbies are still active.");

                return false;
            }

            bool shutdownComplete =
                await networkBootstrap.ShutdownAsync();

            if (!shutdownComplete)
            {
                return false;
            }
        }

        string userId =
            lobbySetupData.userData.userId;

        bool started;

        switch (lobbySetupData.playMode)
        {
            case MainMenuPlayMode.Online:
                started =
                    await networkBootstrap
                        .StartRelayHostAsync(userId);
                break;

            case MainMenuPlayMode.Custom:
                CustomLobbySetupData customSetupData =
                    lobbySetupData.customSetupData;

                if (customSetupData == null)
                {
                    return false;
                }

                if (customSetupData.actionType ==
                    CustomLobbyActionType.HostLobby)
                {
                    started =
                        await networkBootstrap
                            .StartRelayHostAsync(userId);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(
                            customRelayJoinCode))
                    {
                        return false;
                    }

                    started =
                        await networkBootstrap
                            .StartRelayClientAsync(
                                userId,
                                customRelayJoinCode);
                }

                break;

            default:
                return false;
        }

        if (!started)
        {
            return false;
        }

        float timeoutTime =
            Time.realtimeSinceStartup +
            ConnectionTimeoutSeconds;

        while (!networkBootstrap.IsConnected)
        {
            if (networkBootstrap.ConnectionState ==
                    NetworkConnectionState.Failed ||
                networkBootstrap.ConnectionState ==
                    NetworkConnectionState.Disconnected)
            {
                return false;
            }

            if (Time.realtimeSinceStartup >=
                timeoutTime)
            {
                return false;
            }

            await Task.Yield();
        }

        return true;
    }

    private async Task<NetworkLobbyConnection> WaitForLocalLobbyConnectionAsync()
    {
        float timeoutTime = Time.realtimeSinceStartup + LobbyConnectionTimeoutSeconds;

        while (true)
        {
            NetworkLobbyConnection lobbyConnection = NetworkLobbyConnection.GetLocalConnection();

            if (lobbyConnection != null)
            {
                return lobbyConnection;
            }

            if (Time.realtimeSinceStartup >= timeoutTime)
            {
                return null;
            }

            await Task.Yield();
        }
    }

    #endregion

    #region Setup

    private bool CanInitialize()
    {
        if (NetworkRoot.instance == null ||
            !NetworkRoot.instance.IsReady)
        {
            return false;
        }

        if (NetworkBootstrap.instance == null ||
            !NetworkBootstrap.instance.IsReady)
        {
            return false;
        }

        if (NetworkConnectionRegistry.instance == null ||
            !NetworkConnectionRegistry.instance.IsReady)
        {
            return false;
        }

        if (NetworkBotManager.instance == null ||
            !NetworkBotManager.instance.IsReady)
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

    public void ProcessAuthoritySetPlayerReady(ulong clientId, bool isReady)
    {
        if (networkBootstrap == null ||
            !networkBootstrap.IsAuthority ||
            connectionRegistry == null ||
            !connectionRegistry.IsReady)
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

    #endregion
}
