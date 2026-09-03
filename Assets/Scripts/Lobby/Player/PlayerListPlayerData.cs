using System;
using System.Collections.Generic;

[Serializable]
public class PlayerListPlayerData
{
    #region Fields

    public string userId;
    public UserTag userTag;
    public string playerName;
    public string iconId;
    public string displayName;
    public string displayUserId;

    public bool isHost;
    public bool isReady;

    public LobbyBoardData boardData;
    public List<int> markedCellIndices;

    public bool canKick;
    public bool showBotIcon;
    public bool showReadyIcon;
    public string gameplayStatusText;

    #endregion

    #region Constructors

    public PlayerListPlayerData()
    {
        userId = string.Empty;
        userTag = UserTag.Player;
        playerName = string.Empty;
        iconId = string.Empty;
        displayName = string.Empty;
        displayUserId = string.Empty;
        isHost = false;
        isReady = false;
        boardData = new LobbyBoardData();
        markedCellIndices = new List<int>();
        canKick = false;
        showBotIcon = false;
        showReadyIcon = false;
        gameplayStatusText = string.Empty;
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
    }

    public void ApplyProfile(PlayerProfileData profile)
    {
        if (profile == null || !profile.IsValid || !string.Equals(userId, profile.userId, StringComparison.Ordinal))
        {
            return;
        }

        playerName = profile.playerName;
        iconId = profile.iconId;
    }

    public PlayerProfileData BuildProfile()
    {
        return new PlayerProfileData(userId, playerName, iconId);
    }

    #endregion
}
