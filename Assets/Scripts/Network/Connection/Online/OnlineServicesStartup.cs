using UnityEngine;

[DefaultExecutionOrder(-1080)]
[DisallowMultipleComponent]
public class OnlineServicesStartup : MonoBehaviour
{
    #region Fields

    private OnlineServicesRoot onlineServicesRoot;
    private OnlineConnectionManager connectionManager;

    private bool isReady;
    private bool hasAutomaticAttemptStarted;

    public bool IsReady => isReady;
    public bool HasAutomaticAttemptStarted => hasAutomaticAttemptStarted;

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

        isReady = true;
        StartAutomaticConnectionAttempt();
        return true;
    }

    private async void StartAutomaticConnectionAttempt()
    {
        if (hasAutomaticAttemptStarted || connectionManager == null)
        {
            return;
        }

        hasAutomaticAttemptStarted = true;
        await connectionManager.EnsureConnectedAsync();
    }

    #endregion
}
