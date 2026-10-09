public interface ILobbyView
{
    void DisplayLobbyInfo(LobbyViewData lobbyViewData);
    void RefreshLobbyUI(LobbyViewData _lobbyViewData, BingoGame.UI.LobbyUIRefresh _sections);
}
