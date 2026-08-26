using System;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkSessionPlayer))]
public class NetworkPlayerProfileConnection : NetworkBehaviour
{
    public static NetworkPlayerProfileConnection local;

    public static event Action<ulong, PlayerProfileData> AuthorityProfileUpdateRequested;
    public static event Action<PlayerProfileData> LocalProfileUpdateReceived;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        local = null;
        AuthorityProfileUpdateRequested = null;
        LocalProfileUpdateReceived = null;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            local = this;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (local == this)
        {
            local = null;
        }

        base.OnNetworkDespawn();
    }

    public static NetworkPlayerProfileConnection GetLocalConnection()
    {
        if (local != null && local.IsSpawned)
        {
            return local;
        }

        NetworkSessionPlayer sessionPlayer = NetworkSessionPlayer.GetLocalPlayer();

        if (sessionPlayer == null)
        {
            return null;
        }

        NetworkPlayerProfileConnection connection = sessionPlayer.GetComponent<NetworkPlayerProfileConnection>();

        if (connection == null || !connection.IsSpawned)
        {
            return null;
        }

        local = connection;
        return local;
    }

    public bool RequestProfileUpdate(PlayerProfileData profile)
    {
        if (!IsSpawned || !IsOwner || profile == null || !profile.IsValid)
        {
            return false;
        }

        RequestProfileUpdateRpc(JsonUtility.ToJson(profile));
        return true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestProfileUpdateRpc(string profileJson, RpcParams rpcParams = default)
    {
        PlayerProfileData profile = DeserializeProfile(profileJson);

        if (profile == null || !profile.IsValid)
        {
            return;
        }

        AuthorityProfileUpdateRequested?.Invoke(rpcParams.Receive.SenderClientId, profile);
    }

    public static bool TrySendProfileUpdate(ulong clientId, PlayerProfileData profile)
    {
        if (profile == null || !profile.IsValid || !TryGetServerConnection(clientId, out NetworkPlayerProfileConnection connection))
        {
            return false;
        }

        connection.ReceiveProfileUpdateRpc(JsonUtility.ToJson(profile), connection.RpcTarget.Single(clientId, RpcTargetUse.Temp));
        return true;
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveProfileUpdateRpc(string profileJson, RpcParams rpcParams = default)
    {
        PlayerProfileData profile = DeserializeProfile(profileJson);

        if (profile != null && profile.IsValid)
        {
            LocalProfileUpdateReceived?.Invoke(profile);
        }
    }

    private static bool TryGetServerConnection(ulong clientId, out NetworkPlayerProfileConnection connection)
    {
        connection = null;

        if (!NetworkSessionPlayer.TryGetServerPlayer(clientId, out NetworkSessionPlayer sessionPlayer))
        {
            return false;
        }

        connection = sessionPlayer.GetComponent<NetworkPlayerProfileConnection>();
        return connection != null && connection.IsSpawned && connection.IsServer;
    }

    private static PlayerProfileData DeserializeProfile(string profileJson)
    {
        if (string.IsNullOrWhiteSpace(profileJson))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<PlayerProfileData>(profileJson);
        }
        catch
        {
            return null;
        }
    }
}
