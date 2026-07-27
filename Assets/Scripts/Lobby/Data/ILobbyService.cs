using System.Threading.Tasks;

public interface ILobbyService
{
    SessionRuntimeType RuntimeType { get; }
    bool IsReady { get; }

    Task<LobbyEntryResult> EnterLobbyAsync(LobbySetupData lobbySetupData);
    Task<LobbyExitResult> LeaveLobbyAsync(string userId);
}
