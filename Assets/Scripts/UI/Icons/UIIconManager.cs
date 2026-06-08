using System.Collections.Generic;
using UnityEngine;

public class UIIconManager : MonoBehaviour
{
    public static UIIconManager instance;

    [Header("System Icons")]
    [SerializeField] private List<UserIconData> systemIcons = new List<UserIconData>();

    [Header("Player Icons")]
    [SerializeField] private List<UserIconData> playerIcons = new List<UserIconData>();

    public IReadOnlyList<UserIconData> SystemIcons => systemIcons;
    public IReadOnlyList<UserIconData> PlayerIcons => playerIcons;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    public UserIconData GetSystemIconByLookupName(string lookupName)
    {
        if (string.IsNullOrWhiteSpace(lookupName))
        {
            return null;
        }

        for (int i = 0; i < systemIcons.Count; i++)
        {
            UserIconData iconData = systemIcons[i];

            if (iconData != null && iconData.LookupName == lookupName)
            {
                return iconData;
            }
        }

        return null;
    }

    public UserIconData GetPlayerIconById(string iconId)
    {
        if (string.IsNullOrWhiteSpace(iconId))
        {
            return GetFirstPlayerIcon();
        }

        for (int i = 0; i < playerIcons.Count; i++)
        {
            UserIconData iconData = playerIcons[i];

            if (iconData != null && iconData.IconId == iconId)
            {
                return iconData;
            }
        }

        return GetFirstPlayerIcon();
    }

    public UserIconData GetFirstPlayerIcon()
    {
        for (int i = 0; i < playerIcons.Count; i++)
        {
            UserIconData iconData = playerIcons[i];

            if (iconData != null && iconData.IsValid())
            {
                return iconData;
            }
        }

        return null;
    }

    public string GetFirstPlayerIconId()
    {
        UserIconData firstIcon = GetFirstPlayerIcon();

        return firstIcon != null ? firstIcon.IconId : string.Empty;
    }

    public Sprite GetPlayerIconSpriteById(string iconId)
    {
        UserIconData iconData = GetPlayerIconById(iconId);

        return iconData != null ? iconData.IconSprite : null;
    }

    public Sprite GetSystemIconSpriteByLookupName(string lookupName)
    {
        UserIconData iconData = GetSystemIconByLookupName(lookupName);

        return iconData != null ? iconData.IconSprite : null;
    }
}