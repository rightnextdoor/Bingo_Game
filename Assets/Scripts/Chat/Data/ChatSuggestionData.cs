using System;

[Serializable]
public class ChatSuggestionData
{
    public string userId;
    public string playerName;
    public string iconId;
    public string displayName;

    public bool IsValid => !string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(playerName);

    public ChatSuggestionData()
    {
        userId = string.Empty;
        playerName = string.Empty;
        iconId = string.Empty;
        displayName = string.Empty;
    }

    public ChatSuggestionData(ChatParticipantData participant, string displayName)
    {
        userId = participant?.userId ?? string.Empty;
        playerName = participant?.playerName ?? string.Empty;
        iconId = participant?.iconId ?? string.Empty;
        this.displayName = displayName ?? string.Empty;
    }

    public ChatSuggestionData Clone()
    {
        return new ChatSuggestionData
        {
            userId = userId,
            playerName = playerName,
            iconId = iconId,
            displayName = displayName
        };
    }
}
