using UnityEngine;

[DefaultExecutionOrder(-1070)]
[DisallowMultipleComponent]
public class OnlineServicesLifecycle : MonoBehaviour
{
    #region Fields

    private OnlineServicesRoot onlineServicesRoot;
    private OnlineConnectionManager connectionManager;

    private bool isReady;
    private bool isSubscribedToConnectionManager;

    public bool IsReady => isReady;

    #endregion

    #region Unity Methods

    private void OnDestroy()
    {
        UnsubscribeFromConnectionManager();
    }

    #endregion

    #region Initialization

    public bool Initialize()
    {
        if (isReady)
        {
            return true;
        }

        onlineServicesRoot = OnlineServicesRoot.instance;

        if (onlineServicesRoot == null ||
            onlineServicesRoot != GetComponent<OnlineServicesRoot>() ||
            !onlineServicesRoot.IsPrimaryInstance)
        {
            return false;
        }

        connectionManager = OnlineConnectionManager.instance;

        if (connectionManager == null || !connectionManager.IsReady)
        {
            return false;
        }

        SubscribeToConnectionManager();

        isReady = true;
        return true;
    }

    #endregion

    #region Connection Events

    private void SubscribeToConnectionManager()
    {
        if (isSubscribedToConnectionManager || connectionManager == null)
        {
            return;
        }

        connectionManager.ConnectionStateChanged += OnConnectionStateChanged;
        isSubscribedToConnectionManager = true;
    }

    private void UnsubscribeFromConnectionManager()
    {
        if (isSubscribedToConnectionManager && connectionManager != null)
        {
            connectionManager.ConnectionStateChanged -= OnConnectionStateChanged;
        }

        isSubscribedToConnectionManager = false;
    }

    private void OnConnectionStateChanged(OnlineConnectionState state)
    {
        if (state != OnlineConnectionState.Offline)
        {
            return;
        }

        FailureManager.instance?.OnlineFailure?.ReportFailure(
            OnlineFailureType.ConnectionLost);
    }

    #endregion
}