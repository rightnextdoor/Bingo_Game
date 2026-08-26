using UnityEngine;

[DisallowMultipleComponent]
public class ChatVivoxConnectionSimulation : MonoBehaviour
{
    #region Fields

    [Header("Vivox Connection Simulation")]
    [SerializeField] private MultiplayerStressTargetPlayer targetPlayer = MultiplayerStressTargetPlayer.Player1;
    [SerializeField] private bool vivoxConnectionEnabled = true;
    [SerializeField, Min(0f)] private float retryFailureDelaySeconds = 2f;

    private NetworkConnectionRegistry connectionRegistry;
    private MultiplayerStressTargetPlayer appliedTargetPlayer = MultiplayerStressTargetPlayer.Player1;
    private bool appliedConnectionEnabled = true;
    private bool isInitialized;

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

        ProcessTargetChange();
        ProcessConnectionStateChange();
#endif
    }

    #endregion

    #region Initialization

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void TryInitialize()
    {
        if (isInitialized)
        {
            return;
        }

        if (NetworkBootstrap.instance == null || !NetworkBootstrap.instance.IsReady || !NetworkBootstrap.instance.IsConnected || !NetworkBootstrap.instance.IsAuthority)
        {
            return;
        }

        connectionRegistry = NetworkConnectionRegistry.instance;

        if (connectionRegistry == null || !connectionRegistry.IsReady)
        {
            return;
        }

        appliedTargetPlayer = targetPlayer;
        appliedConnectionEnabled = true;
        isInitialized = true;
    }

#endif

    #endregion

    #region Connection Simulation

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void ProcessTargetChange()
    {
        if (targetPlayer == appliedTargetPlayer)
        {
            return;
        }

        if (!appliedConnectionEnabled)
        {
            if (!TryApplyConnectionState(appliedTargetPlayer, true))
            {
                return;
            }
        }

        appliedTargetPlayer = targetPlayer;
        appliedConnectionEnabled = true;
    }

    private void ProcessConnectionStateChange()
    {
        if (vivoxConnectionEnabled == appliedConnectionEnabled)
        {
            return;
        }

        if (!TryApplyConnectionState(appliedTargetPlayer, vivoxConnectionEnabled))
        {
            return;
        }

        appliedConnectionEnabled = vivoxConnectionEnabled;

        Debug.Log(vivoxConnectionEnabled
            ? $"[VivoxSimulation] {appliedTargetPlayer} Vivox connection is AVAILABLE again. It will stay disconnected until that player retries Chat through Settings."
            : $"[VivoxSimulation] {appliedTargetPlayer} Vivox connection is FORCED OFF. Chat reconnect attempts will continue to fail until this checkbox is enabled again.");
    }

    private bool TryApplyConnectionState(MultiplayerStressTargetPlayer player, bool connectionEnabled)
    {
        if (!TryGetTargetClientId(player, out ulong targetClientId))
        {
            return false;
        }

        return NetworkLobbyConnection.TrySetStressVivoxConnectionAvailable(targetClientId, connectionEnabled, retryFailureDelaySeconds);
    }

    private bool TryGetTargetClientId(MultiplayerStressTargetPlayer player, out ulong clientId)
    {
        clientId = ulong.MaxValue;

        string targetUserId = MultiplayerPlayModeTestContext.IsActive
            ? MultiplayerPlayModeTestContext.GetUserId((int)player)
            : player == MultiplayerStressTargetPlayer.Player1 && UserManager.instance != null && UserManager.instance.HasUser
                ? UserManager.instance.UserId
                : string.Empty;

        if (string.IsNullOrWhiteSpace(targetUserId) || connectionRegistry == null || !connectionRegistry.IsReady)
        {
            return false;
        }

        return connectionRegistry.TryGetClientId(targetUserId, out clientId);
    }

#endif

    #endregion
}
