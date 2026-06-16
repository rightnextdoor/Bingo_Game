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

    private bool isBuilding;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        ClearThemeOptions();
    }

    #endregion

    #region Public Methods

    public void BuildThemeOptions(IReadOnlyList<UIThemeData> themeDataList, UIThemeType selectedThemeType)
    {
        isBuilding = true;

        ClearThemeOptions();

        if (themeOptionPrefab == null || themeContent == null)
        {
            isBuilding = false;
            return;
        }

        if (themeDataList == null || themeDataList.Count == 0)
        {
            isBuilding = false;
            return;
        }

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

        isBuilding = false;

        if (selectedIndex >= 0 && scrollSelectedThemeIntoView)
        {
            ScrollToThemeItem(selectedIndex);
        }
    }

    public void SetSelectedTheme(UIThemeType selectedThemeType, bool moveIntoView)
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

    public void ClearThemeOptions()
    {
        for (int i = spawnedThemeItems.Count - 1; i >= 0; i--)
        {
            ThemeOptionItemUI item = spawnedThemeItems[i];

            if (item == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(item.gameObject);
            }
            else
            {
                DestroyImmediate(item.gameObject);
            }
        }

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
        themeScrollRect.verticalNormalizedPosition = 1f;
    }

    #endregion
}