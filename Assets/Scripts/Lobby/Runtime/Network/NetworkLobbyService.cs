using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkLobbyService : MonoBehaviour, ILobbyService
{
    #region Fields

    public static NetworkLobbyService instance;

    private const float ConnectionTimeoutSeconds = 30f;
    private const float LobbyConnectionTimeoutSeconds = 15f;
    private const float ExitNotificationDeliverySeconds = 0.15f;

    private bool isReady;
    private NetworkBootstrap networkBootstrap;

    public SessionRuntimeType RuntimeType => SessionRuntimeType.Network;
    public bool IsReady => isReady;

    #endregion

    #region Unity Methods

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
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

        networkBootstrap = NetworkBootstrap.instance;
        isReady = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Lobby Entry

    public async Task<LobbyEntryResult> EnterLobbyAsync(LobbySetupData lobbySetupData)
    {
        if (!isReady)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.ServiceUnavailable, "The network lobby service is not ready.");
        }

        if (!IsValidNetworkSetup(lobbySetupData))
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.InvalidSetupData, "The network lobby setup data is invalid.");
        }

        string relayJoinCode = string.Empty;

        if (!HasUsableNetworkConnection() && !TryPrepareCustomLobbySearch(lobbySetupData, out relayJoinCode, out LobbyEntryResult customSearchFailure))
        {
            return customSearchFailure;
        }

        if (!await EnsureNetworkConnectionAsync(lobbySetupData, relayJoinCode))
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.NetworkConnectionFailed, "The network connection could not be created.");
        }

        NetworkLobbyConnection lobbyConnection = await WaitForLocalLobbyConnectionAsync();

        if (lobbyConnection == null)
        {
            return LobbyEntryResult.Failed(LobbyEntryFailureType.NetworkLobbyConnectionUnavailable, "The network lobby connection was not available.");
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

    private bool TryPrepareCustomLobbySearch(LobbySetupData lobbySetupData, out string relayJoinCode, out LobbyEntryResult failureResult)
    {
        relayJoinCode = string.Empty;
        failureResult = null;

        if (!IsCustomLobbySearch(lobbySetupData) || MultiplayerPlayModeTestContext.IsActive)
        {
            return true;
        }

        NetworkLobbyManager lobbyManager = NetworkLobbyManager.instance;

        if (lobbyManager == null || !lobbyManager.IsReady)
        {
            failureResult = LobbyEntryResult.Failed(LobbyEntryFailureType.ServiceUnavailable, "The authoritative network lobby manager is not ready.");
            return false;
        }

        return lobbyManager.TryPrepareCustomLobbySearch(lobbySetupData, out relayJoinCode, out failureResult);
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
        catch (Exception exception)
        {
            Debug.LogWarning($"[NetworkLobbyService] Failed lobby entry rollback could not complete: {exception.Message}");
        }
    }

    #endregion

    #region Lobby Exit

    public async Task<LobbyExitResult> LeaveLobbyAsync(string userId)
    {
        if (!isReady)
        {
            return LobbyExitResult.Failed(userId, LobbyPlayerExitReason.VoluntaryLeave, "The network lobby service is not ready.");
        }

        NetworkLobbyConnection lobbyConnection = NetworkLobbyConnection.GetLocalConnection();

        if (lobbyConnection == null)
        {
            return LobbyExitResult.Failed(userId, LobbyPlayerExitReason.VoluntaryLeave, "The network lobby connection was not available.");
        }

        LobbyExitResult result = await lobbyConnection.RequestLeaveLobbyAsync();

        if (result != null && result.success)
        {
            await ShutdownLocalNetworkAfterExitIfPossible();
        }

        return result ?? LobbyExitResult.Failed(userId, LobbyPlayerExitReason.VoluntaryLeave, "The network lobby did not return a leave result.");
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

    private async Task ShutdownLocalNetworkAfterExitIfPossible()
    {
        if (MultiplayerPlayModeTestContext.IsActive || networkBootstrap == null || !networkBootstrap.IsConnected)
        {
            return;
        }

        if (networkBootstrap.IsAuthority && NetworkLobbyManager.instance != null && NetworkLobbyManager.instance.HasActiveLobbies)
        {
            return;
        }

        float deliveryTime = Time.realtimeSinceStartup + ExitNotificationDeliverySeconds;

        while (Time.realtimeSinceStartup < deliveryTime)
        {
            await Task.Yield();
        }

        await networkBootstrap.ShutdownAsync();
    }

    #endregion

    #region Lobby Commands

    public void SetPlayerReady(bool isReady)
    {
        NetworkLobbyConnection.GetLocalConnection()?.RequestSetPlayerReady(isReady);
    }

    public void RerollBoard()
    {
        NetworkLobbyConnection.GetLocalConnection()?.RequestRerollBoard();
    }

    public void StartLobby()
    {
        NetworkLobbyConnection.GetLocalConnection()?.RequestStartLobby();
    }

    public void NotifyLobbySceneReady()
    {
        NetworkLobbyConnection.GetLocalConnection()?.NotifyLobbySceneReady();
    }

    public async Task<bool> ApplyHostSettingsAsync(LobbyHostSettingsData settingsData)
    {
        NetworkLobbyConnection lobbyConnection = NetworkLobbyConnection.GetLocalConnection();
        return lobbyConnection != null && await lobbyConnection.RequestApplyHostSettingsAsync(settingsData);
    }

    #endregion

    #region Network Connection

    private bool HasUsableNetworkConnection()
    {
        return networkBootstrap != null && networkBootstrap.IsConnected && NetworkLobbyConnection.GetLocalConnection() != null;
    }

    private async Task<bool> EnsureNetworkConnectionAsync(LobbySetupData lobbySetupData, string customRelayJoinCode)
    {
        if (networkBootstrap == null || !networkBootstrap.IsReady)
        {
            return false;
        }

        if (MultiplayerPlayModeTestContext.IsActive)
        {
            float timeoutTime = Time.realtimeSinceStartup + ConnectionTimeoutSeconds;

            while (!HasUsableNetworkConnection())
            {
                if (Time.realtimeSinceStartup >= timeoutTime)
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
            if (NetworkLobbyConnection.GetLocalConnection() != null)
            {
                return true;
            }

            if (networkBootstrap.IsAuthority && NetworkLobbyManager.instance != null && NetworkLobbyManager.instance.HasActiveLobbies)
            {
                Debug.LogWarning("[NetworkLobbyService] The network is connected, but the local lobby connection is missing while authority lobbies are still active.");
                return false;
            }

            if (!await networkBootstrap.ShutdownAsync())
            {
                return false;
            }
        }

        string userId = lobbySetupData.userData.userId;
        bool started;

        switch (lobbySetupData.playMode)
        {
            case MainMenuPlayMode.Online:
                started = await networkBootstrap.StartRelayHostAsync(userId);
                break;

            case MainMenuPlayMode.Custom:
                CustomLobbySetupData customSetupData = lobbySetupData.customSetupData;

                if (customSetupData == null)
                {
                    return false;
                }

                if (customSetupData.actionType == CustomLobbyActionType.HostLobby)
                {
                    started = await networkBootstrap.StartRelayHostAsync(userId);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(customRelayJoinCode))
                    {
                        return false;
                    }

                    started = await networkBootstrap.StartRelayClientAsync(userId, customRelayJoinCode);
                }

                break;

            default:
                return false;
        }

        if (!started)
        {
            return false;
        }

        float connectionTimeoutTime = Time.realtimeSinceStartup + ConnectionTimeoutSeconds;

        while (!networkBootstrap.IsConnected)
        {
            if (networkBootstrap.ConnectionState == NetworkConnectionState.Failed || networkBootstrap.ConnectionState == NetworkConnectionState.Disconnected)
            {
                return false;
            }

            if (Time.realtimeSinceStartup >= connectionTimeoutTime)
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

        while (Time.realtimeSinceStartup < timeoutTime)
        {
            NetworkLobbyConnection lobbyConnection = NetworkLobbyConnection.GetLocalConnection();

            if (lobbyConnection != null)
            {
                return lobbyConnection;
            }

            await Task.Yield();
        }

        return null;
    }

    #endregion

    #region Validation

    private bool CanInitialize()
    {
        return NetworkRoot.instance != null &&
               NetworkRoot.instance.IsReady &&
               NetworkBootstrap.instance != null &&
               NetworkBootstrap.instance.IsReady &&
               NetworkLobbyManager.instance != null &&
               NetworkLobbyManager.instance.IsReady;
    }

    private bool IsValidNetworkSetup(LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null || lobbySetupData.userData == null || !lobbySetupData.userData.HasUser)
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

    private bool IsCustomLobbySearch(LobbySetupData lobbySetupData)
    {
        return lobbySetupData != null &&
               lobbySetupData.playMode == MainMenuPlayMode.Custom &&
               lobbySetupData.customSetupData != null &&
               lobbySetupData.customSetupData.actionType == CustomLobbyActionType.SearchLobby;
    }

    #endregion
}
