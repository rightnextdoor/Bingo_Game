using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class UIThemeManager : MonoBehaviour
{
    public static UIThemeManager instance;

    [Header("Theme Selection")]
    [SerializeField] private UIThemeType selectedThemeType = UIThemeType.Default;
    private UIThemeData defaultThemeData;

    [Header("Theme Data")]
    [SerializeField] private List<UIThemeData> themeDataList = new();

    private UIThemeData activeThemeData;

    private readonly List<IUIThemeTarget> registeredTargets = new();

    private readonly List<UIThemeBackgroundStyle> activeBackgroundStyles = new();
    private readonly List<UIThemeButtonStyle> activeButtonStyles = new();
    private readonly List<UIThemeTextStyle> activeTextStyles = new();
    private readonly List<UIThemeInputStyle> activeInputStyles = new();
    private readonly List<UIThemeDropdownStyle> activeDropdownStyles = new();
    private readonly List<UIThemeScrollStyle> activeScrollStyles = new();
    private readonly List<UIThemeSliderStyle> activeSliderStyles = new();
    private readonly List<UIThemeToggleStyle> activeToggleStyles = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            if (Application.isPlaying)
            {
                Destroy(this);
            }

            return;
        }

        instance = this;

        SetTheme(selectedThemeType);
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        ReconnectActiveThemeTargetsInEditor();
#endif
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        if (instance != null && instance != this)
        {
            return;
        }

        SetTheme(selectedThemeType);

        if (!Application.isPlaying)
        {
            ReconnectActiveThemeTargetsInEditor();
        }
    }

#endif

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public UIThemeType SelectedThemeType => selectedThemeType;

    public IReadOnlyList<UIThemeData> GetThemeDataList()
    {
        return themeDataList;
    }

    public UIThemeType ValidateAndSetTheme(UIThemeType requestedThemeType)
    {
        UIThemeData resolvedThemeData = FindThemeDataWithoutWarning(requestedThemeType);

        if (resolvedThemeData == null)
        {
            resolvedThemeData = FindFirstValidThemeData();
        }

        if (resolvedThemeData == null)
        {
            Debug.LogWarning("UIThemeManager could not validate theme because no valid theme data exists.");
            return selectedThemeType;
        }

        SetTheme(resolvedThemeData.ThemeType);
        return selectedThemeType;
    }

    public void Register(IUIThemeTarget target)
    {
        if (target == null)
        {
            return;
        }

        UnityEngine.Object targetObject = target as UnityEngine.Object;

        if (targetObject == null)
        {
            return;
        }

        if (!registeredTargets.Contains(target))
        {
            registeredTargets.Add(target);
        }

        target.ReapplyTheme();
    }

    public void Unregister(IUIThemeTarget target)
    {
        if (target == null)
        {
            return;
        }

        registeredTargets.Remove(target);
    }

    public void SetTheme(UIThemeType themeType)
    {
        selectedThemeType = themeType;

        defaultThemeData = FindThemeData(UIThemeType.Default);
        activeThemeData = FindThemeData(themeType);

        RebuildActiveThemeLists();

        ReapplyThemeToRegisteredTargets();
    }

    public object ApplyTheme(UIThemeSectionType sectionType, Enum styleType)
    {
        switch (sectionType)
        {
            case UIThemeSectionType.Background:
                return FindBackgroundStyle(styleType);

            case UIThemeSectionType.Button:
                return FindButtonStyle(styleType);

            case UIThemeSectionType.Text:
                return FindTextStyle(styleType);

            case UIThemeSectionType.Input:
                return FindInputStyle(styleType);

            case UIThemeSectionType.Dropdown:
                return FindDropdownStyle(styleType);

            case UIThemeSectionType.Scroll:
                return FindScrollStyle(styleType);

            case UIThemeSectionType.Slider:
                return FindSliderStyle(styleType);

            case UIThemeSectionType.Toggle:
                return FindToggleStyle(styleType);

            default:
                Debug.LogWarning($"UIThemeManager does not support section type: {sectionType}");
                return null;
        }
    }

    public UIThemeStyle GetBackgroundStyle(UIThemeBackgroundType backgroundType)
    {
        return FindBackgroundStyle(backgroundType);
    }

    public UIThemeStyle GetTextStyle(UIThemeTextType textType)
    {
        return FindTextStyle(textType);
    }

    private UIThemeData FindThemeData(UIThemeType themeType)
    {
        for (int i = 0; i < themeDataList.Count; i++)
        {
            UIThemeData themeData = themeDataList[i];

            if (themeData == null)
            {
                continue;
            }

            if (themeData.ThemeType == themeType)
            {
                return themeData;
            }
        }

        Debug.LogWarning($"UIThemeManager could not find theme data for theme type: {themeType}");
        return null;
    }

    private UIThemeData FindThemeDataWithoutWarning(UIThemeType themeType)
    {
        for (int i = 0; i < themeDataList.Count; i++)
        {
            UIThemeData themeData = themeDataList[i];

            if (themeData == null)
            {
                continue;
            }

            if (themeData.ThemeType == themeType)
            {
                return themeData;
            }
        }

        return null;
    }

    private UIThemeData FindFirstValidThemeData()
    {
        for (int i = 0; i < themeDataList.Count; i++)
        {
            UIThemeData themeData = themeDataList[i];

            if (themeData == null)
            {
                continue;
            }

            return themeData;
        }

        return null;
    }

    private void RebuildActiveThemeLists()
    {
        ClearActiveThemeLists();

        if (activeThemeData == null)
        {
            return;
        }

        activeBackgroundStyles.AddRange(activeThemeData.BackgroundStyles);
        activeButtonStyles.AddRange(activeThemeData.ButtonStyles);
        activeTextStyles.AddRange(activeThemeData.TextStyles);
        activeInputStyles.AddRange(activeThemeData.InputStyles);
        activeDropdownStyles.AddRange(activeThemeData.DropdownStyles);
        activeScrollStyles.AddRange(activeThemeData.ScrollStyles);
        activeSliderStyles.AddRange(activeThemeData.SliderStyles);
        activeToggleStyles.AddRange(activeThemeData.ToggleStyles);
    }

    private void ClearActiveThemeLists()
    {
        activeBackgroundStyles.Clear();
        activeButtonStyles.Clear();
        activeTextStyles.Clear();
        activeInputStyles.Clear();
        activeDropdownStyles.Clear();
        activeScrollStyles.Clear();
        activeSliderStyles.Clear();
        activeToggleStyles.Clear();
    }

    private void ReapplyThemeToRegisteredTargets()
    {
        for (int i = registeredTargets.Count - 1; i >= 0; i--)
        {
            IUIThemeTarget target = registeredTargets[i];

            UnityEngine.Object targetObject = target as UnityEngine.Object;

            if (target == null || targetObject == null)
            {
                registeredTargets.RemoveAt(i);
                continue;
            }

            target.ReapplyTheme();
        }
    }

    #region Find Style

    private UIThemeStyle FindBackgroundStyle(Enum styleType)
    {
        UIThemeBackgroundType backgroundType;

        if (!TryGetEnumType(styleType, out backgroundType))
        {
            Debug.LogWarning("UIThemeManager received invalid background type.");
            return null;
        }

        for (int i = 0; i < activeBackgroundStyles.Count; i++)
        {
            UIThemeBackgroundStyle style = activeBackgroundStyles[i];

            if (style == null)
            {
                continue;
            }

            if (style.BackgroundType == backgroundType)
            {
                return style.Style;
            }
        }

        UIThemeStyle defaultStyle = FindDefaultBackgroundStyle(backgroundType);

        if (defaultStyle != null)
        {
            return defaultStyle;
        }

        Debug.LogWarning($"UIThemeManager could not find background style: {backgroundType}");
        return null;
    }

    private UIThemeStyle FindButtonStyle(Enum styleType)
    {
        UIThemeButtonType buttonType;

        if (!TryGetEnumType(styleType, out buttonType))
        {
            Debug.LogWarning("UIThemeManager received invalid button type.");
            return null;
        }

        for (int i = 0; i < activeButtonStyles.Count; i++)
        {
            UIThemeButtonStyle style = activeButtonStyles[i];

            if (style == null)
            {
                continue;
            }

            if (style.ButtonType == buttonType)
            {
                return style.Style;
            }
        }

        UIThemeStyle defaultStyle = FindDefaultButtonStyle(buttonType);

        if (defaultStyle != null)
        {
            return defaultStyle;
        }

        Debug.LogWarning($"UIThemeManager could not find button style: {buttonType}");
        return null;
    }

    private UIThemeStyle FindTextStyle(Enum styleType)
    {
        UIThemeTextType textType;

        if (!TryGetEnumType(styleType, out textType))
        {
            Debug.LogWarning("UIThemeManager received invalid text type.");
            return null;
        }

        for (int i = 0; i < activeTextStyles.Count; i++)
        {
            UIThemeTextStyle style = activeTextStyles[i];

            if (style == null)
            {
                continue;
            }

            if (style.TextType == textType)
            {
                return style.Style;
            }
        }

        UIThemeStyle defaultStyle = FindDefaultTextStyle(textType);

        if (defaultStyle != null)
        {
            return defaultStyle;
        }

        Debug.LogWarning($"UIThemeManager could not find text style: {textType}");
        return null;
    }

    private UIThemeInputStyle FindInputStyle(Enum styleType)
    {
        UIThemeInputType inputType;

        if (!TryGetEnumType(styleType, out inputType))
        {
            Debug.LogWarning("UIThemeManager received invalid input type.");
            return null;
        }

        for (int i = 0; i < activeInputStyles.Count; i++)
        {
            UIThemeInputStyle style = activeInputStyles[i];

            if (style == null)
            {
                continue;
            }

            if (style.InputType == inputType)
            {
                return style;
            }
        }

        UIThemeInputStyle defaultStyle = FindDefaultInputStyle(inputType);

        if (defaultStyle != null)
        {
            return defaultStyle;
        }

        Debug.LogWarning($"UIThemeManager could not find input style: {inputType}");
        return null;
    }

    private UIThemeDropdownStyle FindDropdownStyle(Enum styleType)
    {
        UIThemeDropdownType dropdownType;

        if (!TryGetEnumType(styleType, out dropdownType))
        {
            Debug.LogWarning("UIThemeManager received invalid dropdown type.");
            return null;
        }

        for (int i = 0; i < activeDropdownStyles.Count; i++)
        {
            UIThemeDropdownStyle style = activeDropdownStyles[i];

            if (style == null)
            {
                continue;
            }

            if (style.DropdownType == dropdownType)
            {
                return style;
            }
        }

        UIThemeDropdownStyle defaultStyle = FindDefaultDropdownStyle(dropdownType);

        if (defaultStyle != null)
        {
            return defaultStyle;
        }

        Debug.LogWarning($"UIThemeManager could not find dropdown style: {dropdownType}");
        return null;
    }

    private UIThemeScrollStyle FindScrollStyle(Enum styleType)
    {
        UIThemeScrollType scrollType;

        if (!TryGetEnumType(styleType, out scrollType))
        {
            Debug.LogWarning("UIThemeManager received invalid scroll type.");
            return null;
        }

        for (int i = 0; i < activeScrollStyles.Count; i++)
        {
            UIThemeScrollStyle style = activeScrollStyles[i];

            if (style == null)
            {
                continue;
            }

            if (style.ScrollType == scrollType)
            {
                return style;
            }
        }

        UIThemeScrollStyle defaultStyle = FindDefaultScrollStyle(scrollType);

        if (defaultStyle != null)
        {
            return defaultStyle;
        }

        Debug.LogWarning($"UIThemeManager could not find scroll style: {scrollType}");
        return null;
    }

    private UIThemeSliderStyle FindSliderStyle(Enum styleType)
    {
        UIThemeSliderType sliderType;

        if (!TryGetEnumType(styleType, out sliderType))
        {
            Debug.LogWarning("UIThemeManager received invalid slider type.");
            return null;
        }

        for (int i = 0; i < activeSliderStyles.Count; i++)
        {
            UIThemeSliderStyle style = activeSliderStyles[i];

            if (style == null)
            {
                continue;
            }

            if (style.SliderType == sliderType)
            {
                return style;
            }
        }

        UIThemeSliderStyle defaultStyle = FindDefaultSliderStyle(sliderType);

        if (defaultStyle != null)
        {
            return defaultStyle;
        }

        Debug.LogWarning($"UIThemeManager could not find slider style: {sliderType}");
        return null;
    }

    private UIThemeToggleStyle FindToggleStyle(Enum styleType)
    {
        UIThemeToggleType toggleType;

        if (!TryGetEnumType(styleType, out toggleType))
        {
            Debug.LogWarning("UIThemeManager received invalid toggle type.");
            return null;
        }

        for (int i = 0; i < activeToggleStyles.Count; i++)
        {
            UIThemeToggleStyle style = activeToggleStyles[i];

            if (style == null)
            {
                continue;
            }

            if (style.ToggleType == toggleType)
            {
                return style;
            }
        }

        UIThemeToggleStyle defaultStyle = FindDefaultToggleStyle(toggleType);

        if (defaultStyle != null)
        {
            return defaultStyle;
        }

        Debug.LogWarning($"UIThemeManager could not find toggle style: {toggleType}");
        return null;
    }

    #endregion

    private bool TryGetEnumType<TEnum>(Enum styleType, out TEnum typedValue)
        where TEnum : struct, Enum
    {
        if (styleType is TEnum value)
        {
            typedValue = value;
            return true;
        }

        typedValue = default;
        return false;
    }

    public static void RefreshThemeData(UIThemeData changedThemeData)
    {
        if (instance == null || changedThemeData == null)
        {
            return;
        }

        if (instance.selectedThemeType != changedThemeData.ThemeType)
        {
            return;
        }

        instance.activeThemeData = changedThemeData;
        instance.RebuildActiveThemeLists();
        instance.ReapplyThemeToRegisteredTargets();
    }

    #region Find Default

    private UIThemeStyle FindDefaultBackgroundStyle(UIThemeBackgroundType backgroundType)
    {
        if (defaultThemeData == null)
        {
            return null;
        }

        for (int i = 0; i < defaultThemeData.BackgroundStyles.Count; i++)
        {
            UIThemeBackgroundStyle style = defaultThemeData.BackgroundStyles[i];

            if (style != null && style.BackgroundType == backgroundType)
            {
                return style.Style;
            }
        }

        return null;
    }

    private UIThemeStyle FindDefaultButtonStyle(UIThemeButtonType buttonType)
    {
        if (defaultThemeData == null)
        {
            return null;
        }

        for (int i = 0; i < defaultThemeData.ButtonStyles.Count; i++)
        {
            UIThemeButtonStyle style = defaultThemeData.ButtonStyles[i];

            if (style != null && style.ButtonType == buttonType)
            {
                return style.Style;
            }
        }

        return null;
    }

    private UIThemeStyle FindDefaultTextStyle(UIThemeTextType textType)
    {
        if (defaultThemeData == null)
        {
            return null;
        }

        for (int i = 0; i < defaultThemeData.TextStyles.Count; i++)
        {
            UIThemeTextStyle style = defaultThemeData.TextStyles[i];

            if (style != null && style.TextType == textType)
            {
                return style.Style;
            }
        }

        return null;
    }

    private UIThemeInputStyle FindDefaultInputStyle(UIThemeInputType inputType)
    {
        if (defaultThemeData == null)
        {
            return null;
        }

        for (int i = 0; i < defaultThemeData.InputStyles.Count; i++)
        {
            UIThemeInputStyle style = defaultThemeData.InputStyles[i];

            if (style != null && style.InputType == inputType)
            {
                return style;
            }
        }

        return null;
    }

    private UIThemeDropdownStyle FindDefaultDropdownStyle(UIThemeDropdownType dropdownType)
    {
        if (defaultThemeData == null)
        {
            return null;
        }

        for (int i = 0; i < defaultThemeData.DropdownStyles.Count; i++)
        {
            UIThemeDropdownStyle style = defaultThemeData.DropdownStyles[i];

            if (style != null && style.DropdownType == dropdownType)
            {
                return style;
            }
        }

        return null;
    }

    private UIThemeScrollStyle FindDefaultScrollStyle(UIThemeScrollType scrollType)
    {
        if (defaultThemeData == null)
        {
            return null;
        }

        for (int i = 0; i < defaultThemeData.ScrollStyles.Count; i++)
        {
            UIThemeScrollStyle style = defaultThemeData.ScrollStyles[i];

            if (style != null && style.ScrollType == scrollType)
            {
                return style;
            }
        }

        return null;
    }

    private UIThemeSliderStyle FindDefaultSliderStyle(UIThemeSliderType sliderType)
    {
        if (defaultThemeData == null)
        {
            return null;
        }

        for (int i = 0; i < defaultThemeData.SliderStyles.Count; i++)
        {
            UIThemeSliderStyle style = defaultThemeData.SliderStyles[i];

            if (style != null && style.SliderType == sliderType)
            {
                return style;
            }
        }

        return null;
    }

    private UIThemeToggleStyle FindDefaultToggleStyle(UIThemeToggleType toggleType)
    {
        if (defaultThemeData == null)
        {
            return null;
        }

        for (int i = 0; i < defaultThemeData.ToggleStyles.Count; i++)
        {
            UIThemeToggleStyle style = defaultThemeData.ToggleStyles[i];

            if (style != null && style.ToggleType == toggleType)
            {
                return style;
            }
        }

        return null;
    }

    #endregion

#if UNITY_EDITOR

    private void ReconnectActiveThemeTargetsInEditor()
    {
        if (Application.isPlaying)
        {
            return;
        }

        registeredTargets.Clear();

        MonoBehaviour[] sceneBehaviours = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < sceneBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = sceneBehaviours[i];

            if (behaviour == null)
            {
                continue;
            }

            if (PrefabUtility.IsPartOfPrefabAsset(behaviour))
            {
                continue;
            }

            if (behaviour is not IUIThemeTarget themeTarget)
            {
                continue;
            }

            if (!registeredTargets.Contains(themeTarget))
            {
                registeredTargets.Add(themeTarget);
            }

            themeTarget.ReapplyTheme();
        }
    }

#endif
}