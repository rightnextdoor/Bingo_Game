using System;
using UnityEngine;

[DefaultExecutionOrder(-1140)]
[DisallowMultipleComponent]
public class MultiplayerSessionLifecycle : MonoBehaviour
{
    #region Fields

    public static MultiplayerSessionLifecycle instance;

    private NetworkRoot networkRoot;
    private NetworkBootstrap networkBootstrap;
    private bool isReady;

    public bool IsReady => isReady;

    public event Action<NetworkConnectionState> ConnectionLost;

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
        UnsubscribeFromBootstrap();

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
        networkBootstrap = NetworkBootstrap.instance;

        if (networkRoot == null ||
            networkRoot != GetComponentInParent<NetworkRoot>() ||
            networkBootstrap == null ||
            !networkBootstrap.IsReady)
        {
            return false;
        }

        SubscribeToBootstrap();
        isReady = true;
        return true;
    }

    #endregion

    #region Bootstrap Events

    private void SubscribeToBootstrap()
    {
        if (networkBootstrap == null)
        {
            return;
        }

        networkBootstrap.ConnectionStateChanged -= OnConnectionStateChanged;
        networkBootstrap.ConnectionStateChanged += OnConnectionStateChanged;
    }

    private void UnsubscribeFromBootstrap()
    {
        if (networkBootstrap != null)
        {
            networkBootstrap.ConnectionStateChanged -= OnConnectionStateChanged;
        }
    }

    private void OnConnectionStateChanged(NetworkConnectionState connectionState)
    {
        if (connectionState == NetworkConnectionState.Offline)
        {
            MultiplayerNetworkScheduler.instance?.ClearAll();
            return;
        }

        if (connectionState != NetworkConnectionState.Disconnected &&
            connectionState != NetworkConnectionState.Failed)
        {
            return;
        }

        MultiplayerNetworkScheduler.instance?.ClearAll();
        ConnectionLost?.Invoke(connectionState);
    }

    #endregion
}
