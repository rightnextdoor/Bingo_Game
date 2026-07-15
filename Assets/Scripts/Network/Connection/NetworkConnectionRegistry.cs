using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DefaultExecutionOrder(-1180)]
[DisallowMultipleComponent]
public class NetworkConnectionRegistry : MonoBehaviour
{
    public static NetworkConnectionRegistry instance;

    private readonly Dictionary<ulong, string>
        clientIdToBingoUserId =
            new Dictionary<ulong, string>();

    private readonly Dictionary<string, ulong>
        bingoUserIdToClientId =
            new Dictionary<string, ulong>(
                StringComparer.Ordinal);

    private bool isReady;

    private NetworkRoot networkRoot;
    private NetworkManager networkManager;

    public bool IsReady => isReady;
    public int ConnectionCount => clientIdToBingoUserId.Count;

    public event Action<ulong, string> ConnectionRegistered;
    public event Action<ulong, string> ConnectionRemoved;


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
        UnregisterNetworkCallbacks();

        if (instance == this)
        {
            instance = null;
        }
    }

    public bool Initialize()
    {
        if (isReady)
        {
            return true;
        }

        networkRoot = NetworkRoot.instance;

        if (networkRoot == null)
        {
            Debug.LogError(
                "NetworkConnectionRegistry could not initialize because NetworkRoot.instance is null.");

            return false;
        }

        networkManager = networkRoot.GetComponent<NetworkManager>();

        if (networkManager == null)
        {
            Debug.LogError(
                "NetworkConnectionRegistry could not initialize because NetworkManager is missing.");

            return false;
        }

        RegisterNetworkCallbacks();

        isReady = true;

        return true;
    }

    public bool TryRegisterApprovedConnection(
        ulong clientId,
        string bingoUserId,
        out string reason)
    {
        reason = string.Empty;

        if (!isReady)
        {
            reason = "Network connection registry is not ready.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(bingoUserId))
        {
            reason = "Bingo UserId is missing.";
            return false;
        }

        string normalizedBingoUserId = bingoUserId.Trim();

        if (clientIdToBingoUserId.TryGetValue(
                clientId,
                out string existingBingoUserId))
        {
            if (existingBingoUserId == normalizedBingoUserId)
            {
                return true;
            }

            reason =
                "This network client is already registered to another Bingo UserId.";

            return false;
        }

        if (bingoUserIdToClientId.TryGetValue(
                normalizedBingoUserId,
                out ulong existingClientId))
        {
            if (existingClientId == clientId)
            {
                return true;
            }

            reason =
                "This Bingo user is already connected.";

            return false;
        }

        clientIdToBingoUserId.Add(
            clientId,
            normalizedBingoUserId);

        bingoUserIdToClientId.Add(
            normalizedBingoUserId,
            clientId);

        ConnectionRegistered?.Invoke(
            clientId,
            normalizedBingoUserId);

        return true;
    }

    public bool TryGetBingoUserId(
        ulong clientId,
        out string bingoUserId)
    {
        return clientIdToBingoUserId.TryGetValue(
            clientId,
            out bingoUserId);
    }

    public bool TryGetClientId(
        string bingoUserId,
        out ulong clientId)
    {
        clientId = default;

        if (string.IsNullOrWhiteSpace(bingoUserId))
        {
            return false;
        }

        return bingoUserIdToClientId.TryGetValue(
            bingoUserId.Trim(),
            out clientId);
    }

    public bool IsBingoUserConnected(string bingoUserId)
    {
        if (string.IsNullOrWhiteSpace(bingoUserId))
        {
            return false;
        }

        return bingoUserIdToClientId.ContainsKey(
            bingoUserId.Trim());
    }

    public void RemoveConnection(ulong clientId)
    {
        if (!clientIdToBingoUserId.TryGetValue(
                clientId,
                out string bingoUserId))
        {
            return;
        }

        clientIdToBingoUserId.Remove(clientId);
        bingoUserIdToClientId.Remove(bingoUserId);

        ConnectionRemoved?.Invoke(
            clientId,
            bingoUserId);
    }

    public void ClearConnections()
    {
        clientIdToBingoUserId.Clear();
        bingoUserIdToClientId.Clear();
    }

    private void RegisterNetworkCallbacks()
    {
        if (networkManager == null)
        {
            return;
        }

        networkManager.OnClientDisconnectCallback -=
            OnClientDisconnected;

        networkManager.OnClientDisconnectCallback +=
            OnClientDisconnected;
    }

    private void UnregisterNetworkCallbacks()
    {
        if (networkManager == null)
        {
            return;
        }

        networkManager.OnClientDisconnectCallback -=
            OnClientDisconnected;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        RemoveConnection(clientId);
    }
}