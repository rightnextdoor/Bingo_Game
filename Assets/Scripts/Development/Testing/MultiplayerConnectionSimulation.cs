using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using Unity.Multiplayer.PlayMode;
#endif

[DisallowMultipleComponent]
public class MultiplayerConnectionSimulation : MonoBehaviour
{
    [Header("Player 2 Connections")]
    [SerializeField] private bool player2OnlineConnected = true;
    [SerializeField] private bool player2LobbyGameConnected = true;

    [Header("Player 3 Connections")]
    [SerializeField] private bool player3OnlineConnected = true;
    [SerializeField] private bool player3LobbyGameConnected = true;

    [Header("Player 4 Connections")]
    [SerializeField] private bool player4OnlineConnected = true;
    [SerializeField] private bool player4LobbyGameConnected = true;

#if UNITY_EDITOR
    private const string KeyPrefix = "BingoGame.MultiplayerConnectionSimulation.";
    private const string SharedStateFileName = "BingoGame.MultiplayerConnectionSimulation.json";
    private const string LobbyRecoveryFilePrefix = "BingoGame.LobbyRecoveryAbandoned.Player";

    [Serializable]
    private sealed class LobbyRecoveryAbandonedState
    {
        public int schemaVersion = 1;
        public string lobbyId;
    }

    [Serializable]
    private sealed class SharedConnectionState
    {
        public int schemaVersion = 1;
        public bool player2Online;
        public bool player2LobbyGame;
        public bool player3Online;
        public bool player3LobbyGame;
        public bool player4Online;
        public bool player4LobbyGame;

        public bool Online(int player) => player switch
        {
            2 => player2Online,
            3 => player3Online,
            _ => player4Online
        };

        public bool LobbyGame(int player) => player switch
        {
            2 => player2LobbyGame,
            3 => player3LobbyGame,
            _ => player4LobbyGame
        };
    }

    private static readonly bool[] savedOnline = { true, true, true };
    private static readonly bool[] savedLobbyGame = { true, true, true };
    private static bool hasSessionSettings;

    private readonly bool[] observedOnline = new bool[3];
    private readonly bool[] observedLobbyGame = new bool[3];
    private readonly ulong[] pendingDisconnectClientIds = { ulong.MaxValue, ulong.MaxValue, ulong.MaxValue };
    private bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        hasSessionSettings = false;

        for (int index = 0; index < 3; index++)
        {
            savedOnline[index] = true;
            savedLobbyGame[index] = true;
        }
    }

    [InitializeOnLoadMethod]
    private static void RegisterPlayModeReset()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode || !CurrentPlayer.IsMainEditor)
        {
            return;
        }

        // A stopped Play session must not leave a client blocked in the next one.
        for (int player = 2; player <= 4; player++)
        {
            savedOnline[player - 2] = true;
            savedLobbyGame[player - 2] = true;
            PublishPlayerState(player, true, true);
            ClearLobbyRecoveryAbandoned(player);
        }
    }

    private void Update()
    {
        // Player 1's Development Inspector is the only control surface.
        if (!MultiplayerPlayModeTestContext.IsHost)
        {
            return;
        }

        InitializeHostControls();

        for (int player = 2; player <= 4; player++)
        {
            SyncHostControl(player);
            DisconnectBlockedClient(player);
        }
    }

    private void InitializeHostControls()
    {
        if (initialized)
        {
            return;
        }

        if (!hasSessionSettings)
        {
            for (int player = 2; player <= 4; player++)
            {
                ClearLobbyRecoveryAbandoned(player);
            }
        }

        for (int player = 2; player <= 4; player++)
        {
            int index = player - 2;

            if (hasSessionSettings)
            {
                SetOnlineField(player, savedOnline[index]);
                SetLobbyGameField(player, savedLobbyGame[index]);
            }
            else
            {
                savedOnline[index] = GetOnlineField(player);
                savedLobbyGame[index] = savedOnline[index] && GetLobbyGameField(player);
                SetLobbyGameField(player, savedLobbyGame[index]);
            }

            observedOnline[index] = savedOnline[index];
            observedLobbyGame[index] = savedLobbyGame[index];
            PublishPlayerState(player, savedOnline[index], savedLobbyGame[index]);
        }

        hasSessionSettings = true;
        initialized = true;
    }

    private void SyncHostControl(int player)
    {
        int index = player - 2;
        bool online = GetOnlineField(player);
        bool lobbyGame = GetLobbyGameField(player);

        if (online != observedOnline[index])
        {
            // Online controls both switches; lobby/game can be changed separately.
            lobbyGame = online;
            SetLobbyGameField(player, lobbyGame);
        }
        else if (!online && lobbyGame)
        {
            lobbyGame = false;
            SetLobbyGameField(player, false);
        }

        if (online == observedOnline[index] && lobbyGame == observedLobbyGame[index])
        {
            return;
        }

        observedOnline[index] = savedOnline[index] = online;
        observedLobbyGame[index] = savedLobbyGame[index] = lobbyGame;
        PublishPlayerState(player, online, lobbyGame);

        if (lobbyGame)
        {
            pendingDisconnectClientIds[index] = ulong.MaxValue;
        }
    }

    private void DisconnectBlockedClient(int player)
    {
        int index = player - 2;

        if (savedLobbyGame[index])
        {
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        NetworkConnectionRegistry registry = NetworkConnectionRegistry.instance;

        if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer ||
            registry == null || !registry.IsReady)
        {
            return;
        }

        string userId = MultiplayerPlayModeTestContext.GetUserId(player);

        if (!registry.TryGetClientId(userId, out ulong clientId) ||
            clientId == NetworkManager.ServerClientId ||
            clientId == pendingDisconnectClientIds[index])
        {
            return;
        }

        // Approval registers the user before Netcode spawns its player object.
        // Disconnecting in that gap removes the registry entry that OnNetworkSpawn needs.
        if (!networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
            client.PlayerObject == null)
        {
            return;
        }

        // Keep the old simulation's server-side disconnect behavior.
        pendingDisconnectClientIds[index] = clientId;
        networkManager.DisconnectClient(clientId);
    }

    private bool GetOnlineField(int player) => player switch
    {
        2 => player2OnlineConnected,
        3 => player3OnlineConnected,
        _ => player4OnlineConnected
    };

    private bool GetLobbyGameField(int player) => player switch
    {
        2 => player2LobbyGameConnected,
        3 => player3LobbyGameConnected,
        _ => player4LobbyGameConnected
    };

    private void SetOnlineField(int player, bool value)
    {
        switch (player)
        {
            case 2: player2OnlineConnected = value; break;
            case 3: player3OnlineConnected = value; break;
            case 4: player4OnlineConnected = value; break;
        }
    }

    private void SetLobbyGameField(int player, bool value)
    {
        switch (player)
        {
            case 2: player2LobbyGameConnected = value; break;
            case 3: player3LobbyGameConnected = value; break;
            case 4: player4LobbyGameConnected = value; break;
        }
    }

    private static void PublishPlayerState(int player, bool online, bool lobbyGame)
    {
        EditorPrefs.SetBool(GetOnlineKey(player), online);
        EditorPrefs.SetBool(GetLobbyGameKey(player), online && lobbyGame);
        WriteSharedState();
    }

    private static void WriteSharedState()
    {
        string path = GetSharedStatePath();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        SharedConnectionState state = new SharedConnectionState
        {
            player2Online = savedOnline[0],
            player2LobbyGame = savedLobbyGame[0],
            player3Online = savedOnline[1],
            player3LobbyGame = savedLobbyGame[1],
            player4Online = savedOnline[2],
            player4LobbyGame = savedLobbyGame[2]
        };

        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(state));
        }
        catch (IOException exception)
        {
            Debug.LogWarning($"[MultiplayerConnectionSimulation] Could not publish test connection state: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            Debug.LogWarning($"[MultiplayerConnectionSimulation] Could not publish test connection state: {exception.Message}");
        }
    }

    private static bool TryReadSharedState(out SharedConnectionState state)
    {
        state = null;
        string path = GetSharedStatePath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            state = JsonUtility.FromJson<SharedConnectionState>(File.ReadAllText(path));
            return state != null && state.schemaVersion == 1;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string GetSharedStatePath()
    {
        string projectPath = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectPath))
        {
            return string.Empty;
        }

        // MPPM virtual projects live under the real project's Library/VP folder.
        // Resolve back to that project so all editor processes read the same file.
        string normalized = projectPath.Replace('\\', '/');
        int virtualProjectIndex = normalized.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase);
        if (virtualProjectIndex >= 0)
        {
            projectPath = normalized.Substring(0, virtualProjectIndex);
        }

        return Path.Combine(projectPath, "Library", "VP", SharedStateFileName);
    }

    public static void ReportLobbyRecoveryAbandoned(string lobbyId)
    {
        int player = MultiplayerPlayModeTestContext.PlayerNumber;
        string path = GetLobbyRecoveryPath(player);

        if (string.IsNullOrWhiteSpace(lobbyId) || string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(new LobbyRecoveryAbandonedState
            {
                lobbyId = lobbyId
            }));
        }
        catch (IOException exception)
        {
            Debug.LogWarning($"[MultiplayerConnectionSimulation] Could not report lobby recovery failure: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            Debug.LogWarning($"[MultiplayerConnectionSimulation] Could not report lobby recovery failure: {exception.Message}");
        }
    }

    public static bool TryConsumeLobbyRecoveryAbandoned(string userId, string lobbyId)
    {
        if (string.IsNullOrWhiteSpace(lobbyId))
        {
            return false;
        }

        for (int player = 2; player <= 4; player++)
        {
            if (!string.Equals(userId, MultiplayerPlayModeTestContext.GetUserId(player), StringComparison.Ordinal))
            {
                continue;
            }

            string path = GetLobbyRecoveryPath(player);

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                LobbyRecoveryAbandonedState state =
                    JsonUtility.FromJson<LobbyRecoveryAbandonedState>(File.ReadAllText(path));

                if (state?.schemaVersion != 1)
                {
                    return false;
                }

                File.Delete(path);
                return string.Equals(state.lobbyId, lobbyId, StringComparison.Ordinal);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        return false;
    }

    private static void ClearLobbyRecoveryAbandoned(int player)
    {
        string path = GetLobbyRecoveryPath(player);

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string GetLobbyRecoveryPath(int player)
    {
        if (player < 2 || player > 4)
        {
            return string.Empty;
        }

        string sharedStatePath = GetSharedStatePath();
        return string.IsNullOrEmpty(sharedStatePath)
            ? string.Empty
            : Path.Combine(Path.GetDirectoryName(sharedStatePath),
                $"{LobbyRecoveryFilePrefix}{player}.json");
    }

    // NetworkRoot already survives scene changes. Its recovery manager calls this
    // for the matching test client, so no second simulator object is required.
    public static void ApplyClientConnectionState()
    {
        int player = MultiplayerPlayModeTestContext.PlayerNumber;

        if (player < 2 || player > 4)
        {
            return;
        }

        bool hasSharedState = TryReadSharedState(out SharedConnectionState sharedState);
        bool online = hasSharedState
            ? sharedState.Online(player)
            : EditorPrefs.GetBool(GetOnlineKey(player), true);
        bool lobbyGame = online && (hasSharedState
            ? sharedState.LobbyGame(player)
            : EditorPrefs.GetBool(GetLobbyGameKey(player), true));

        OnlineConnectionManager onlineManager = OnlineConnectionManager.instance;
        if (onlineManager?.IsReady == true)
        {
            onlineManager.SetConnectionAvailableForTesting(online);
        }

        NetworkBootstrap bootstrap = NetworkBootstrap.instance;
        if (bootstrap?.IsReady == true)
        {
            bootstrap.SetConnectionAvailableForTesting(lobbyGame);
        }
    }

    private static string GetOnlineKey(int player) => KeyPrefix + player + ".Online";
    private static string GetLobbyGameKey(int player) => KeyPrefix + player + ".LobbyGame";
#endif
}
