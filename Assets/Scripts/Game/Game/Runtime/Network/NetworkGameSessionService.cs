using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkGameSessionService : MonoBehaviour, IGameSessionService
{
    public static NetworkGameSessionService instance;

    private const float GameConnectionTimeoutSeconds = 15f;

    private bool isReady;
    private NetworkBootstrap networkBootstrap;

    public SessionRuntimeType RuntimeType => SessionRuntimeType.Network;
    public bool IsReady => isReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        isReady = false;
    }

    private IEnumerator Start()
    {
        while (NetworkBootstrap.instance == null || !NetworkBootstrap.instance.IsReady)
        {
            yield return null;
        }

        networkBootstrap = NetworkBootstrap.instance;
        isReady = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public async Task<GameSessionResult> RejoinGameAsync(string gameId, UserData userData)
    {
        if (!isReady)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.ServiceUnavailable,
                "The network Game service is not ready.",
                gameId);
        }

        if (networkBootstrap == null || !networkBootstrap.IsConnected)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.NetworkConnectionFailed,
                "A network connection is required to rejoin this Game.",
                gameId);
        }

        NetworkGameSessionConnection connection = await WaitForLocalGameConnectionAsync();

        if (connection == null)
        {
            return GameSessionResult.Failed(
                GameSessionOperationType.Rejoin,
                GameSessionFailureType.NetworkGameConnectionUnavailable,
                "The network Game connection was not available.",
                gameId);
        }

        return await connection.RequestRejoinGameAsync(gameId);
    }

    private async Task<NetworkGameSessionConnection> WaitForLocalGameConnectionAsync()
    {
        float timeoutTime = Time.realtimeSinceStartup + GameConnectionTimeoutSeconds;

        while (Time.realtimeSinceStartup < timeoutTime)
        {
            NetworkGameSessionConnection connection = NetworkGameSessionConnection.GetLocalConnection();

            if (connection != null)
            {
                return connection;
            }

            await Task.Yield();
        }

        return null;
    }
}
