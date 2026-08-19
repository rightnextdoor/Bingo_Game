using System;
using UnityEngine;

[Serializable]
public class ChatSettingsData
{
    public bool chatEnabled = true;
    public Color currentUserMessageColor = Color.white;
    public Color otherUserMessageColor = Color.white;
    public Color privateMessageColor = Color.white;
    public ChatTabType lastSelectedChatTab = ChatTabType.Session;

    public ChatSettingsData()
    {
    }

    public ChatSettingsData(ChatSettingsData other)
    {
        if (other == null)
        {
            return;
        }

        chatEnabled = other.chatEnabled;
        currentUserMessageColor = other.currentUserMessageColor;
        otherUserMessageColor = other.otherUserMessageColor;
        privateMessageColor = other.privateMessageColor;
        lastSelectedChatTab = other.lastSelectedChatTab;
    }

    public ChatSettingsData Clone()
    {
        return new ChatSettingsData(this);
    }
}
