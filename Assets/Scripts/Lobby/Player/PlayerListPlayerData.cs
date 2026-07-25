using System;
using System.Collections.Generic;

[Serializable]
public class PlayerListPlayerData
{
    public string userId;
    public UserTag userTag;
    public string playerName;
    public string iconId;

    public bool isHost;
    public bool isReady;

    public LobbyBoardData boardData;
    public List<int> markedCellIndices;

    public bool canKick;
    public bool showBotIcon;
    public bool showReadyIcon;

    public PlayerListPlayerData()
    {
        userId = string.Empty;
        userTag = UserTag.Player;
        playerName = string.Empty;
        iconId = string.Empty;

        isHost = false;
        isReady = false;

        boardData = new LobbyBoardData();
        markedCellIndices = new List<int>();

        canKick = false;
        showBotIcon = false;
        showReadyIcon = false;
    }

    public PlayerListPlayerData(LobbyPlayerViewData playerData) : this()
    {
        if (playerData == null)
        {
            return;
        }

        userId = playerData.userId ?? string.Empty;
        userTag = playerData.userTag;
        playerName = playerData.playerName ?? string.Empty;
        iconId = playerData.iconId ?? string.Empty;

        isHost = playerData.isHost;
        isReady = playerData.isReady;

        boardData = new LobbyBoardData(playerData.boardData);
    }
}