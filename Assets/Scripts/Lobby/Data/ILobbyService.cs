using System.Collections.Generic;
using System.Threading.Tasks;

public interface ILobbyService
{
    SessionRuntimeType RuntimeType { get; }

    bool IsReady { get; }

    IReadOnlyList<Lobby> Lobbies { get; }

    Task<LobbyEntryResult> EnterLobbyAsync(
        LobbySetupData lobbySetupData);

    Task<LobbyExitResult> LeaveLobbyAsync(
        string userId);
}
