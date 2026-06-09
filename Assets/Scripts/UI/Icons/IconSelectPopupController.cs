using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

public class IconSelectPopupController : MonoBehaviour
{
    private Action<UserIconData> iconSelectedCallback;

    [Header("Managers")]
    [SerializeField] private UIIconManager iconManager;

    [Header("Popup Rects")]
    [SerializeField] private RectTransform popupRect;
    [SerializeField] private RectTransform scrollViewRect;
    [SerializeField] private RectTransform viewportRect;
    [SerializeField] private RectTransform iconSlotParent;
    [SerializeField] private RectTransform verticalScrollbarRect;

    [Header("Scroll")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Scrollbar verticalScrollbar;

    [Header("Slot Setup")]
    [SerializeField] private UIUserIconSlot iconSlotPrefab;
    [SerializeField] private GridLayoutGroup gridLayoutGroup;

    [Header("Grid Size")]
    [SerializeField] private int maxColumns = 5;
    [SerializeField] private int maxVisibleRows = 3;
    [SerializeField] private Vector2 cellSize = new Vector2(96f, 96f);
    [SerializeField] private Vector2 spacing = new Vector2(16f, 16f);

    [Header("Grid Padding")]
    [SerializeField] private int gridPaddingLeft = 16;
    [SerializeField] private int gridPaddingRight = 16;
    [SerializeField] private int gridPaddingTop = 16;
    [SerializeField] private int gridPaddingBottom = 16;

    [Header("Popup Size")]
    [SerializeField] private float headerHeight = 70f;
    [SerializeField] private float topPadding = 20f;
    [SerializeField] private float bottomPadding = 32f;
    [SerializeField] private float sidePadding = 32f;
    [SerializeField] private float headerToGridSpacing = 10f;
    [SerializeField] private float scrollbarWidth = 20f;
    [SerializeField] private float scrollbarSpacing = 0f;

    [Header("Test Settings")]
    [SerializeField] private bool populateOnEnable = true;
    [SerializeField] private bool autoCloseOnSelect = true;

    [Header("Close Behavior")]
    [SerializeField] private bool closeWhenClickOutside = true;

    private Canvas parentCanvas;

    private readonly List<UIUserIconSlot> spawnedSlots = new List<UIUserIconSlot>();

    private string selectedIconId = string.Empty;

    public string SelectedIconId => selectedIconId;

    private void Awake()
    {
        FindMissingReferences();
        parentCanvas = GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        if (!closeWhenClickOutside)
        {
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            CloseIfPointerIsOutside(Mouse.current.position.ReadValue());
            return;
        }

        if (Touchscreen.current != null)
        {
            for (int i = 0; i < Touchscreen.current.touches.Count; i++)
            {
                TouchControl touch = Touchscreen.current.touches[i];

                if (touch.press.wasPressedThisFrame)
                {
                    CloseIfPointerIsOutside(touch.position.ReadValue());
                    return;
                }
            }
        }
    }

    private void OnEnable()
    {
        if (populateOnEnable)
        {
            PopulateIconSlots();
        }
    }

    private void CloseIfPointerIsOutside(Vector2 screenPosition)
    {
        if (popupRect == null)
        {
            return;
        }

        Camera uiCamera = GetUICamera();

        bool pointerIsInsidePopup = RectTransformUtility.RectangleContainsScreenPoint(
            popupRect,
            screenPosition,
            uiCamera
        );

        if (pointerIsInsidePopup)
        {
            return;
        }

        CloseIconPopup();
    }

    private Camera GetUICamera()
    {
        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>();
        }

        if (parentCanvas == null)
        {
            return null;
        }

        if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return parentCanvas.worldCamera;
    }

    public void OpenForSelection(string currentIconId, Action<UserIconData> onIconSelected)
    {
        selectedIconId = string.IsNullOrWhiteSpace(currentIconId) ? string.Empty : currentIconId;
        iconSelectedCallback = onIconSelected;

        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>();
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        else
        {
            PopulateIconSlots();
        }
    }

    public void PopulateIconSlots()
    {
        FindMissingReferences();
        ClearIconSlots();

        if (iconManager == null)
        {
            iconManager = UIIconManager.instance;
        }

        if (iconManager == null)
        {
            Debug.LogWarning("IconSelectPopupController could not find UIIconManager.");
            return;
        }

        if (iconSlotParent == null)
        {
            Debug.LogWarning("IconSelectPopupController needs an icon slot parent.");
            return;
        }

        if (iconSlotPrefab == null)
        {
            Debug.LogWarning("IconSelectPopupController needs an icon slot prefab.");
            return;
        }

        List<UserIconData> validIcons = GetValidPlayerIcons();

        ResizePopup(validIcons.Count);

        for (int i = 0; i < validIcons.Count; i++)
        {
            UserIconData iconData = validIcons[i];

            UIUserIconSlot slot = Instantiate(iconSlotPrefab, iconSlotParent);
            slot.Setup(iconData, SelectIcon);
            slot.SetSelected(iconData.IconId == selectedIconId);

            spawnedSlots.Add(slot);
        }

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }

        if (iconSlotParent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(iconSlotParent);
        }
    }

    public void SelectIcon(UserIconData iconData)
    {
        if (iconData == null)
        {
            return;
        }

        selectedIconId = iconData.IconId;

        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            UIUserIconSlot slot = spawnedSlots[i];

            if (slot == null || slot.IconData == null)
            {
                continue;
            }

            slot.SetSelected(slot.IconData.IconId == selectedIconId);
        }

        iconSelectedCallback?.Invoke(iconData);

        if (autoCloseOnSelect)
        {
            CloseIconPopup();
        }
    }

    public void CloseIconPopup()
    {
        iconSelectedCallback = null;
        gameObject.SetActive(false);
    }

    public void SetSelectedIconId(string iconId)
    {
        selectedIconId = string.IsNullOrWhiteSpace(iconId) ? string.Empty : iconId;

        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            UIUserIconSlot slot = spawnedSlots[i];

            if (slot == null || slot.IconData == null)
            {
                continue;
            }

            slot.SetSelected(slot.IconData.IconId == selectedIconId);
        }
    }

    private void ResizePopup(int iconCount)
    {
        int displayIconCount = Mathf.Max(iconCount, 1);

        int columns = Mathf.Clamp(displayIconCount, 1, maxColumns);
        int totalRows = Mathf.CeilToInt(displayIconCount / (float)columns);
        int visibleRows = Mathf.Min(totalRows, maxVisibleRows);

        bool shouldShowScrollbar = totalRows > visibleRows;

        RectOffset runtimeGridPadding = new RectOffset(
            gridPaddingLeft,
            gridPaddingRight,
            gridPaddingTop,
            gridPaddingBottom
        );

        float gridWidth =
            runtimeGridPadding.left +
            runtimeGridPadding.right +
            columns * cellSize.x +
            Mathf.Max(0, columns - 1) * spacing.x;

        float visibleGridHeight =
            runtimeGridPadding.top +
            runtimeGridPadding.bottom +
            visibleRows * cellSize.y +
            Mathf.Max(0, visibleRows - 1) * spacing.y;

        float totalContentHeight =
            runtimeGridPadding.top +
            runtimeGridPadding.bottom +
            totalRows * cellSize.y +
            Mathf.Max(0, totalRows - 1) * spacing.y;

        float scrollbarExtraWidth = shouldShowScrollbar ? scrollbarWidth + scrollbarSpacing : 0f;

        float scrollViewWidth = gridWidth + scrollbarExtraWidth;
        float scrollViewHeight = visibleGridHeight;

        float popupWidth = scrollViewWidth + sidePadding * 2f;
        float popupHeight = topPadding + headerHeight + headerToGridSpacing + scrollViewHeight + bottomPadding;

        if (popupRect != null)
        {
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.pivot = new Vector2(0.5f, 0.5f);
            popupRect.anchoredPosition = Vector2.zero;
            popupRect.sizeDelta = new Vector2(popupWidth, popupHeight);
        }

        if (scrollViewRect != null)
        {
            scrollViewRect.anchorMin = new Vector2(0.5f, 1f);
            scrollViewRect.anchorMax = new Vector2(0.5f, 1f);
            scrollViewRect.pivot = new Vector2(0.5f, 1f);
            scrollViewRect.anchoredPosition = new Vector2(0f, -(topPadding + headerHeight + headerToGridSpacing));
            scrollViewRect.sizeDelta = new Vector2(scrollViewWidth, scrollViewHeight);
        }

        if (viewportRect != null)
        {
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = shouldShowScrollbar
                ? new Vector2(-(scrollbarWidth + scrollbarSpacing * 2f), 0f)
                : Vector2.zero;
        }

        if (verticalScrollbarRect != null)
        {
            verticalScrollbarRect.gameObject.SetActive(shouldShowScrollbar);
            verticalScrollbarRect.anchorMin = new Vector2(1f, 0f);
            verticalScrollbarRect.anchorMax = new Vector2(1f, 1f);
            verticalScrollbarRect.pivot = new Vector2(1f, 0.5f);
            verticalScrollbarRect.anchoredPosition = new Vector2(-scrollbarSpacing, 0f);
            verticalScrollbarRect.sizeDelta = new Vector2(scrollbarWidth, 0f);
        }

        if (verticalScrollbar != null)
        {
            verticalScrollbar.gameObject.SetActive(shouldShowScrollbar);
        }

        if (scrollRect != null)
        {
            scrollRect.vertical = shouldShowScrollbar;
        }

        if (gridLayoutGroup != null)
        {
            gridLayoutGroup.padding = runtimeGridPadding;
            gridLayoutGroup.cellSize = cellSize;
            gridLayoutGroup.spacing = spacing;
            gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayoutGroup.constraintCount = columns;
            gridLayoutGroup.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayoutGroup.childAlignment = TextAnchor.UpperCenter;
        }

        if (iconSlotParent != null)
        {
            iconSlotParent.anchorMin = new Vector2(0f, 1f);
            iconSlotParent.anchorMax = new Vector2(1f, 1f);
            iconSlotParent.pivot = new Vector2(0.5f, 1f);
            iconSlotParent.anchoredPosition = Vector2.zero;
            iconSlotParent.sizeDelta = new Vector2(0f, totalContentHeight);
        }
    }

    private List<UserIconData> GetValidPlayerIcons()
    {
        List<UserIconData> validIcons = new List<UserIconData>();

        if (iconManager == null)
        {
            return validIcons;
        }

        IReadOnlyList<UserIconData> playerIcons = iconManager.PlayerIcons;

        for (int i = 0; i < playerIcons.Count; i++)
        {
            UserIconData iconData = playerIcons[i];

            if (iconData == null || !iconData.IsValid())
            {
                continue;
            }

            validIcons.Add(iconData);
        }

        return validIcons;
    }

    private void ClearIconSlots()
    {
        spawnedSlots.Clear();

        if (iconSlotParent == null)
        {
            return;
        }

        for (int i = iconSlotParent.childCount - 1; i >= 0; i--)
        {
            Destroy(iconSlotParent.GetChild(i).gameObject);
        }
    }

    private void FindMissingReferences()
    {
        if (popupRect == null)
        {
            popupRect = GetComponent<RectTransform>();
        }

        if (scrollRect == null)
        {
            scrollRect = GetComponentInChildren<ScrollRect>(true);
        }

        if (scrollRect != null)
        {
            if (scrollViewRect == null)
            {
                scrollViewRect = scrollRect.GetComponent<RectTransform>();
            }

            if (viewportRect == null && scrollRect.viewport != null)
            {
                viewportRect = scrollRect.viewport;
            }

            if (iconSlotParent == null && scrollRect.content != null)
            {
                iconSlotParent = scrollRect.content;
            }

            if (verticalScrollbar == null)
            {
                verticalScrollbar = scrollRect.verticalScrollbar;
            }
        }

        if (verticalScrollbar != null && verticalScrollbarRect == null)
        {
            verticalScrollbarRect = verticalScrollbar.GetComponent<RectTransform>();
        }

        if (gridLayoutGroup == null && iconSlotParent != null)
        {
            gridLayoutGroup = iconSlotParent.GetComponent<GridLayoutGroup>();
        }
    }
}