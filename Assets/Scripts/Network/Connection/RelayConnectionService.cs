using System;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

[DefaultExecutionOrder(-1190)]
[DisallowMultipleComponent]
public class RelayConnectionService : MonoBehaviour
{
    public static RelayConnectionService instance;

    private bool isReady;

    private NetworkRoot networkRoot;
    private NetworkRuntimeConfigData runtimeConfig;

    private Task servicesInitializationTask;

    public bool IsReady => isReady;


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
                "RelayConnectionService could not initialize because NetworkRoot.instance is null.");

            return false;
        }

        runtimeConfig = networkRoot.RuntimeConfig;

        if (runtimeConfig == null)
        {
            Debug.LogError(
                "RelayConnectionService could not initialize because NetworkRuntimeConfigData is missing.");

            return false;
        }

        isReady = true;

        return true;
    }

    public async Task<RelayHostConnectionData> CreateHostConnectionAsync()
    {
        if (!isReady)
        {
            throw new InvalidOperationException(
                "RelayConnectionService is not ready.");
        }

        await EnsureServicesReadyAsync();

        int maximumConnections =
            runtimeConfig.MaximumRelayJoinConnections;

        Allocation allocation =
            await RelayService.Instance.CreateAllocationAsync(
                maximumConnections);

        string joinCode =
            await RelayService.Instance.GetJoinCodeAsync(
                allocation.AllocationId);

        RelayServerData relayServerData =
            AllocationUtils.ToRelayServerData(
                allocation,
                GetRelayConnectionType());

        return new RelayHostConnectionData(
            joinCode,
            relayServerData);
    }

    public async Task<RelayServerData> JoinConnectionAsync(
        string joinCode)
    {
        if (!isReady)
        {
            throw new InvalidOperationException(
                "RelayConnectionService is not ready.");
        }

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            throw new ArgumentException(
                "Relay join code cannot be empty.",
                nameof(joinCode));
        }

        await EnsureServicesReadyAsync();

        string normalizedJoinCode =
            joinCode.Trim().ToUpperInvariant();

        JoinAllocation allocation =
            await RelayService.Instance.JoinAllocationAsync(
                normalizedJoinCode);

        return AllocationUtils.ToRelayServerData(
            allocation,
            GetRelayConnectionType());
    }

    private async Task EnsureServicesReadyAsync()
    {
        if (UnityServices.State ==
                ServicesInitializationState.Initialized &&
            AuthenticationService.Instance.IsSignedIn)
        {
            return;
        }

        if (servicesInitializationTask == null ||
            servicesInitializationTask.IsCompleted)
        {
            servicesInitializationTask =
                InitializeServicesAsync();
        }

        try
        {
            await servicesInitializationTask;
        }
        catch
        {
            servicesInitializationTask = null;
            throw;
        }
    }

    private async Task InitializeServicesAsync()
    {
        if (UnityServices.State !=
            ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance
                .SignInAnonymouslyAsync();
        }
    }

    private string GetRelayConnectionType()
    {
        switch (runtimeConfig.RelayConnectionType)
        {
            case NetworkRelayConnectionType.Udp:
                return "udp";

            case NetworkRelayConnectionType.Wss:
                return "wss";

            case NetworkRelayConnectionType.Dtls:
            default:
                return "dtls";
        }
    }
}

public readonly struct RelayHostConnectionData
{
    public string JoinCode { get; }
    public RelayServerData ServerData { get; }


    public RelayHostConnectionData(
        string joinCode,
        RelayServerData serverData)
    {
        JoinCode = joinCode;
        ServerData = serverData;
    }
}