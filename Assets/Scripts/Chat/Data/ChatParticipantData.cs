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
        this.userId = userId ?? string.Empty;
        this.playerName = playerName ?? string.Empty;
        this.iconId = iconId ?? string.Empty;
    }

    public ChatParticipantData Clone()
    {
        return new ChatParticipantData(userId, playerName, iconId);
    }
}
