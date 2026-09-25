using System;
using System.Threading.Tasks;
using UnityEngine;

public enum ConnectionRecoveryResult
{
    Connected,
    OnlineUnavailable,
    MultiplayerUnavailable,
    Cancelled
}

// Shared by gameplay now and by other online features later. This service owns
// retry/presentation only; callers retain responsibility for their own data.
[DisallowMultipleComponent]
public class ConnectionRecoveryManager : MonoBehaviour
{
    public static ConnectionRecoveryManager instance;

    private const int MaximumAttempts = 3;
    private const float MinimumPopupSeconds = 3f;
    private const float AttemptSpacingSeconds = 1f;
    private const float MultiplayerConnectTimeoutSeconds = 10f;

    private NetworkConnectionMode lastClientMode = NetworkConnectionMode.Offline;
    private string lastRelayJoinCode = string.Empty;
    private Task<ConnectionRecoveryResult> activeRecovery;
    private bool recoveringMainMenu;
    private bool observedSimulationAvailability;
    private bool lastSimulationOnlineAvailable = true;
    private bool lastSimulationMultiplayerAvailable = true;
    private bool recoveringInScene;
    private bool interruptedOnline;
    private bool abortForFinalCountdown;
    private bool expectingNetworkLobbyLoading;
    private GameSceneType recoveringScene;
    private bool hasPausedLocalPresentation;
    private float previousLocalTimeScale;
    private MultiplayerSessionLifecycle subscribedMultiplayerLifecycle;

    public bool IsRecoveringInScene => recoveringInScene;
    public bool IsExpectingNetworkLobbyLoading => expectingNetworkLobbyLoading;

    public void SetExpectingNetworkLobbyLoading(bool expecting)
    {
        expectingNetworkLobbyLoading = expecting;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState() => instance = null;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        SubscribeToMultiplayerLoss();
    }

    private void OnDestroy()
    {
        if (subscribedMultiplayerLifecycle != null)
        {
            subscribedMultiplayerLifecycle.ConnectionLost -= OnMultiplayerConnectionLost;
            subscribedMultiplayerLifecycle = null;
        }

        RestoreLocalPresentation();

        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
#if UNITY_EDITOR
        MultiplayerConnectionSimulation.ApplyClientConnectionState();
        WatchSimulatedAvailability();
#endif
        SubscribeToMultiplayerLoss();

        NetworkBootstrap bootstrap = NetworkBootstrap.instance;

        if (bootstrap?.IsConnected == true && bootstrap.IsClient && !bootstrap.IsHost)
        {
            lastClientMode = bootstrap.ConnectionMode;
            lastRelayJoinCode = bootstrap.RelayJoinCode;
        }

        if (recoveringInScene && recoveringScene == GameSceneType.Lobby &&
            LobbyManager.instance?.CurrentLobbyViewData?.lobbyState == LobbyState.FinalCountdown)
        {
            abortForFinalCountdown = true;
        }
    }

    private void SubscribeToMultiplayerLoss()
    {
        MultiplayerSessionLifecycle lifecycle = MultiplayerSessionLifecycle.instance;

        if (lifecycle == subscribedMultiplayerLifecycle)
        {
            return;
        }

        if (subscribedMultiplayerLifecycle != null)
        {
            subscribedMultiplayerLifecycle.ConnectionLost -= OnMultiplayerConnectionLost;
        }

        subscribedMultiplayerLifecycle = lifecycle;

        if (subscribedMultiplayerLifecycle != null)
        {
            subscribedMultiplayerLifecycle.ConnectionLost += OnMultiplayerConnectionLost;
        }
    }

    private void OnMultiplayerConnectionLost(NetworkConnectionState _)
    {
        HandleMultiplayerConnectionLost();
    }

    public void HandleOnlineConnectionLost()
    {
        NetworkBootstrap bootstrap = NetworkBootstrap.instance;

        if (bootstrap?.IsClient == true && !bootstrap.IsAuthority)
        {
            lastClientMode = bootstrap.ConnectionMode;
            lastRelayJoinCode = bootstrap.RelayJoinCode;
            bootstrap.Shutdown();
        }

        HandleSceneConnectionLost(true);
    }

    public void HandleMultiplayerConnectionLost()
    {
        NetworkBootstrap bootstrap = NetworkBootstrap.instance;

        if (bootstrap != null &&
            (bootstrap.ConnectionMode == NetworkConnectionMode.DirectClient ||
             bootstrap.ConnectionMode == NetworkConnectionMode.RelayClient))
        {
            lastClientMode = bootstrap.ConnectionMode;
            lastRelayJoinCode = bootstrap.RelayJoinCode;
        }

        HandleSceneConnectionLost(false);
    }

    private void HandleSceneConnectionLost(bool online)
    {
        GameSceneManager sceneManager = GameSceneManager.instance;

        if (sceneManager == null)
        {
            return;
        }

        if (sceneManager.CurrentSceneType == GameSceneType.Main)
        {
            _ = RecoverMainMenuAsync();
            return;
        }

        GameSceneType scene = sceneManager.CurrentSceneType;

        if (!IsNetworkSessionScene(scene, sceneManager.IsLoadingScene))
        {
            return;
        }

        interruptedOnline |= online;

        if (recoveringInScene)
        {
            return;
        }

        bool finalLobbyCountdown = scene == GameSceneType.Lobby &&
                                   LobbyManager.instance?.CurrentLobbyViewData?.lobbyState == LobbyState.FinalCountdown;

        if (sceneManager.IsLoadingScene || finalLobbyCountdown)
        {
            FinishSceneConnectionLoss(scene);
            return;
        }

        _ = RecoverCurrentSceneAsync(scene);
    }

#if UNITY_EDITOR
    private void WatchSimulatedAvailability()
    {
        int player = MultiplayerPlayModeTestContext.PlayerNumber;
        if (player < 2 || player > 4)
        {
            return;
        }

        OnlineConnectionManager online = OnlineConnectionManager.instance;
        NetworkBootstrap bootstrap = NetworkBootstrap.instance;
        if (online?.IsReady != true || bootstrap?.IsReady != true)
        {
            return;
        }

        bool onlineAvailable = online.IsConnectionAvailableForTesting;
        bool multiplayerAvailable = bootstrap.IsConnectionAvailableForTesting;
        bool becameUnavailable = observedSimulationAvailability
            ? (lastSimulationOnlineAvailable && !onlineAvailable) ||
              (lastSimulationMultiplayerAvailable && !multiplayerAvailable)
            : !onlineAvailable || !multiplayerAvailable;
        lastSimulationOnlineAvailable = onlineAvailable;
        lastSimulationMultiplayerAvailable = multiplayerAvailable;
        observedSimulationAvailability = true;

        if (becameUnavailable)
        {
            HandleSceneConnectionLost(!onlineAvailable);
        }
    }
#endif

    private async Task RecoverMainMenuAsync()
    {
        if (recoveringMainMenu || GameSceneManager.instance?.CurrentSceneType != GameSceneType.Main)
        {
            return;
        }

        recoveringMainMenu = true;
        ConnectionRecoveryResult result;
        try
        {
            result = await RecoverTrackedAsync(true, null, true);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Main menu connection recovery failed: {exception.Message}");
            result = ConnectionRecoveryResult.MultiplayerUnavailable;
        }
        finally
        {
            recoveringMainMenu = false;
        }

        if (result == ConnectionRecoveryResult.Connected ||
            GameSceneManager.instance?.CurrentSceneType != GameSceneType.Main)
        {
            return;
        }

        PopupManager.instance?.OpenFailurePopup(
            result == ConnectionRecoveryResult.OnlineUnavailable
                ? "Online services are unavailable. Check your connection and try again."
                : "The lobby and game connection is unavailable. Please try again.");
    }

    private bool IsNetworkSessionScene(GameSceneType scene, bool isLoading)
    {
        if (scene == GameSceneType.Lobby)
        {
            LobbyManager lobbyManager = LobbyManager.instance;

            // A direct Lobby launch owns its first connection attempt. Recovery
            // begins only after entry, or while a real scene transition is loading.
            if (lobbyManager?.IsEnteringLobby == true &&
                !lobbyManager.HasEnteredLobby &&
                !isLoading && !expectingNetworkLobbyLoading)
            {
                return false;
            }

            return lobbyManager?.IsLeavingLobby != true &&
                   ((lobbyManager?.RuntimeType == SessionRuntimeType.Network &&
                     (lobbyManager.HasEnteredLobby || lobbyManager.IsEnteringLobby ||
                      lobbyManager.HasPendingLobbySetupData || isLoading)) ||
                    expectingNetworkLobbyLoading);
        }

        if (scene != GameSceneType.Game)
        {
            return false;
        }

        GameSessionManager gameManager = GameSessionManager.instance;

        if (gameManager?.IsLeavingGame == true)
        {
            return false;
        }

        // Direct Game simulation creates a lobby and connection in this scene.
        // Let that startup/retry path finish before in-scene recovery can run.
        if (gameManager?.IsGameSimulationCreationPending == true &&
            !gameManager.HasEnteredGame)
        {
            return false;
        }

        if (gameManager?.RuntimeType == SessionRuntimeType.Network &&
            (gameManager.HasEnteredGame || gameManager.IsEnteringGame ||
             gameManager.EntryState == GameSessionEntryState.WaitingForService || isLoading))
        {
            return true;
        }

        return isLoading && LobbyManager.instance?.RuntimeType == SessionRuntimeType.Network;
    }

    private async Task RecoverCurrentSceneAsync(GameSceneType scene)
    {
        recoveringInScene = true;
        recoveringScene = scene;
        abortForFinalCountdown = false;
        PauseLocalPresentation();

        ConnectionRecoveryResult result;

        try
        {
            result = await RecoverAsync(true);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Connection recovery failed: {exception.Message}");
            result = ConnectionRecoveryResult.MultiplayerUnavailable;
        }

        if (abortForFinalCountdown ||
            GameSceneManager.instance?.CurrentSceneType != scene ||
            GameSceneManager.instance?.IsLoadingScene == true)
        {
            recoveringInScene = false;
            RestoreLocalPresentation();
            FinishSceneConnectionLoss(scene);
            return;
        }

        bool restored = false;

        if (result == ConnectionRecoveryResult.Connected)
        {
            try
            {
                restored = scene == GameSceneType.Game
                    ? await (GameSessionManager.instance?.RestoreCurrentNetworkGameAsync() ?? Task.FromResult(false))
                    : await (LobbyManager.instance?.RestoreCurrentNetworkLobbyAsync() ?? Task.FromResult(false));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not restore the current scene: {exception.Message}");
            }
        }

        recoveringInScene = false;
        RestoreLocalPresentation();
        PopupManager.instance?.CloseReconnectPopup();

        if (restored)
        {
            interruptedOnline = false;

            if (scene == GameSceneType.Game)
            {
                _ = GameSessionManager.instance?.StartPendingRiskSubmitAfterLoadingAsync();
                FindFirstObjectByType<GameController>()?.RefreshAfterConnectionRecovery(
                    GameSessionManager.instance?.CurrentGameSession);
            }

            return;
        }

        FinishSceneConnectionLoss(scene);
    }

    private void FinishSceneConnectionLoss(GameSceneType scene)
    {
        expectingNetworkLobbyLoading = false;
        PopupManager.instance?.CloseReconnectPopup();
        recoveringInScene = false;
        RestoreLocalPresentation();
        bool gameEnded = scene == GameSceneType.Game &&
                         GameSessionManager.instance?.LastConnectionRestoreFoundEndedGame == true;
        string message = gameEnded
            ? "The game ended while you were reconnecting."
            : interruptedOnline
            ? "The connection to online services was lost."
            : scene == GameSceneType.Game
                ? "The connection to the game was lost."
                : "The connection to the lobby was lost.";
        FailureManager.instance?.ReportFailure(
            message,
            interruptedOnline ? FailurePrecedence.Online : FailurePrecedence.SessionConnection,
            FailureDisplayMode.WaitForMain);
        interruptedOnline = false;

        string abandonedLobbyId = scene == GameSceneType.Lobby
            ? LobbyManager.instance?.CurrentLobbyId
            : scene == GameSceneType.Game
                ? GameSessionManager.instance?.CurrentGameSession?.lobbyId
                : string.Empty;

        // Game creation occurs at the start of the lobby's final countdown.
        // If its custom host leaves the Lobby during connection failure, that
        // pre-start Game is cancelled; do not leave its ID for Main Menu rejoin.
        LobbyViewData lobbyViewData = scene == GameSceneType.Lobby
            ? LobbyManager.instance?.CurrentLobbyViewData
            : null;
        if (lobbyViewData?.playMode == MainMenuPlayMode.Custom &&
            lobbyViewData.players != null)
        {
            string localUserId = UserManager.instance?.UserId;

            for (int i = 0; i < lobbyViewData.players.Count; i++)
            {
                LobbyPlayerViewData player = lobbyViewData.players[i];

                if (player?.isHost == true &&
                    string.Equals(player.userId, localUserId, StringComparison.Ordinal))
                {
                    GameSessionManager.instance?.ClearCurrentGame(true);
                    break;
                }
            }
        }

        NetworkLobbyManager.instance?.ProcessAuthorityHostLobbyRecoveryAbandoned(
            abandonedLobbyId, UserManager.instance?.UserId);

        if (scene == GameSceneType.Game)
        {
            GameSessionManager.instance?.ClearCurrentGame(gameEnded);
        }

        LobbyManager.instance?.ClearLocalLobbyAfterConnectionLoss();
        GameSceneManager.instance?.ReturnToMainSceneAfterFailure();
#if UNITY_EDITOR
        // The host has now begun the return-to-Main loading transition.
        // Notify the simulation authority here, not after loading finishes.
        MultiplayerConnectionSimulation.ReportLobbyRecoveryAbandoned(abandonedLobbyId);
#endif
    }

    private void PauseLocalPresentation()
    {
        if (hasPausedLocalPresentation || NetworkBootstrap.instance?.IsAuthority == true)
        {
            return;
        }

        previousLocalTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        hasPausedLocalPresentation = true;
    }

    private void RestoreLocalPresentation()
    {
        if (!hasPausedLocalPresentation)
        {
            return;
        }

        Time.timeScale = previousLocalTimeScale;
        hasPausedLocalPresentation = false;
    }

    public Task<ConnectionRecoveryResult> RecoverAsync(
        bool requireMultiplayer,
        LobbySetupData lobbySetupData = null)
    {
        return RecoverTrackedAsync(requireMultiplayer, lobbySetupData, false);
    }

    public async Task<ConnectionRecoveryResult> PrepareLobbyAfterPlayRecoveryAsync(
        LobbySetupData lobbySetupData)
    {
        if (HasRequiredConnections(true, lobbySetupData, false))
        {
            return ConnectionRecoveryResult.Connected;
        }

        if (OnlineConnectionManager.instance?.IsOnline != true)
        {
            return ConnectionRecoveryResult.OnlineUnavailable;
        }

        return await EnsureMultiplayerAsync(lobbySetupData) &&
               HasRequiredConnections(true, lobbySetupData, false)
            ? ConnectionRecoveryResult.Connected
            : ConnectionRecoveryResult.MultiplayerUnavailable;
    }

    private Task<ConnectionRecoveryResult> RecoverTrackedAsync(
        bool requireMultiplayer,
        LobbySetupData lobbySetupData,
        bool allowIdleMultiplayer)
    {
        if (activeRecovery != null && !activeRecovery.IsCompleted)
        {
            return RecoverAfterActiveAsync(activeRecovery, requireMultiplayer,
                lobbySetupData, allowIdleMultiplayer);
        }

        activeRecovery = RecoverCoreAsync(requireMultiplayer, lobbySetupData, allowIdleMultiplayer);
        return activeRecovery;
    }

    private async Task<ConnectionRecoveryResult> RecoverAfterActiveAsync(
        Task<ConnectionRecoveryResult> previous,
        bool requireMultiplayer,
        LobbySetupData lobbySetupData,
        bool allowIdleMultiplayer)
    {
        await previous;
        if (HasRequiredConnections(requireMultiplayer, lobbySetupData, allowIdleMultiplayer))
        {
            return ConnectionRecoveryResult.Connected;
        }

        return await RecoverTrackedAsync(requireMultiplayer, lobbySetupData, allowIdleMultiplayer);
    }

    private async Task<ConnectionRecoveryResult> RecoverCoreAsync(
        bool requireMultiplayer,
        LobbySetupData lobbySetupData,
        bool allowIdleMultiplayer)
    {
        if (HasRequiredConnections(requireMultiplayer, lobbySetupData, allowIdleMultiplayer))
        {
            return ConnectionRecoveryResult.Connected;
        }

        float openedAt = Time.realtimeSinceStartup;
        ConnectionRecoveryResult result = ConnectionRecoveryResult.OnlineUnavailable;

        for (int attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            if (abortForFinalCountdown)
            {
                result = ConnectionRecoveryResult.Cancelled;
                break;
            }

            bool onlineMissing = OnlineConnectionManager.instance?.IsOnline != true;
            PopupManager.instance?.OpenReconnectPopup(
                onlineMissing
                    ? $"Reconnecting to online services... ({attempt}/{MaximumAttempts})"
                    : $"Reconnecting to multiplayer... ({attempt}/{MaximumAttempts})");

            try
            {
                OnlineConnectionManager online = OnlineConnectionManager.instance;
                bool onlineReady = online != null &&
                                   (online.IsOnline || await online.EnsureConnectedAsync());

                if (!onlineReady)
                {
                    result = ConnectionRecoveryResult.OnlineUnavailable;
                }
                else if (!requireMultiplayer && online.IsOnline)
                {
                    result = ConnectionRecoveryResult.Connected;
                    break;
                }
                else if (allowIdleMultiplayer &&
                         NetworkBootstrap.instance?.IsReady == true &&
                         NetworkBootstrap.instance.IsConnectionAvailableForTesting)
                {
                    result = ConnectionRecoveryResult.Connected;
                    break;
                }
                else if (await EnsureMultiplayerAsync(lobbySetupData) &&
                         HasRequiredConnections(true, lobbySetupData, false))
                {
                    result = ConnectionRecoveryResult.Connected;
                    break;
                }
                else
                {
                    result = ConnectionRecoveryResult.MultiplayerUnavailable;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Connection retry {attempt} failed: {exception.Message}");
                result = OnlineConnectionManager.instance?.IsOnline == true
                    ? ConnectionRecoveryResult.MultiplayerUnavailable
                    : ConnectionRecoveryResult.OnlineUnavailable;
            }

            if (attempt < MaximumAttempts)
            {
                float nextAttemptAt = Time.realtimeSinceStartup + AttemptSpacingSeconds;
                while (Time.realtimeSinceStartup < nextAttemptAt && !abortForFinalCountdown)
                {
                    await Task.Yield();
                }
            }
        }

        if (!abortForFinalCountdown)
        {
            while (Time.realtimeSinceStartup - openedAt < MinimumPopupSeconds)
            {
                await Task.Yield();
            }
        }

        if (result == ConnectionRecoveryResult.Connected &&
            !HasRequiredConnections(requireMultiplayer, lobbySetupData, allowIdleMultiplayer))
        {
            result = OnlineConnectionManager.instance?.IsOnline == true
                ? ConnectionRecoveryResult.MultiplayerUnavailable
                : ConnectionRecoveryResult.OnlineUnavailable;
        }

        if (!recoveringInScene)
        {
            PopupManager.instance?.CloseReconnectPopup();
        }

        return result;
    }

    private static bool HasRequiredConnections(
        bool requireMultiplayer,
        LobbySetupData lobbySetupData,
        bool allowIdleMultiplayer)
    {
        if (OnlineConnectionManager.instance?.IsOnline != true)
        {
            return false;
        }

        if (!requireMultiplayer)
        {
            return true;
        }

        NetworkBootstrap bootstrap = NetworkBootstrap.instance;
        if (bootstrap?.IsReady != true || !bootstrap.IsConnectionAvailableForTesting)
        {
            return false;
        }

        if (allowIdleMultiplayer)
        {
            return true;
        }

        return bootstrap.IsConnected &&
               (lobbySetupData == null || NetworkLobbyConnection.GetLocalConnection() != null);
    }

    private async Task<bool> EnsureMultiplayerAsync(LobbySetupData lobbySetupData)
    {
        NetworkBootstrap bootstrap = NetworkBootstrap.instance;

        if (bootstrap == null || !bootstrap.IsReady || !bootstrap.IsConnectionAvailableForTesting)
        {
            return false;
        }

        if (bootstrap.IsConnected &&
            (lobbySetupData == null || NetworkLobbyConnection.GetLocalConnection() != null))
        {
            return true;
        }

        if (lobbySetupData != null)
        {
            if (NetworkLobbyService.instance == null ||
                !await NetworkLobbyService.instance.PrepareConnectionForEntryAsync(lobbySetupData))
            {
                return false;
            }

            float lobbyConnectionDeadline = Time.realtimeSinceStartup + MultiplayerConnectTimeoutSeconds;
            while (Time.realtimeSinceStartup < lobbyConnectionDeadline && !abortForFinalCountdown)
            {
                if (!bootstrap.IsConnectionAvailableForTesting ||
                    OnlineConnectionManager.instance?.IsOnline != true)
                {
                    return false;
                }

                if (bootstrap.IsConnected && NetworkLobbyConnection.GetLocalConnection() != null)
                {
                    return true;
                }

                await Task.Yield();
            }

            return false;
        }

        if (lastClientMode != NetworkConnectionMode.DirectClient &&
            lastClientMode != NetworkConnectionMode.RelayClient)
        {
            return false;
        }

        if (!await bootstrap.ShutdownAsync())
        {
            return false;
        }

        string userId = UserManager.instance?.UserId;
        bool started = lastClientMode == NetworkConnectionMode.DirectClient
            ? bootstrap.StartDirectClient(userId, MultiplayerPlayModeTestContext.DirectAddress)
            : await bootstrap.StartRelayClientAsync(userId, lastRelayJoinCode);

        if (!started)
        {
            return false;
        }

        float deadline = Time.realtimeSinceStartup + MultiplayerConnectTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline && !abortForFinalCountdown)
        {
            if (bootstrap.IsConnected)
            {
                return true;
            }

            if (bootstrap.ConnectionState == NetworkConnectionState.Failed ||
                bootstrap.ConnectionState == NetworkConnectionState.Disconnected)
            {
                return false;
            }

            await Task.Yield();
        }

        return false;
    }
}
