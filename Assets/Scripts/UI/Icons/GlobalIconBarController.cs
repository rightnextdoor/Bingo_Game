using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalIconBarController : MonoBehaviour
{
    #region Data

    private class RuntimeTopBarIcon
    {
        public UIIconType IconType { get; }
        public UITopBarIconSlot Slot { get; }

        public RuntimeTopBarIcon(UIIconType iconType, UITopBarIconSlot slot)
        {
            IconType = iconType;
            Slot = slot;
        }
    }

    #endregion

    #region Fields

    private static readonly UIIconType[] OrderedIcons = { UIIconType.User, UIIconType.Leaderboard, UIIconType.Settings };

    [Header("Managers")]
    private PopupManager popupManager;
    private UserManager userManager;
    private UIIconManager iconManager;

    [SerializeField] private IconSelectPopupController iconSelectPopupController;

    [Header("Top Icon Bar")]
    [SerializeField] private RectTransform topIconGroup;
    [SerializeField] private UITopBarIconSlot topIconSlotPrefab;

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

        for (int i = 0; i < OrderedIcons.Length; i++)
        {
            UIIconType iconType = OrderedIcons[i];
            Sprite iconSprite = GetSpriteForIcon(iconType);
            UIMessageData tooltipMessageData = GetTooltipMessage(iconType);

            UITopBarIconSlot slot = Instantiate(topIconSlotPrefab, topIconGroup);

            slot.name = $"UITopBarIconSlot_{iconType}";
            slot.Setup(iconSprite, () => HandleTopBarAction(iconType), tooltipMessageData);

            runtimeIcons.Add(new RuntimeTopBarIcon(iconType, slot));
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
        {
            popupManager.TogglePopup(PopupId.UserInfo);
        }
        else
        {
            popupManager.TogglePopup(PopupId.CreateUser);
        }
    }

    #endregion

    #region Icon Display

    private void RefreshTopBarIcons()
    {
        CacheManagers();

        for (int i = 0; i < runtimeIcons.Count; i++)
        {
            RuntimeTopBarIcon runtimeIcon = runtimeIcons[i];

            if (runtimeIcon == null || runtimeIcon.Slot == null)
            {
                continue;
            }

            runtimeIcon.Slot.SetIcon(GetSpriteForIcon(runtimeIcon.IconType));
        }
    }

    private Sprite GetSpriteForIcon(UIIconType iconType)
    {
        if (iconManager == null)
        {
            return null;
        }

        if (iconType == UIIconType.User)
        {
            Sprite savedUserIconSprite = GetSavedUserIconSprite();

            if (savedUserIconSprite != null)
            {
                return savedUserIconSprite;
            }
        }

        return iconManager.GetNonPlayerIconSprite(iconType);
    }

    private Sprite GetSavedUserIconSprite()
    {
        CacheManagers();

        if (userManager == null || !userManager.HasUser || userManager.CurrentUser == null || iconManager == null)
        {
            return null;
        }

        string iconId = userManager.CurrentUser.iconId;

        if (!iconManager.HasValidPlayerIconId(iconId))
        {
            return null;
        }

        return iconManager.GetPlayerIconSpriteById(iconId);
    }

    #endregion

    #region Messages

    private UIMessageData GetTooltipMessage(UIIconType iconType)
    {
        if (UIMessageCatalog.instance == null)
        {
            Debug.LogWarning("GlobalIconBarController could not find UIMessageCatalog.instance.");
            return null;
        }

        return UIMessageCatalog.instance.GetMessage(GetTooltipMessageType(iconType));
    }

    private UIMessageType GetTooltipMessageType(UIIconType iconType)
    {
        switch (iconType)
        {
            case UIIconType.User:
                return UIMessageType.UserTooltip;

            case UIIconType.Leaderboard:
                return UIMessageType.LeaderboardTooltip;

            case UIIconType.Settings:
                return UIMessageType.SettingsTooltip;

            default:
                return UIMessageType.None;
        }
    }

    #endregion

    #region Refresh

    private void QueueDelayedRefresh()
    {
        if (delayedRefreshRoutine != null)
        {
            StopCoroutine(delayedRefreshRoutine);
        }

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
        if (iconSelectPopupController == null || !iconSelectPopupController.gameObject.activeInHierarchy)
        {
            return;
        }

        iconSelectPopupController.CloseIconPopup();
    }

    private void FindMissingReferences()
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

    #endregion
}