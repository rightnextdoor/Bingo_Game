using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkNotificationConnection : NetworkBehaviour
{
    private static readonly Dictionary<ulong, NetworkNotificationConnection> serverConnectionsByClientId = new Dictionary<ulong, NetworkNotificationConnection>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        serverConnectionsByClientId.Clear();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            serverConnectionsByClientId[OwnerClientId] = this;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && serverConnectionsByClientId.TryGetValue(OwnerClientId, out NetworkNotificationConnection connection) && connection == this)
        {
            serverConnectionsByClientId.Remove(OwnerClientId);
        }

        base.OnNetworkDespawn();
    }

    public static bool TrySendNotification(ulong clientId, UIMessageType messageType, string messageOverride = null)
    {
        if (messageType == UIMessageType.None)
        {
            return false;
        }

        if (!serverConnectionsByClientId.TryGetValue(clientId, out NetworkNotificationConnection connection))
        {
            return false;
        }

        if (connection == null || !connection.IsSpawned || !connection.IsServer)
        {
            return false;
        }

        connection.ReceiveNotificationRpc(messageType, messageOverride ?? string.Empty, connection.RpcTarget.Single(clientId, RpcTargetUse.Temp));
        return true;
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveNotificationRpc(UIMessageType messageType, string messageOverride, RpcParams rpcParams = default)
    {
        NotificationService.instance?.ReceiveNetworkNotification(messageType, messageOverride);
    }
}