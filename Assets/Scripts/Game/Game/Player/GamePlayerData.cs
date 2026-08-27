using System;

[Serializable]
public class GamePlayerData
{
    public string userId;
    public UserTag userTag;
    public string playerName;
    public string iconId;

    public bool isLobbyHost;
    public bool isConnected;
    public bool canRejoin;

    public LobbyBoardData boardData;

    public bool HasValidPlayer =>
        !string.IsNullOrWhiteSpace(userId) &&
        !string.IsNullOrWhiteSpace(playerName) &&
        boardData != null;

    public GamePlayerData()
    {
        userId = string.Empty;
        userTag = UserTag.Player;
        playerName = string.Empty;
        iconId = string.Empty;
        isLobbyHost = false;
        isConnected = false;
        canRejoin = true;
        boardData = new LobbyBoardData();
    }

    public GamePlayerData(LobbyPlayerData lobbyPlayerData) : this()
    {
        if (lobbyPlayerData?.userData == null)
        {
            return;
        }

        userId = lobbyPlayerData.userData.userId ?? string.Empty;
        userTag = lobbyPlayerData.userData.userTag;
        playerName = lobbyPlayerData.userData.playerName ?? string.Empty;
        iconId = lobbyPlayerData.userData.iconId ?? string.Empty;
        isLobbyHost = lobbyPlayerData.isHost;
        isConnected = true;
        canRejoin = userTag != UserTag.Bot;
        boardData = new LobbyBoardData(lobbyPlayerData.boardData);
    }

    public GamePlayerData(GamePlayerData playerData) : this()
    {
        if (playerData == null)
        {
            return;
        }

        userId = playerData.userId ?? string.Empty;
        userTag = playerData.userTag;
        playerName = playerData.playerName ?? string.Empty;
        iconId = playerData.iconId ?? string.Empty;
        isLobbyHost = playerData.isLobbyHost;
        isConnected = playerData.isConnected;
        canRejoin = playerData.canRejoin;
        boardData = new LobbyBoardData(playerData.boardData);
    }
}
