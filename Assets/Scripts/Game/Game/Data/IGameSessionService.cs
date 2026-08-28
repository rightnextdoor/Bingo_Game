using System.Threading.Tasks;

public interface IGameSessionService
{
    SessionRuntimeType RuntimeType { get; }
    bool IsReady { get; }

    Task<GameSessionResult> RejoinGameAsync(string gameId, UserData userData);
    Task<GameSessionResult> SyncGameSessionAsync(string gameId, string lobbyId, UserData userData);
    Task<GameSessionResult> SetGameSceneReadyAsync(string gameId, UserData userData);
    Task<GameSessionResult> LeaveGameAsync(string gameId, UserData userData);
}
