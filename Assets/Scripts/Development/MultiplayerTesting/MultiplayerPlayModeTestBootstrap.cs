using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

[DefaultExecutionOrder(-900)]
[DisallowMultipleComponent]
public class MultiplayerPlayModeTestBootstrap : MonoBehaviour
{
    private const float InitialClientDelaySeconds = 1f;
    private const float ClientRetryDelaySeconds = 1f;
    private const float ConnectionAttemptTimeoutSeconds = 5f;
    private const int MaximumClientAttempts = 10;

    private IEnumerator Start()
    {
        if (!MultiplayerPlayModeTestContext.IsActive)
        {
            yield break;
        }

        NetworkRoot networkRoot =
            GetComponentInParent<NetworkRoot>();

        if (networkRoot == null ||
            !networkRoot.IsPrimaryInstance)
        {
            yield break;
        }

        while (NetworkBootstrap.instance == null ||
               !NetworkBootstrap.instance.IsReady ||
               UserManager.instance == null ||
               !UserManager.instance.IsReady)
        {
            yield return null;
        }

        NetworkBootstrap networkBootstrap = NetworkBootstrap.instance;

        networkBootstrap.PrepareForMultiplayerPlayModeTesting();

        string userId = UserManager.instance.UserId;

        if (MultiplayerPlayModeTestContext.IsHost)
        {
            StartTestHost(
                networkBootstrap,
                userId);

            yield break;
        }

        float clientDelay =
            InitialClientDelaySeconds +
            ((MultiplayerPlayModeTestContext.PlayerNumber - 2) * 0.25f);

        yield return new WaitForSecondsRealtime(
            clientDelay);

        yield return StartTestClient(
            networkBootstrap,
            userId);
    }

    private void StartTestHost(
        NetworkBootstrap networkBootstrap,
        string userId)
    {
        if (networkBootstrap.IsConnected)
        {
            return;
        }

        bool started =
            networkBootstrap.StartDirectHost(
                userId);

        if (!started)
        {
            Debug.LogWarning(
                "[MultiplayerPlayModeTest] Player 1 could not start the Direct Host.");
        }
    }

    private IEnumerator StartTestClient(
        NetworkBootstrap networkBootstrap,
        string userId)
    {
        for (int attempt = 0;
             attempt < MaximumClientAttempts;
             attempt++)
        {
            if (networkBootstrap.IsConnected)
            {
                yield break;
            }

            if (networkBootstrap.ConnectionState !=
                NetworkConnectionState.Offline)
            {
                yield return ResetConnection(
                    networkBootstrap);
            }

            bool started =
                networkBootstrap.StartDirectClient(
                    userId,
                    MultiplayerPlayModeTestContext.DirectAddress);

            if (started)
            {
                float timeoutTime =
                    Time.realtimeSinceStartup +
                    ConnectionAttemptTimeoutSeconds;

                while (!networkBootstrap.IsConnected &&
                       Time.realtimeSinceStartup <
                       timeoutTime)
                {
                    if (networkBootstrap.ConnectionState ==
                            NetworkConnectionState.Failed ||
                        networkBootstrap.ConnectionState ==
                            NetworkConnectionState.Disconnected)
                    {
                        break;
                    }

                    yield return null;
                }

                if (networkBootstrap.IsConnected)
                {
                    yield break;
                }
            }

            yield return ResetConnection(
                networkBootstrap);

            yield return new WaitForSecondsRealtime(
                ClientRetryDelaySeconds);
        }

        Debug.LogWarning(
            $"[MultiplayerPlayModeTest] " +
            $"Player {MultiplayerPlayModeTestContext.PlayerNumber} " +
            "could not connect to the Direct Host.");
    }

    private IEnumerator ResetConnection(
        NetworkBootstrap networkBootstrap)
    {
        Task<bool> shutdownTask =
            networkBootstrap.ShutdownAsync();

        while (!shutdownTask.IsCompleted)
        {
            yield return null;
        }
    }
}