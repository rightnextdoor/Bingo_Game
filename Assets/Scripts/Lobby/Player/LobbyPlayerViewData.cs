using System;

[Serializable]
public class LobbyPlayerViewData
{
    public string userId;
    public UserTag userTag;
    public string playerName;
    public string iconId;

    public bool isHost;
    public bool isReady;

    public LobbyBoardData boardData;

    public LobbyPlayerViewData()
    {
        userId = string.Empty;
        userTag = UserTag.Player;
        playerName = string.Empty;
        iconId = string.Empty;

        isHost = false;
        isReady = false;

        boardData = new LobbyBoardData();
    }

    public LobbyPlayerViewData(LobbyPlayerData playerData) : this()
    {
        if (playerData?.userData == null)
        {
            return;
        }

        userId = playerData.userData.userId ?? string.Empty;
        userTag = playerData.userData.userTag;
        playerName = playerData.userData.playerName ?? string.Empty;
        iconId = playerData.userData.iconId ?? string.Empty;

        isHost = playerData.isHost;
        isReady = playerData.isReady;

        boardData = new LobbyBoardData(playerData.boardData);
    }
}