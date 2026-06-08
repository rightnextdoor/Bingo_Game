using System;

[Serializable]
public class UserData
{
    public string userId;
    public string playerName;
    public string iconId;
    public string lastGameId;

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
        playerName = string.Empty;
        iconId = string.Empty;
        lastGameId = string.Empty;
        stats = new UserStats();
    }

    public void CreateUser(string newPlayerName)
    {
        CreateUser(newPlayerName, string.Empty);
    }

    public void CreateUser(string newPlayerName, string newIconId)
    {
        userId = Guid.NewGuid().ToString("N");
        playerName = newPlayerName.Trim();
        iconId = string.IsNullOrWhiteSpace(newIconId) ? string.Empty : newIconId.Trim();
        lastGameId = string.Empty;
        stats = new UserStats();
    }

    public void SetIcon(string newIconId)
    {
        iconId = string.IsNullOrWhiteSpace(newIconId) ? string.Empty : newIconId.Trim();
    }

    public void RepairData()
    {
        if (stats == null)
        {
            stats = new UserStats();
        }

        if (!string.IsNullOrWhiteSpace(playerName) && string.IsNullOrWhiteSpace(userId))
        {
            userId = Guid.NewGuid().ToString("N");
        }
    }
}