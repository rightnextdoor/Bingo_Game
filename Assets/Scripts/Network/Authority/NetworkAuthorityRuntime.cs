using System;
using UnityEngine;

[DefaultExecutionOrder(-1150)]
[DisallowMultipleComponent]
public class NetworkAuthorityRuntime : MonoBehaviour
{
    public static NetworkAuthorityRuntime instance;

    private bool isReady;
    private bool isAuthorityActive;

    private NetworkRoot networkRoot;
    private NetworkBootstrap networkBootstrap;

    public bool IsReady => isReady;
    public bool IsAuthorityActive => isAuthorityActive;

    public event Action<bool> AuthorityActiveChanged;


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
        UnregisterBootstrapEvents();

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
                "NetworkAuthorityRuntime could not initialize because NetworkRoot.instance is null.");

            return false;
        }

        networkBootstrap =
            NetworkBootstrap.instance;

        if (networkBootstrap == null ||
            !networkBootstrap.IsReady)
        {
            Debug.LogError(
                "NetworkAuthorityRuntime could not initialize because NetworkBootstrap is not ready.");

            return false;
        }

        RegisterBootstrapEvents();

        isReady = true;

        RefreshAuthorityState();

        return true;
    }

    private void RegisterBootstrapEvents()
    {
        if (networkBootstrap == null)
        {
            return;
        }

        networkBootstrap.ConnectionStateChanged -=
            OnConnectionStateChanged;

        networkBootstrap.ConnectionStateChanged +=
            OnConnectionStateChanged;
    }

    private void UnregisterBootstrapEvents()
    {
        if (networkBootstrap == null)
        {
            return;
        }

        networkBootstrap.ConnectionStateChanged -=
            OnConnectionStateChanged;
    }

    private void OnConnectionStateChanged(
        NetworkConnectionState connectionState)
    {
        RefreshAuthorityState();
    }

    private void RefreshAuthorityState()
    {
        bool shouldBeActive =
            isReady &&
            networkBootstrap != null &&
            networkBootstrap.IsAuthority &&
            networkBootstrap.ConnectionState ==
                NetworkConnectionState.Connected;

        SetAuthorityActive(
            shouldBeActive);
    }

    private void SetAuthorityActive(bool active)
    {
        if (isAuthorityActive == active)
        {
            return;
        }

        isAuthorityActive = active;

        AuthorityActiveChanged?.Invoke(
            isAuthorityActive);
    }
}