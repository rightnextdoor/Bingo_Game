using System;
using UnityEngine;

[Serializable]
public class ChatSettingsData
{
    public bool chatEnabled = true;

    public bool overrideCurrentUserMessageColor;
    public Color currentUserMessageColor = Color.white;

    public bool overrideOtherUserMessageColor;
    public Color otherUserMessageColor = Color.white;

    public bool overridePrivateMessageColor;
    public Color privateMessageColor = Color.white;

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

        overrideCurrentUserMessageColor = other.overrideCurrentUserMessageColor;
        currentUserMessageColor = other.currentUserMessageColor;

        overrideOtherUserMessageColor = other.overrideOtherUserMessageColor;
        otherUserMessageColor = other.otherUserMessageColor;

        overridePrivateMessageColor = other.overridePrivateMessageColor;
        privateMessageColor = other.privateMessageColor;
    }

    public ChatSettingsData Clone()
    {
        return new ChatSettingsData(this);
    }
}
