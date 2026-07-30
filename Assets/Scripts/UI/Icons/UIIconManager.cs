using System.Collections.Generic;
using UnityEngine;

public class UIIconManager : MonoBehaviour
{
    public static UIIconManager instance;

    #region Fields

    [Header("Player Icons")]
    [SerializeField] private List<UserIconData> playerIcons = new List<UserIconData>();

    [Header("Non Player Icons")]
    [SerializeField] private List<UserIconData> nonPlayerIcons = new List<UserIconData>();

    public IReadOnlyList<UserIconData> PlayerIcons => playerIcons;
    public IReadOnlyList<UserIconData> NonPlayerIcons => nonPlayerIcons;

    #endregion

    #region Unity Lifecycle

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    #endregion

    #region Player Icons

    public UserIconData GetPlayerIconById(string iconId)
    {
        if (string.IsNullOrWhiteSpace(iconId))
            return GetFirstPlayerIcon();

        for (int i = 0; i < playerIcons.Count; i++)
        {
            UserIconData iconData = playerIcons[i];

            if (iconData != null &&
                iconData.IsValid() &&
                iconData.IconId == iconId)
            {
                return iconData;
            }
        }

        return GetFirstPlayerIcon();
    }

    public Sprite GetPlayerIconSpriteById(string iconId)
    {
        UserIconData iconData = GetPlayerIconById(iconId);
        return iconData != null ? iconData.IconSprite : null;
    }

    public bool HasValidPlayerIconId(string iconId)
    {
        if (string.IsNullOrWhiteSpace(iconId))
            return false;

        for (int i = 0; i < playerIcons.Count; i++)
        {
            UserIconData iconData = playerIcons[i];

            if (iconData != null &&
                iconData.IsValid() &&
                iconData.IconId == iconId)
            {
                return true;
            }
        }

        return false;
    }

    public UserIconData GetFirstPlayerIcon()
    {
        for (int i = 0; i < playerIcons.Count; i++)
        {
            UserIconData iconData = playerIcons[i];

            if (iconData != null && iconData.IsValid())
                return iconData;
        }

        return null;
    }

    public string GetFirstPlayerIconId()
    {
        UserIconData firstIcon = GetFirstPlayerIcon();
        return firstIcon != null ? firstIcon.IconId : string.Empty;
    }

    #endregion

    #region Non Player Icons

    public UserIconData GetNonPlayerIcon(UIIconType iconType)
    {
        if (iconType == UIIconType.None)
            return null;

        for (int i = 0; i < nonPlayerIcons.Count; i++)
        {
            UserIconData iconData = nonPlayerIcons[i];

            if (iconData != null &&
                iconData.IsValid() &&
                iconData.IconType == iconType)
            {
                return iconData;
            }
        }

        return null;
    }

    public Sprite GetNonPlayerIconSprite(UIIconType iconType)
    {
        UserIconData iconData = GetNonPlayerIcon(iconType);
        return iconData != null ? iconData.IconSprite : null;
    }

    public UserIconData GetNonPlayerIconById(string iconId)
    {
        if (string.IsNullOrWhiteSpace(iconId))
            return null;

        for (int i = 0; i < nonPlayerIcons.Count; i++)
        {
            UserIconData iconData = nonPlayerIcons[i];

            if (iconData != null &&
                iconData.IsValid() &&
                iconData.IconId == iconId)
            {
                return iconData;
            }
        }

        return null;
    }

    public Sprite GetNonPlayerIconSpriteById(string iconId)
    {
        UserIconData iconData = GetNonPlayerIconById(iconId);
        return iconData != null ? iconData.IconSprite : null;
    }

    public bool HasValidNonPlayerIconId(string iconId)
    {
        return GetNonPlayerIconById(iconId) != null;
    }

    #endregion
}