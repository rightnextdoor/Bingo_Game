using System;

[Serializable]
public class ChatParticipantData
{
    public string userId;
    public string playerName;
    public string iconId;

    public bool IsValid => !string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(playerName);

    public ChatParticipantData()
    {
        userId = string.Empty;
        playerName = string.Empty;
        iconId = string.Empty;
    }

    public ChatParticipantData(string userId, string playerName, string iconId)
    {
        this.userId = userId?.Trim() ?? string.Empty;
        this.playerName = playerName?.Trim() ?? string.Empty;
        this.iconId = iconId?.Trim() ?? string.Empty;
    }

    public ChatParticipantData(PlayerProfileData profile) : this(profile?.userId, profile?.playerName, profile?.iconId)
    {
    }

    public ChatParticipantData Clone()
    {
        return new ChatParticipantData(userId, playerName, iconId);
    }
}
