using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalIconBarController : MonoBehaviour
{
    #region Data

    [Serializable]
    private class TopBarIconEntry
    {
        [SerializeField] private UIIconType iconType;
        [SerializeField] private UIMessageData tooltipMessageData;

        public UIIconType IconType => iconType;
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

    #endregion

    #region Fields

    [Header("Managers")]
    private PopupManager popupManager;
    private UserManager userManager;
    private UIIconManager iconManager;

    [SerializeField] private IconSelectPopupController iconSelectPopupController;

    [Header("Top Icon Bar")]
    [SerializeField] private RectTransform topIconGroup;
    [SerializeField] private UITopBarIconSlot topIconSlotPrefab;

    [Header("Ordered Icons")]
    [SerializeField] private List<TopBarIconEntry> topBarIcons = new List<TopBarIconEntry>();

    private readonly List<RuntimeTopBarIcon> runtimeIcons = new List<RuntimeTopBarIcon>();

    private Coroutine delayedRefreshRoutine;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        CacheManagers();

        BuildTopIconBar();

        UserManager.UserChanged += RefreshTopBarIcons;
        SaveManager.SaveDataChanged += RefreshTopBarIcons;

        RefreshTopBarIcons();
        QueueDelayedRefresh();
    }

    private void OnDisable()
    {
        if (delayedRefreshRoutine != null)
        {
            StopCoroutine(delayedRefreshRoutine);
            delayedRefreshRoutine = null;
        }

        UserManager.UserChanged -= RefreshTopBarIcons;
        SaveManager.SaveDataChanged -= RefreshTopBarIcons;
    }

    #endregion

    #region Build

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

            if (entry == null || entry.IconType == UIIconType.None)
                continue;

            UIIconType iconType = entry.IconType;
            Sprite iconSprite = GetSpriteForEntry(entry);

            UITopBarIconSlot slot = Instantiate(topIconSlotPrefab, topIconGroup);

            slot.name = $"UITopBarIconSlot_{iconType}";
            slot.Setup(
                iconSprite,
                () => HandleTopBarAction(iconType),
                entry.TooltipMessageData);

            runtimeIcons.Add(new RuntimeTopBarIcon(entry, slot));
        }
    }

    private void ClearTopIconSlots()
    {
        runtimeIcons.Clear();

        if (topIconGroup == null)
            return;

        for (int i = topIconGroup.childCount - 1; i >= 0; i--)
        {
            Transform child = topIconGroup.GetChild(i);

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    #endregion

    #region Actions

    private void HandleTopBarAction(UIIconType iconType)
    {
        CacheManagers();

        CloseIconSelectPopupIfOpen();

        if (popupManager == null)
        {
            Debug.LogWarning("GlobalIconBarController could not find PopupManager.instance.");
            return;
        }

        switch (iconType)
        {
            case UIIconType.User:
                ToggleUserPopup();
                break;

            case UIIconType.Leaderboard:
                popupManager.TogglePopup(PopupId.Leaderboard);
                break;

            case UIIconType.Settings:
                popupManager.TogglePopup(PopupId.Settings);
                break;

            default:
                Debug.LogWarning($"GlobalIconBarController does not have an action for {iconType}.");
                break;
        }
    }

    private void ToggleUserPopup()
    {
        CacheManagers();

        if (userManager != null && userManager.HasUser)
            popupManager.TogglePopup(PopupId.UserInfo);
        else
            popupManager.TogglePopup(PopupId.CreateUser);
    }

    #endregion

    #region Icon Display

    private void RefreshTopBarIcons()
    {
        CacheManagers();

        for (int i = 0; i < runtimeIcons.Count; i++)
        {
            RuntimeTopBarIcon runtimeIcon = runtimeIcons[i];

            if (runtimeIcon == null ||
                runtimeIcon.Slot == null ||
                runtimeIcon.Entry == null)
            {
                continue;
            }

            Sprite iconSprite = GetSpriteForEntry(runtimeIcon.Entry);
            runtimeIcon.Slot.SetIcon(iconSprite);
        }
    }

    private Sprite GetSpriteForEntry(TopBarIconEntry entry)
    {
        if (entry == null || iconManager == null)
            return null;

        if (entry.IconType == UIIconType.User)
        {
            Sprite savedUserIconSprite = GetSavedUserIconSprite();

            if (savedUserIconSprite != null)
                return savedUserIconSprite;
        }

        return iconManager.GetNonPlayerIconSprite(entry.IconType);
    }

    private Sprite GetSavedUserIconSprite()
    {
        CacheManagers();

        if (userManager == null ||
            !userManager.HasUser ||
            userManager.CurrentUser == null ||
            iconManager == null)
        {
            return null;
        }

        string iconId = userManager.CurrentUser.iconId;

        if (!iconManager.HasValidPlayerIconId(iconId))
            return null;

        return iconManager.GetPlayerIconSpriteById(iconId);
    }

    #endregion

    #region Refresh

    private void QueueDelayedRefresh()
    {
        if (delayedRefreshRoutine != null)
            StopCoroutine(delayedRefreshRoutine);

        delayedRefreshRoutine = StartCoroutine(DelayedRefreshTopBarIcons());
    }

    private IEnumerator DelayedRefreshTopBarIcons()
    {
        yield return null;

        delayedRefreshRoutine = null;

        CacheManagers();
        RefreshTopBarIcons();
    }

    #endregion

    #region Helpers

    private void CacheManagers()
    {
        FindMissingReferences();
    }

    private void CloseIconSelectPopupIfOpen()
    {
        if (iconSelectPopupController == null)
            return;

        if (!iconSelectPopupController.gameObject.activeInHierarchy)
            return;

        iconSelectPopupController.CloseIconPopup();
    }

    private void FindMissingReferences()
    {
        if (popupManager == null)
            popupManager = PopupManager.instance;

        if (userManager == null)
            userManager = UserManager.instance;

        if (iconManager == null)
            iconManager = UIIconManager.instance;
    }

    #endregion
}