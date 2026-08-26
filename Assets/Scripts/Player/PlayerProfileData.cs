using System;

[Serializable]
public class PlayerProfileData
{
    public string userId;
    public string playerName;
    public string iconId;

    public bool IsValid => !string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(playerName);

    public PlayerProfileData()
    {
        userId = string.Empty;
        playerName = string.Empty;
        iconId = string.Empty;
    }

    public PlayerProfileData(string userId, string playerName, string iconId)
    {
        this.userId = userId?.Trim() ?? string.Empty;
        this.playerName = playerName?.Trim() ?? string.Empty;
        this.iconId = iconId?.Trim() ?? string.Empty;
    }

    public PlayerProfileData(UserData userData) : this(
        userData?.userId,
        userData?.playerName,
        userData?.iconId)
    {
    }

    public PlayerProfileData(LobbyPlayerViewData playerData) : this(
        playerData?.userId,
        playerData?.playerName,
        playerData?.iconId)
    {
    }

    public PlayerProfileData Clone()
    {
        return new PlayerProfileData(userId, playerName, iconId);
    }

    public bool Matches(PlayerProfileData other)
    {
        return other != null &&
               string.Equals(userId, other.userId, StringComparison.Ordinal) &&
               string.Equals(playerName, other.playerName, StringComparison.Ordinal) &&
               string.Equals(iconId, other.iconId, StringComparison.Ordinal);
    }
}
