using Unity.Netcode;
using UnityEngine;

public enum MultiplayerSimulationPlayer
{
    Player2 = 2,
    Player3 = 3,
    Player4 = 4
}

[DisallowMultipleComponent]
public class MultiplayerConnectionSimulation : MonoBehaviour
{
    #region Fields

    [Header("Multiplayer Connection Loss")]
    [SerializeField] private MultiplayerSimulationPlayer targetPlayer = MultiplayerSimulationPlayer.Player2;
    [SerializeField] private bool simulateConnectionLoss;

    private NetworkManager networkManager;
    private NetworkConnectionRegistry connectionRegistry;

    private bool isInitialized;
    private bool isWaitingForDisconnect;
    private ulong pendingDisconnectClientId = ulong.MaxValue;

    #endregion

    #region Unity Methods

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        TryInitialize();

        if (!isInitialized)
        {
            return;
        }

        ProcessConnectionLossSimulation();
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnregisterCallbacks();

        networkManager = null;
        connectionRegistry = null;

        isInitialized = false;
        isWaitingForDisconnect = false;
        pendingDisconnectClientId = ulong.MaxValue;

        simulateConnectionLoss = false;
#endif
    }

    #endregion

    #region Initialization

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void TryInitialize()
    {
        if (isInitialized || !MultiplayerPlayModeTestContext.IsActive)
        {
            return;
        }

        networkManager = NetworkManager.Singleton;
        connectionRegistry = NetworkConnectionRegistry.instance;

        if (networkManager == null || connectionRegistry == null || !connectionRegistry.IsReady)
        {
            return;
        }

        RegisterCallbacks();
        isInitialized = true;
    }

    private void RegisterCallbacks()
    {
        if (networkManager == null)
        {
            return;
        }

        networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void UnregisterCallbacks()
    {
        if (networkManager != null)
        {
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

#endif

    #endregion

    #region Connection Loss

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void ProcessConnectionLossSimulation()
    {
        if (!simulateConnectionLoss || isWaitingForDisconnect)
        {
            return;
        }

        if (!IsAuthorityProcess())
        {
            return;
        }

        if (!TryGetTargetClientId(out ulong targetClientId))
        {
            simulateConnectionLoss = false;
            return;
        }

        if (targetClientId == NetworkManager.ServerClientId)
        {
            simulateConnectionLoss = false;
            return;
        }

        pendingDisconnectClientId = targetClientId;
        isWaitingForDisconnect = true;

        networkManager.DisconnectClient(targetClientId);
    }

    private bool TryGetTargetClientId(out ulong clientId)
    {
        clientId = ulong.MaxValue;

        int targetPlayerNumber = (int)targetPlayer;

        string targetUserId = MultiplayerPlayModeTestContext.GetUserId(targetPlayerNumber);

        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            return false;
        }

        return connectionRegistry.TryGetClientId(targetUserId, out clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!isWaitingForDisconnect || clientId != pendingDisconnectClientId)
        {
            return;
        }

        pendingDisconnectClientId = ulong.MaxValue;
        isWaitingForDisconnect = false;
        simulateConnectionLoss = false;
    }

#endif

    #endregion

    #region Helpers

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private bool IsAuthorityProcess()
    {
        return MultiplayerPlayModeTestContext.IsActive &&
               MultiplayerPlayModeTestContext.IsHost &&
               networkManager != null &&
               networkManager.IsListening &&
               networkManager.IsServer;
    }

#endif

    #endregion
}