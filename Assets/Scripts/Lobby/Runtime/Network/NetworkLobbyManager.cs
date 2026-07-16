using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkLobbyManager :
    MonoBehaviour,
    ILobbyService
{
    public static NetworkLobbyManager instance;

    private const float ConnectionTimeoutSeconds = 30f;
    private const float LobbyConnectionTimeoutSeconds = 15f;

    private readonly List<Lobby> lobbies =
        new List<Lobby>();

    private readonly Dictionary<string, LobbySetupData>
        lobbySetupDataById =
            new Dictionary<string, LobbySetupData>();

    private readonly Dictionary<string, string>
    customLobbyIdByCode =
        new Dictionary<string, string>();

    private bool isReady;

    private NetworkRoot networkRoot;
    private NetworkBootstrap networkBootstrap;

    private Lobby currentLobby;

    public SessionRuntimeType RuntimeType =>
        SessionRuntimeType.Network;

    public bool IsReady => isReady;

    public IReadOnlyList<Lobby> Lobbies =>
        lobbies;

    public Lobby CurrentLobby =>
        currentLobby;


    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        networkRoot =
            GetComponentInParent<NetworkRoot>();

        if (networkRoot == null ||
            !networkRoot.IsPrimaryInstance)
        {
            enabled = false;
            return;
        }

        if (instance != null &&
            instance != this)
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
        networkBootstrap =
            NetworkBootstrap.instance;

        isReady = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public async Task<LobbyEntryResult> EnterLobbyAsync(
        LobbySetupData lobbySetupData)
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

        bool connectionReady =
            await EnsureNetworkConnectionAsync(
                lobbySetupData);

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
                LobbyEntryFailureType
                    .NetworkLobbyConnectionUnavailable,
                "The network lobby connection was not available.");
        }

        LobbyEntryResult result =
            await lobbyConnection
                .RequestEnterLobbyAsync(
                    lobbySetupData);

        if (result != null &&
            result.success)
        {
            currentLobby = result.lobby;
        }

        return result ??
               LobbyEntryResult.Failed(
                   LobbyEntryFailureType.Unknown,
                   "The network lobby did not return a result.");
    }

    public LobbyEntryResult ProcessAuthorityLobbyEntry(
        LobbySetupData lobbySetupData,
        ulong senderClientId)
    {
        if (networkBootstrap == null ||
            !networkBootstrap.IsAuthority)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.ServiceUnavailable,
                "This process is not the network authority.");
        }

        NetworkConnectionRegistry connectionRegistry =
            NetworkConnectionRegistry.instance;

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

        if (lobbySetupData.userData.userId !=
            registeredUserId)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.UserMissing,
                "The lobby user does not match the connected user.");
        }

        Lobby existingUserLobby =
            FindUserLobby(registeredUserId);

        if (existingUserLobby != null)
        {
            return LobbyEntryResult.Succeeded(
                existingUserLobby);
        }

        Lobby selectedLobby;

        switch (lobbySetupData.playMode)
        {
            case MainMenuPlayMode.Online:
                selectedLobby =
                    FindOrCreateOnlineLobby(
                        lobbySetupData);
                break;

            case MainMenuPlayMode.Custom:
                return ProcessCustomLobbyEntry(
                    lobbySetupData);

            default:
                return LobbyEntryResult.Failed(
                    LobbyEntryFailureType.InvalidSetupData,
                    "The network lobby mode is not valid.");
        }

        if (selectedLobby == null)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.LobbyCreationFailed,
                "The Online lobby could not be created.");
        }

        return AddPlayerToLobby(
            selectedLobby,
            lobbySetupData.userData);
    }

    private LobbyEntryResult ProcessCustomLobbyEntry(
        LobbySetupData lobbySetupData)
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
                {
                    Lobby newLobby =
                        CreateCustomHostLobby(
                            lobbySetupData);

                    if (newLobby == null)
                    {
                        return LobbyEntryResult.Failed(
                            LobbyEntryFailureType
                                .LobbyCreationFailed,
                            "The Custom lobby could not be created.");
                    }

                    return AddPlayerToLobby(
                        newLobby,
                        lobbySetupData.userData);
                }

            case CustomLobbyActionType.SearchLobby:
                {
                    Lobby lobby =
                        FindCustomLobby(
                            customSetupData
                                .searchSetupData
                                ?.lobbyCode);

                    if (lobby == null)
                    {
                        return LobbyEntryResult.Failed(
                            LobbyEntryFailureType.LobbyNotFound,
                            "The Custom lobby could not be found.");
                    }

                    if (!IsCustomPasswordValid(
                            lobby,
                            customSetupData
                                .searchSetupData
                                ?.password))
                    {
                        return LobbyEntryResult.Failed(
                            LobbyEntryFailureType.InvalidPassword,
                            "The Custom lobby password is incorrect.");
                    }

                    if (IsLobbyFull(lobby))
                    {
                        return LobbyEntryResult.Failed(
                            LobbyEntryFailureType.LobbyFull,
                            "The Custom lobby is full.");
                    }

                    return AddPlayerToLobby(
                        lobby,
                        lobbySetupData.userData);
                }

            default:
                return LobbyEntryResult.Failed(
                    LobbyEntryFailureType.InvalidSetupData,
                    "The Custom lobby action is invalid.");
        }
    }

    private LobbyEntryResult AddPlayerToLobby(
        Lobby lobby,
        UserData userData)
    {
        if (lobby == null)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.LobbyNotFound,
                "The lobby could not be found.");
        }

        if (userData == null ||
            !userData.HasUser)
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.UserMissing,
                "The user information is missing.");
        }

        if (lobby.HasPlayer(userData.userId))
        {
            return LobbyEntryResult.Succeeded(lobby);
        }

        if (IsLobbyFull(lobby))
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.LobbyFull,
                "The lobby is full.");
        }

        bool isHost =
            lobby.players.Count == 0;

        LobbyPlayerData playerData =
            new LobbyPlayerData(
                userData,
                isHost);

        if (!lobby.AddPlayer(playerData))
        {
            return LobbyEntryResult.Failed(
                LobbyEntryFailureType.LobbyJoinFailed,
                "The player could not be added to the lobby.");
        }

        return LobbyEntryResult.Succeeded(lobby);
    }

    private Lobby FindOrCreateOnlineLobby(
    LobbySetupData lobbySetupData)
    {
        List<Lobby> matchingLobbies =
            FindMatchingOnlineLobbies(
                lobbySetupData);

        if (matchingLobbies.Count > 0)
        {
            int randomIndex =
                UnityEngine.Random.Range(
                    0,
                    matchingLobbies.Count);

            return matchingLobbies[randomIndex];
        }

        return CreateLobby(lobbySetupData);
    }

    private List<Lobby> FindMatchingOnlineLobbies(
        LobbySetupData requestedSetupData)
    {
        List<Lobby> matchingLobbies =
            new List<Lobby>();

        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby lobby = lobbies[i];

            if (!IsEligibleOnlineLobby(
                    lobby,
                    requestedSetupData))
            {
                continue;
            }

            matchingLobbies.Add(lobby);
        }

        return matchingLobbies;
    }

    private bool IsEligibleOnlineLobby(
    Lobby lobby,
    LobbySetupData requestedSetupData)
    {
        if (lobby == null)
        {
            return false;
        }

        if (lobby.playMode !=
            MainMenuPlayMode.Online)
        {
            return false;
        }

        if (lobby.lobbyState !=
            LobbyState.Open)
        {
            return false;
        }

        if (IsLobbyFull(lobby))
        {
            return false;
        }

        string lobbyId =
            lobby.GetLobbyId();

        if (!lobbySetupDataById.TryGetValue(
                lobbyId,
                out LobbySetupData existingSetupData))
        {
            return false;
        }

        return DoOnlineSettingsMatch(
            existingSetupData,
            requestedSetupData);
    }

    private bool DoOnlineSettingsMatch(
        LobbySetupData existingSetupData,
        LobbySetupData requestedSetupData)
    {
        if (existingSetupData?.onlineSetupData == null ||
            requestedSetupData?.onlineSetupData == null)
        {
            return false;
        }

        OnlineLobbySetupData existing =
            existingSetupData.onlineSetupData;

        OnlineLobbySetupData requested =
            requestedSetupData.onlineSetupData;

        return
            existing.gameModeType ==
            requested.gameModeType
            &&
            existing.searchType ==
            requested.searchType
            &&
            existing.ballCountType ==
            requested.ballCountType;
    }

    private Lobby CreateCustomHostLobby(
    LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null)
        {
            return null;
        }

        string relayCode =
            networkBootstrap != null
                ? networkBootstrap.RelayJoinCode
                : string.Empty;

        if (string.IsNullOrWhiteSpace(relayCode))
        {
            return null;
        }

        string normalizedCode =
            relayCode
                .Trim()
                .ToUpperInvariant();

        if (customLobbyIdByCode.ContainsKey(
                normalizedCode))
        {
            return null;
        }

        Lobby lobby =
            CreateLobby(lobbySetupData);

        if (lobby == null)
        {
            return null;
        }

        customLobbyIdByCode[normalizedCode] =
            lobby.GetLobbyId();

        return lobby;
    }

    private Lobby CreateLobby(
    LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null)
        {
            return null;
        }

        Lobby lobby =
            new Lobby(lobbySetupData.playMode);

        string lobbyId =
            lobby.GetLobbyId();

        lobbies.Add(lobby);

        lobbySetupDataById[lobbyId] =
            CloneSetupData(lobbySetupData);

        return lobby;
    }

    private Lobby FindCustomLobby(string lobbyCode)
    {
        if (string.IsNullOrWhiteSpace(lobbyCode))
        {
            return null;
        }

        string normalizedCode =
            lobbyCode
                .Trim()
                .ToUpperInvariant();

        if (!customLobbyIdByCode.TryGetValue(
                normalizedCode,
                out string lobbyId))
        {
            return null;
        }

        return FindLobby(lobbyId);
    }

    private Lobby FindLobby(string lobbyId)
    {
        if (string.IsNullOrWhiteSpace(lobbyId))
        {
            return null;
        }

        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby lobby = lobbies[i];

            if (lobby == null)
            {
                continue;
            }

            if (string.Equals(
                    lobby.GetLobbyId(),
                    lobbyId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return lobby;
            }
        }

        return null;
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

            if (lobby == null)
            {
                continue;
            }

            if (lobby.HasPlayer(userId))
            {
                return lobby;
            }
        }

        return null;
    }

    private bool IsCustomPasswordValid(
    Lobby lobby,
    string requestedPassword)
    {
        if (lobby == null)
        {
            return false;
        }

        string lobbyId =
            lobby.GetLobbyId();

        if (!lobbySetupDataById.TryGetValue(
                lobbyId,
                out LobbySetupData existingSetupData))
        {
            return false;
        }

        string expectedPassword =
            existingSetupData
                ?.customSetupData
                ?.hostSetupData
                ?.password
            ?? string.Empty;

        requestedPassword ??= string.Empty;

        return string.Equals(
            expectedPassword,
            requestedPassword,
            StringComparison.Ordinal);
    }

    private bool IsLobbyFull(Lobby lobby)
    {
        if (lobby == null)
        {
            return true;
        }

        string lobbyId =
            lobby.GetLobbyId();

        if (!lobbySetupDataById.TryGetValue(
                lobbyId,
                out LobbySetupData setupData))
        {
            return true;
        }

        if (lobby.playMode ==
            MainMenuPlayMode.Custom)
        {
            CustomHostLobbySetupData hostSetupData =
                setupData
                    ?.customSetupData
                    ?.hostSetupData;

            if (hostSetupData == null)
            {
                return true;
            }

            if (hostSetupData.unlimitedPlayers)
            {
                return false;
            }

            return lobby.players.Count >=
                   hostSetupData.maxPlayers;
        }

        if (lobby.playMode ==
            MainMenuPlayMode.Online)
        {
            int maximumConnections =
                networkRoot?.RuntimeConfig != null
                    ? networkRoot
                        .RuntimeConfig
                        .MaximumConnections
                    : int.MaxValue;

            return lobby.players.Count >=
                   maximumConnections;
        }

        return false;
    }

    private bool IsValidNetworkSetup(
        LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null)
        {
            return false;
        }

        if (lobbySetupData.userData == null ||
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
        LobbySetupData lobbySetupData)
    {
        if (networkBootstrap == null ||
            !networkBootstrap.IsReady)
        {
            return false;
        }

        if (networkBootstrap.IsConnected)
        {
            return true;
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
                {
                    CustomLobbySetupData customSetupData =
                        lobbySetupData.customSetupData;

                    if (customSetupData.actionType ==
                        CustomLobbyActionType.HostLobby)
                    {
                        started =
                            await networkBootstrap
                                .StartRelayHostAsync(userId);
                    }
                    else
                    {
                        string joinCode =
                            customSetupData
                                .searchSetupData
                                ?.lobbyCode;

                        started =
                            await networkBootstrap
                                .StartRelayClientAsync(
                                    userId,
                                    joinCode);
                    }

                    break;
                }

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

    private async Task<NetworkLobbyConnection>
        WaitForLocalLobbyConnectionAsync()
    {
        float timeoutTime =
            Time.realtimeSinceStartup +
            LobbyConnectionTimeoutSeconds;

        while (NetworkLobbyConnection.local == null)
        {
            if (Time.realtimeSinceStartup >=
                timeoutTime)
            {
                return null;
            }

            await Task.Yield();
        }

        return NetworkLobbyConnection.local;
    }

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

        return true;
    }

    private LobbySetupData CloneSetupData(
        LobbySetupData source)
    {
        if (source == null)
        {
            return null;
        }

        string json =
            JsonUtility.ToJson(source);

        return JsonUtility
            .FromJson<LobbySetupData>(json);
    }
}