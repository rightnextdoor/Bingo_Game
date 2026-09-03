using System.Threading.Tasks;
using System.Collections.Generic;

public interface IGameSessionService
{
    SessionRuntimeType RuntimeType { get; }
    bool IsReady { get; }

    Task<GameSessionResult> RejoinGameAsync(string gameId, UserData userData);
    Task<GameSessionResult> SyncGameSessionAsync(string gameId, string lobbyId, UserData userData);
    Task<GameSessionResult> SetGameSceneReadyAsync(string gameId, UserData userData);
    Task<GameSessionResult> LeaveGameAsync(string gameId, UserData userData);
    bool TrySetPlayerMarkedCell(
        string gameId,
        UserData userData,
        int cellIndex,
        bool isMarked,
        out GamePlayerMarkedCellChangedData updateData);
    bool TrySubmitBingoCheck(
        string gameId,
        UserData userData,
        LobbyBoardData boardData,
        IReadOnlyList<int> markedCellIndices,
        out GameBingoCheckResolvedData resolvedData);
    bool TryCompleteBingoCheckAnimation(string gameId, UserData userData);
}
