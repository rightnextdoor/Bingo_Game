using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

[DefaultExecutionOrder(-1090)]
[DisallowMultipleComponent]
public class OnlineConnectionManager : MonoBehaviour
{
    #region Fields

    public static OnlineConnectionManager instance;

    private OnlineServicesRoot onlineServicesRoot;

    private bool isReady;
    private bool authenticationEventsRegistered;

    private OnlineConnectionState connectionState = OnlineConnectionState.NotStarted;
    private Task<bool> connectionTask;
    private string lastConnectionError = string.Empty;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool connectionAvailableForTesting = true;
#endif

    public bool IsReady => isReady;
    public bool IsOnline => connectionState == OnlineConnectionState.Online && IsConnectionAvailableForTesting;
    public bool IsConnecting => connectionState == OnlineConnectionState.Connecting;
    public OnlineConnectionState ConnectionState => connectionState;
    public string LastConnectionError => lastConnectionError;
    public bool IsConnectionAvailableForTesting
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return connectionAvailableForTesting;
#else
            return true;
#endif
        }
    }

    public event Action<OnlineConnectionState> ConnectionStateChanged;

    #endregion

    #region Unity Methods

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        onlineServicesRoot = GetComponent<OnlineServicesRoot>();

        if (onlineServicesRoot == null || !onlineServicesRoot.IsPrimaryInstance)
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
        UnregisterAuthenticationEvents();

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

        onlineServicesRoot = OnlineServicesRoot.instance;

        if (onlineServicesRoot == null ||
            onlineServicesRoot != GetComponent<OnlineServicesRoot>() ||
            !onlineServicesRoot.IsPrimaryInstance)
        {
            return false;
        }

        isReady = true;
        return true;
    }

    #endregion

    #region Connection

    public async Task<bool> EnsureConnectedAsync()
    {
        if (!isReady || !IsConnectionAvailableForTesting)
        {
            return false;
        }

        if (HasAuthorizedSession())
        {
            SetConnectionState(OnlineConnectionState.Online);
            return true;
        }

        if (connectionTask != null && !connectionTask.IsCompleted)
        {
            return await connectionTask;
        }

        Task<bool> activeTask = ConnectAsync();
        connectionTask = activeTask;

        try
        {
            return await activeTask;
        }
        finally
        {
            if (connectionTask == activeTask)
            {
                connectionTask = null;
            }
        }
    }

    private async Task<bool> ConnectAsync()
    {
        lastConnectionError = string.Empty;
        SetConnectionState(OnlineConnectionState.Connecting);

        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            RegisterAuthenticationEvents();

            if (!AuthenticationService.Instance.IsAuthorized || !IsConnectionAvailableForTesting)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            if (!AuthenticationService.Instance.IsAuthorized || !IsConnectionAvailableForTesting)
            {
                lastConnectionError = !IsConnectionAvailableForTesting
                    ? "Online services are unavailable in the connection simulation."
                    : "Unity Authentication did not create an authorized session.";
                SetConnectionState(OnlineConnectionState.Offline);
                return false;
            }

            SetConnectionState(OnlineConnectionState.Online);
            return true;
        }
        catch (Exception exception)
        {
            lastConnectionError = exception.Message;
            SetConnectionState(OnlineConnectionState.Offline);
            return false;
        }
    }

    public void ReportConnectionLost(string reason)
    {
        if (!isReady)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            lastConnectionError = reason;
        }

        SetConnectionState(OnlineConnectionState.Offline);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void SetConnectionAvailableForTesting(bool available)
    {
        if (connectionAvailableForTesting == available)
        {
            return;
        }

        connectionAvailableForTesting = available;

        if (!available)
        {
            ReportConnectionLost("Online services are unavailable in the connection simulation.");
        }
        else if (isReady)
        {
            _ = RestoreSimulatedConnectionAsync();
        }
    }

    private async Task RestoreSimulatedConnectionAsync()
    {
        Task<bool> pendingConnection = connectionTask;

        if (pendingConnection != null && !pendingConnection.IsCompleted)
        {
            try
            {
                await pendingConnection;
            }
            catch (Exception)
            {
                // A fresh attempt below handles the failed in-flight connection.
            }
        }

        if (IsConnectionAvailableForTesting && !IsOnline)
        {
            await EnsureConnectedAsync();
        }
    }
#endif

    #endregion

    #region Authentication

    private bool HasAuthorizedSession()
    {
        return UnityServices.State == ServicesInitializationState.Initialized &&
               AuthenticationService.Instance.IsAuthorized;
    }

    private void RegisterAuthenticationEvents()
    {
        if (authenticationEventsRegistered)
        {
            return;
        }

        AuthenticationService.Instance.SignedOut += OnAuthenticationSignedOut;
        AuthenticationService.Instance.Expired += OnAuthenticationExpired;

        authenticationEventsRegistered = true;
    }

    private void UnregisterAuthenticationEvents()
    {
        if (!authenticationEventsRegistered)
        {
            return;
        }

        AuthenticationService.Instance.SignedOut -= OnAuthenticationSignedOut;
        AuthenticationService.Instance.Expired -= OnAuthenticationExpired;

        authenticationEventsRegistered = false;
    }

    private void OnAuthenticationSignedOut()
    {
        ReportConnectionLost("The online authentication session was signed out.");
    }

    private void OnAuthenticationExpired()
    {
        ReportConnectionLost("The online authentication session expired.");
    }

    #endregion

    #region State

    private void SetConnectionState(OnlineConnectionState state)
    {
        if (connectionState == state)
        {
            return;
        }

        connectionState = state;
        ConnectionStateChanged?.Invoke(connectionState);
    }

    #endregion
}
