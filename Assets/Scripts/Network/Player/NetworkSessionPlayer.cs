using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class NetworkSessionPlayer : NetworkBehaviour
{
    #region Fields

    private static readonly Dictionary<ulong, NetworkSessionPlayer> serverPlayersByClientId = new Dictionary<ulong, NetworkSessionPlayer>();
    private static NetworkSessionPlayer localPlayer;

    public static NetworkSessionPlayer LocalPlayer => GetLocalPlayer();
    public ulong ClientId => OwnerClientId;

    #endregion

    #region Unity Methods

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        localPlayer = null;
        serverPlayersByClientId.Clear();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        PreserveAcrossScenes();

        if (IsServer)
        {
            serverPlayersByClientId[OwnerClientId] = this;
        }

        if (IsOwner)
        {
            localPlayer = this;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer &&
            serverPlayersByClientId.TryGetValue(OwnerClientId, out NetworkSessionPlayer registeredPlayer) &&
            registeredPlayer == this)
        {
            serverPlayersByClientId.Remove(OwnerClientId);
        }

        if (localPlayer == this)
        {
            localPlayer = null;
        }

        base.OnNetworkDespawn();
    }

    #endregion

    #region Lookup

    public static NetworkSessionPlayer GetLocalPlayer()
    {
        if (localPlayer != null && localPlayer.IsSpawned)
        {
            return localPlayer;
        }

        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsListening || !networkManager.IsClient)
        {
            return null;
        }

        NetworkObject playerObject = networkManager.LocalClient?.PlayerObject;

        if (playerObject == null)
        {
            return null;
        }

        NetworkSessionPlayer sessionPlayer = playerObject.GetComponent<NetworkSessionPlayer>();

        if (sessionPlayer == null || !sessionPlayer.IsSpawned)
        {
            return null;
        }

        localPlayer = sessionPlayer;
        return localPlayer;
    }

    public static bool TryGetServerPlayer(ulong clientId, out NetworkSessionPlayer sessionPlayer)
    {
        if (!serverPlayersByClientId.TryGetValue(clientId, out sessionPlayer))
        {
            return false;
        }

        if (sessionPlayer != null && sessionPlayer.IsSpawned && sessionPlayer.IsServer)
        {
            return true;
        }

        serverPlayersByClientId.Remove(clientId);
        sessionPlayer = null;
        return false;
    }

    #endregion

    #region Lifetime

    private void PreserveAcrossScenes()
    {
        if (transform.parent != null)
        {
            Debug.LogWarning($"NetworkSessionPlayer for ClientId {OwnerClientId} must be a root GameObject to persist across scenes.");
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    #endregion
}
