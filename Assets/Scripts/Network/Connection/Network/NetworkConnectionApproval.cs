using Unity.Netcode;
using UnityEngine;

[DefaultExecutionOrder(-1170)]
[DisallowMultipleComponent]
public class NetworkConnectionApproval : MonoBehaviour
{
    #region Fields

    public static NetworkConnectionApproval instance;

    private bool isReady;
    private NetworkRoot networkRoot;
    private NetworkManager networkManager;
    private NetworkConnectionRegistry connectionRegistry;
    private NetworkRuntimeConfigData runtimeConfig;

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
    }

    private void OnDestroy()
    {
        if (networkManager != null && networkManager.ConnectionApprovalCallback == ApprovalCheck)
        {
            networkManager.ConnectionApprovalCallback = null;
        }

        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Initialization

    public bool Initialize()
    {
        if (isReady)
        {
            return true;
        }

        networkRoot = NetworkRoot.instance;

        if (networkRoot == null)
        {
            Debug.LogError("NetworkConnectionApproval could not initialize because NetworkRoot.instance is null.");
            return false;
        }

        runtimeConfig = networkRoot.RuntimeConfig;

        if (runtimeConfig == null)
        {
            Debug.LogError("NetworkConnectionApproval could not initialize because NetworkRuntimeConfigData is missing.");
            return false;
        }

        networkManager = networkRoot.GetComponent<NetworkManager>();
        connectionRegistry = networkRoot.GetComponent<NetworkConnectionRegistry>();

        if (networkManager == null)
        {
            Debug.LogError("NetworkConnectionApproval could not initialize because NetworkManager is missing.");
            return false;
        }

        if (connectionRegistry == null || !connectionRegistry.IsReady)
        {
            Debug.LogError("NetworkConnectionApproval could not initialize because NetworkConnectionRegistry is not ready.");
            return false;
        }

        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = ApprovalCheck;

        isReady = true;
        return true;
    }

    #endregion

    #region Approval

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = false;
        response.CreatePlayerObject = false;
        response.Pending = false;
        response.Reason = string.Empty;

        if (!isReady)
        {
            Reject(response, "Network connection approval is not ready.");
            return;
        }

        byte[] payloadBytes = request.Payload;

        if (payloadBytes == null || payloadBytes.Length == 0)
        {
            Reject(response, "Connection payload is missing.");
            return;
        }

        if (payloadBytes.Length > runtimeConfig.MaximumApprovalPayloadBytes)
        {
            Reject(response, "Connection payload is too large.");
            return;
        }

        if (!NetworkConnectionPayload.TryFromBytes(payloadBytes, out NetworkConnectionPayload payload))
        {
            Reject(response, "Connection payload is invalid.");
            return;
        }

        if (payload.ProtocolVersion != runtimeConfig.ProtocolVersion)
        {
            Reject(response, "Network protocol version does not match.");
            return;
        }

        if (string.IsNullOrWhiteSpace(payload.BingoUserId))
        {
            Reject(response, "Bingo UserId is missing.");
            return;
        }

        string bingoUserId = payload.BingoUserId.Trim();

        if (bingoUserId.Length > runtimeConfig.MaximumBingoUserIdLength)
        {
            Reject(response, "Bingo UserId is too long.");
            return;
        }

        if (connectionRegistry.ConnectionCount >= runtimeConfig.MaximumConnections && !connectionRegistry.TryGetBingoUserId(request.ClientNetworkId, out _))
        {
            Reject(response, "Maximum network connection count reached.");
            return;
        }

        if (!connectionRegistry.TryRegisterApprovedConnection(request.ClientNetworkId, bingoUserId, out string reason))
        {
            Reject(response, reason);
            return;
        }

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Pending = false;
        response.Reason = string.Empty;
    }

    private void Reject(NetworkManager.ConnectionApprovalResponse response, string reason)
    {
        response.Approved = false;
        response.CreatePlayerObject = false;
        response.Pending = false;
        response.Reason = reason;
    }

    #endregion
}
