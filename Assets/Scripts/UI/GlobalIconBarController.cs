using System;
using System.Collections.Generic;
using UnityEngine;

public class GlobalIconBarController : MonoBehaviour
{
    private enum TopBarAction
    {
        User,
        Leaderboard,
        Settings
    }

    [Serializable]
    private class TopBarIconEntry
    {
        [SerializeField] private TopBarAction action;
        [SerializeField] private UserIconData iconData;
        [SerializeField] private UIMessageData tooltipMessageData;

        public TopBarAction Action => action;
        public UserIconData IconData => iconData;
        public UIMessageData TooltipMessageData => tooltipMessageData;
    }

    private class RuntimeTopBarIcon
    {
        public TopBarIconEntry Entry { get; }
        public UITopBarIconSlot Slot { get; }

        public RuntimeTopBarIcon(TopBarIconEntry entry, UITopBarIconSlot slot)
        {
            Entry = entry;
            Slot = slot;
        }
    }

    [Header("Managers")]
    private PopupManager popupManager;
    private UserManager userManager;
    [SerializeField] private UIIconManager iconManager;
    [SerializeField] private IconSelectPopupController iconSelectPopupController;

    [Header("Top Icon Bar")]
    [SerializeField] private RectTransform topIconGroup;
    [SerializeField] private UITopBarIconSlot topIconSlotPrefab;

    [Header("Ordered Icons")]
    [SerializeField] private List<TopBarIconEntry> topBarIcons = new List<TopBarIconEntry>();

    private readonly List<RuntimeTopBarIcon> runtimeIcons = new List<RuntimeTopBarIcon>();

    private void OnEnable()
    {
        CacheManagers();

        BuildTopIconBar();

        UserManager.UserChanged += RefreshTopBarIcons;
        SaveManager.SaveDataChanged += RefreshTopBarIcons;

        RefreshTopBarIcons();
    }

    private void OnDisable()
    {
        UserManager.UserChanged -= RefreshTopBarIcons;
        SaveManager.SaveDataChanged -= RefreshTopBarIcons;
    }

    private void CacheManagers()
    {
        if (popupManager == null)
        {
            popupManager = PopupManager.instance;
        }

        if (userManager == null)
        {
            userManager = UserManager.instance;
        }

        if (iconManager == null)
        {
            iconManager = UIIconManager.instance;
        }
    }

    public void BuildTopIconBar()
    {
        FindMissingReferences();
        ClearTopIconSlots();

        if (topIconGroup == null)
        {
            Debug.LogWarning("GlobalIconBarController needs Top Icon Group assigned.");
            return;
        }

        if (topIconSlotPrefab == null)
        {
            Debug.LogWarning("GlobalIconBarController needs Top Icon Slot Prefab assigned.");
            return;
        }

        for (int i = 0; i < topBarIcons.Count; i++)
        {
            TopBarIconEntry entry = topBarIcons[i];

            if (entry == null)
            {
                continue;
            }

            TopBarAction action = entry.Action;
            Sprite iconSprite = GetSpriteForEntry(entry);

            UITopBarIconSlot slot = Instantiate(topIconSlotPrefab, topIconGroup);
            slot.name = $"UITopBarIconSlot_{action}";
            slot.Setup(iconSprite, () => HandleTopBarAction(action), entry.TooltipMessageData);

            runtimeIcons.Add(new RuntimeTopBarIcon(entry, slot));
        }
    }

    private void ClearTopIconSlots()
    {
        runtimeIcons.Clear();

        if (topIconGroup == null)
        {
            return;
        }

        for (int i = topIconGroup.childCount - 1; i >= 0; i--)
        {
            Transform child = topIconGroup.GetChild(i);

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void HandleTopBarAction(TopBarAction action)
    {
        CacheManagers();

        CloseIconSelectPopupIfOpen();

        if (popupManager == null)
        {
            Debug.LogWarning("GlobalIconBarController could not find PopupManager.instance.");
            return;
        }

        switch (action)
        {
            case TopBarAction.User:
                ToggleUserPopup();
                break;

            case TopBarAction.Leaderboard:
                popupManager.TogglePopup(PopupId.Leaderboard);
                break;

            case TopBarAction.Settings:
                popupManager.TogglePopup(PopupId.Settings);
                break;
        }
    }

    private void ToggleUserPopup()
    {
        if (userManager != null && userManager.HasUser)
        {
            popupManager.TogglePopup(PopupId.UserInfo);
        }
        else
        {
            popupManager.TogglePopup(PopupId.CreateUser);
        }
    }

    private void RefreshTopBarIcons()
    {
        CacheManagers();

        for (int i = 0; i < runtimeIcons.Count; i++)
        {
            RuntimeTopBarIcon runtimeIcon = runtimeIcons[i];

            if (runtimeIcon == null || runtimeIcon.Slot == null || runtimeIcon.Entry == null)
            {
                continue;
            }

            Sprite iconSprite = GetSpriteForEntry(runtimeIcon.Entry);
            runtimeIcon.Slot.SetIcon(iconSprite);
        }
    }

    private Sprite GetSpriteForEntry(TopBarIconEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        if (entry.Action == TopBarAction.User)
        {
            Sprite savedUserIconSprite = GetSavedUserIconSprite();

            if (savedUserIconSprite != null)
            {
                return savedUserIconSprite;
            }
        }

        return entry.IconData != null ? entry.IconData.IconSprite : null;
    }

    private Sprite GetSavedUserIconSprite()
    {
        if (userManager == null || !userManager.HasUser)
        {
            return null;
        }

        string iconId = userManager.CurrentUser.iconId;

        if (string.IsNullOrWhiteSpace(iconId))
        {
            return null;
        }

        if (iconManager == null)
        {
            iconManager = UIIconManager.instance;
        }

        if (iconManager == null)
        {
            return null;
        }

        IReadOnlyList<UserIconData> playerIcons = iconManager.PlayerIcons;

        for (int i = 0; i < playerIcons.Count; i++)
        {
            UserIconData iconData = playerIcons[i];

            if (iconData == null)
            {
                continue;
            }

            if (iconData.IconId == iconId && iconData.IconSprite != null)
            {
                return iconData.IconSprite;
            }
        }

        return null;
    }

    private void CloseIconSelectPopupIfOpen()
    {
        if (iconSelectPopupController == null)
        {
            return;
        }

        if (!iconSelectPopupController.gameObject.activeInHierarchy)
        {
            return;
        }

        iconSelectPopupController.CloseIconPopup();
    }

    private void FindMissingReferences()
    {
        if (iconManager == null)
        {
            iconManager = UIIconManager.instance;
        }
    }
}