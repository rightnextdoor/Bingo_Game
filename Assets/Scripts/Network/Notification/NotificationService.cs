using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NotificationService : MonoBehaviour
{
    public static NotificationService instance;

    #region Unity Lifecycle

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
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Local Notifications

    public void SendLocal(UIMessageType messageType, string messageOverride = null)
    {
        if (NotificationManager.instance == null)
        {
            Debug.LogWarning($"NotificationService could not display {messageType} because NotificationManager was not found.");
            return;
        }

        NotificationManager.instance.SendNotification(messageType, messageOverride);
    }

    public void ReceiveNetworkNotification(UIMessageType messageType, string messageOverride)
    {
        SendLocal(messageType, string.IsNullOrWhiteSpace(messageOverride) ? null : messageOverride);
    }

    #endregion

    #region Network Notifications

    public bool SendToUser(string userId, UIMessageType messageType, string messageOverride = null)
    {
        if (string.IsNullOrWhiteSpace(userId) || messageType == UIMessageType.None)
        {
            return false;
        }

        NetworkBootstrap networkBootstrap = NetworkBootstrap.instance;
        NetworkConnectionRegistry connectionRegistry = NetworkConnectionRegistry.instance;

        if (networkBootstrap == null || !networkBootstrap.IsAuthority || connectionRegistry == null || !connectionRegistry.IsReady)
        {
            return false;
        }

        if (!connectionRegistry.TryGetClientId(userId, out ulong clientId))
        {
            return false;
        }

        if (networkBootstrap.IsHost && clientId == networkBootstrap.LocalClientId)
        {
            SendLocal(messageType, messageOverride);
            return true;
        }

        return NetworkNotificationConnection.TrySendNotification(clientId, messageType, messageOverride);
    }

    public int SendToUsers(IReadOnlyList<string> userIds, UIMessageType messageType, string messageOverride = null)
    {
        if (userIds == null || messageType == UIMessageType.None)
        {
            return 0;
        }

        int sentCount = 0;

        for (int i = 0; i < userIds.Count; i++)
        {
            if (SendToUser(userIds[i], messageType, messageOverride))
            {
                sentCount++;
            }
        }

        return sentCount;
    }

    #endregion
}