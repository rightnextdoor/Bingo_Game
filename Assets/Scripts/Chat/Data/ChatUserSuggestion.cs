using System;

[Serializable]
public class ChatUserSuggestion
{
    public string userId;
    public string playerName;
    public string iconId;
    public string displayName;

    public ChatUserSuggestion()
    {
        userId = string.Empty;
        playerName = string.Empty;
        iconId = string.Empty;
        displayName = string.Empty;
    }

    public ChatUserSuggestion(ChatParticipantData participant, string displayName)
    {
        userId = participant?.userId ?? string.Empty;
        playerName = participant?.playerName ?? string.Empty;
        iconId = participant?.iconId ?? string.Empty;
        this.displayName = displayName ?? string.Empty;
    }
}
