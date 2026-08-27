using System.Threading.Tasks;

public interface IGameSessionService
{
    SessionRuntimeType RuntimeType { get; }
    bool IsReady { get; }

    Task<GameSessionResult> RejoinGameAsync(string gameId, UserData userData);
}
