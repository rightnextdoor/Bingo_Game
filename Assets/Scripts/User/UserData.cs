using System;

public enum UserTag
{
    Player,
    Bot
}

[Serializable]
public class UserData
{
    public string userId;
    public UserTag userTag;
    public string playerName;
    public string iconId;
    public string lastGameId;
    public string pendingNetworkGameCleanupId;
    public bool hasLastGameDisplayData;
    public MainMenuPlayMode lastGamePlayMode;
    public BingoGameModeType lastGameModeType;

    public UserStats stats;

    public bool HasUser
    {
        get
        {
            return !string.IsNullOrWhiteSpace(userId) &&
                   !string.IsNullOrWhiteSpace(playerName);
        }
    }

    public UserData()
    {
        userId = string.Empty;
        userTag = UserTag.Player;
        playerName = string.Empty;
        iconId = string.Empty;
        lastGameId = string.Empty;
        pendingNetworkGameCleanupId = string.Empty;
        hasLastGameDisplayData = false;
        lastGamePlayMode = MainMenuPlayMode.Online;
        lastGameModeType = BingoGameModeType.Traditional;
        stats = new UserStats();
    }

    public void CreateUser(string newPlayerName)
    {
        CreateUser(newPlayerName, string.Empty);
    }

    public void CreateUser(string newPlayerName, string newIconId)
    {
        userId = Guid.NewGuid().ToString("N");
        userTag = UserTag.Player;
        playerName = newPlayerName.Trim();
        iconId = string.IsNullOrWhiteSpace(newIconId) ? string.Empty : newIconId.Trim();
        lastGameId = string.Empty;
        pendingNetworkGameCleanupId = string.Empty;
        hasLastGameDisplayData = false;
        lastGamePlayMode = MainMenuPlayMode.Online;
        lastGameModeType = BingoGameModeType.Traditional;
        stats = new UserStats();
    }

    public void SetIcon(string newIconId)
    {
        iconId = string.IsNullOrWhiteSpace(newIconId) ? string.Empty : newIconId.Trim();
    }

    public void SetLastGameInfo(
        string gameId,
        MainMenuPlayMode playMode,
        BingoGameModeType gameModeType)
    {
        lastGameId = string.IsNullOrWhiteSpace(gameId)
            ? string.Empty
            : gameId.Trim();
        hasLastGameDisplayData = !string.IsNullOrWhiteSpace(lastGameId);
        lastGamePlayMode = playMode;
        lastGameModeType = gameModeType;
    }

    public void ClearLastGameInfo()
    {
        lastGameId = string.Empty;
        hasLastGameDisplayData = false;
        lastGamePlayMode = MainMenuPlayMode.Online;
        lastGameModeType = BingoGameModeType.Traditional;
    }

    public void RepairData()
    {
        pendingNetworkGameCleanupId ??= string.Empty;
        if (stats == null)
        {
            stats = new UserStats();
        }

        stats.RepairData();

        if (string.IsNullOrWhiteSpace(lastGameId))
        {
            ClearLastGameInfo();
        }

        if (!string.IsNullOrWhiteSpace(playerName) && string.IsNullOrWhiteSpace(userId))
        {
            userId = Guid.NewGuid().ToString("N");
        }
    }
}
