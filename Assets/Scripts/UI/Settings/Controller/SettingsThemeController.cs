using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsThemeController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Theme Options")]
    [SerializeField] private ThemeOptionItemUI themeOptionPrefab;
    [SerializeField] private RectTransform themeContent;
    [SerializeField] private ToggleGroup themeToggleGroup;

    [Header("Scroll")]
    [SerializeField] private ScrollRect themeScrollRect;
    [SerializeField] private bool scrollSelectedThemeIntoView = true;

    #endregion

    #region Events

    public event Action<UIThemeType> ThemeSelected;

    #endregion

    #region Private Fields

    private readonly List<ThemeOptionItemUI> spawnedThemeItems = new();

    private bool hasInitializedContent;
    private bool hasBuiltThemeOptions;
    private bool isBuilding;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        InitializeContentOnce();
    }

    #endregion

    #region Public Methods

    public void InitializeThemeOptions(IReadOnlyList<UIThemeData> themeDataList, UIThemeType selectedThemeType)
    {
        InitializeContentOnce();

        if (hasBuiltThemeOptions)
        {
            UpdateSelectedTheme(selectedThemeType, false);
            return;
        }

        if (themeOptionPrefab == null)
        {
            Debug.LogWarning("SettingsThemeController needs a Theme Option Prefab.", this);
            return;
        }

        if (themeContent == null)
        {
            Debug.LogWarning("SettingsThemeController needs Theme Content assigned.", this);
            return;
        }

        if (themeDataList == null || themeDataList.Count == 0)
        {
            Debug.LogWarning("SettingsThemeController received no theme data to build.", this);
            return;
        }

        isBuilding = true;

        int selectedIndex = -1;

        for (int i = 0; i < themeDataList.Count; i++)
        {
            UIThemeData themeData = themeDataList[i];

            if (themeData == null)
            {
                continue;
            }

            ThemeOptionItemUI item = Instantiate(themeOptionPrefab, themeContent);
            item.gameObject.SetActive(true);
            item.name = $"ThemeOption_{themeData.ThemeType}";

            item.Setup(
                themeData,
                selectedThemeType,
                themeToggleGroup,
                OnThemeItemSelected
            );

            spawnedThemeItems.Add(item);

            if (themeData.ThemeType == selectedThemeType)
            {
                selectedIndex = spawnedThemeItems.Count - 1;
            }
        }

        hasBuiltThemeOptions = true;
        isBuilding = false;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(themeContent);

        if (selectedIndex >= 0 && scrollSelectedThemeIntoView)
        {
            ScrollToThemeItem(selectedIndex);
        }
        else
        {
            ResetScrollPosition();
        }
    }

    public void UpdateSelectedTheme(UIThemeType selectedThemeType, bool moveIntoView)
    {
        int selectedIndex = -1;

        for (int i = 0; i < spawnedThemeItems.Count; i++)
        {
            ThemeOptionItemUI item = spawnedThemeItems[i];

            if (item == null)
            {
                continue;
            }

            bool isSelected = item.GetThemeType() == selectedThemeType;
            item.SetSelected(isSelected);

            if (isSelected)
            {
                selectedIndex = i;
            }
        }

        if (moveIntoView && selectedIndex >= 0)
        {
            ScrollToThemeItem(selectedIndex);
        }
    }

    #endregion

    #region Setup Helpers

    private void InitializeContentOnce()
    {
        if (hasInitializedContent)
        {
            return;
        }

        FindMissingReferences();
        ClearStarterChildrenOnce();

        hasInitializedContent = true;
    }

    private void FindMissingReferences()
    {
        if (themeScrollRect == null)
        {
            themeScrollRect = GetComponentInChildren<ScrollRect>(true);
        }

        if (themeScrollRect != null && themeContent == null)
        {
            themeContent = themeScrollRect.content;
        }

        if (themeContent != null && themeToggleGroup == null)
        {
            themeToggleGroup = themeContent.GetComponent<ToggleGroup>();
        }
    }

    private void ClearStarterChildrenOnce()
    {
        spawnedThemeItems.Clear();

        if (themeContent == null)
        {
            return;
        }

        for (int i = themeContent.childCount - 1; i >= 0; i--)
        {
            Transform child = themeContent.GetChild(i);

            if (child == null)
            {
                continue;
            }

            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    #endregion

    #region Selection

    private void OnThemeItemSelected(UIThemeType selectedThemeType)
    {
        if (isBuilding)
        {
            return;
        }

        ThemeSelected?.Invoke(selectedThemeType);
    }

    #endregion

    #region Scroll

    private void ResetScrollPosition()
    {
        if (themeScrollRect == null)
        {
            return;
        }

        themeScrollRect.horizontalNormalizedPosition = 0f;
        themeScrollRect.verticalNormalizedPosition = 1f;
    }

    private void ScrollToThemeItem(int selectedIndex)
    {
        if (themeScrollRect == null || themeContent == null)
        {
            return;
        }

        if (selectedIndex < 0 || selectedIndex >= spawnedThemeItems.Count)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(themeContent);

        RectTransform viewport = themeScrollRect.viewport;

        if (viewport == null)
        {
            return;
        }

        RectTransform selectedItemRect = spawnedThemeItems[selectedIndex].GetRectTransform();

        if (selectedItemRect == null)
        {
            return;
        }

        if (themeScrollRect.horizontal)
        {
            ScrollToThemeItemHorizontal(selectedItemRect, viewport);
        }

        if (themeScrollRect.vertical)
        {
            ScrollToThemeItemVertical(selectedItemRect, viewport);
        }
    }

    private void ScrollToThemeItemHorizontal(RectTransform selectedItemRect, RectTransform viewport)
    {
        float contentWidth = themeContent.rect.width;
        float viewportWidth = viewport.rect.width;
        float overflowWidth = contentWidth - viewportWidth;

        if (overflowWidth <= 0f)
        {
            themeScrollRect.horizontalNormalizedPosition = 0f;
            return;
        }

        float itemLeft = selectedItemRect.anchoredPosition.x - selectedItemRect.rect.width * selectedItemRect.pivot.x;
        float itemCenter = itemLeft + selectedItemRect.rect.width * 0.5f;

        float targetContentX = viewportWidth * 0.5f - itemCenter;
        targetContentX = Mathf.Clamp(targetContentX, -overflowWidth, 0f);

        float normalizedPosition = Mathf.Clamp01(-targetContentX / overflowWidth);

        themeScrollRect.horizontalNormalizedPosition = normalizedPosition;
    }

    private void ScrollToThemeItemVertical(RectTransform selectedItemRect, RectTransform viewport)
    {
        float contentHeight = themeContent.rect.height;
        float viewportHeight = viewport.rect.height;
        float overflowHeight = contentHeight - viewportHeight;

        if (overflowHeight <= 0f)
        {
            themeScrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        float itemTop = -selectedItemRect.anchoredPosition.y - selectedItemRect.rect.height * (1f - selectedItemRect.pivot.y);
        float itemCenter = itemTop + selectedItemRect.rect.height * 0.5f;

        float targetContentY = viewportHeight * 0.5f - itemCenter;
        targetContentY = Mathf.Clamp(targetContentY, -overflowHeight, 0f);

        float normalizedPosition = Mathf.Clamp01(1f + targetContentY / overflowHeight);

        themeScrollRect.verticalNormalizedPosition = normalizedPosition;
    }

    #endregion
}