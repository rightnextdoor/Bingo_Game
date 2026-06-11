using System;
using System.Collections.Generic;
using UnityEngine;

public class UIThemeManager : MonoBehaviour
{
    public static UIThemeManager Instance { get; private set; }

    [Header("Theme Selection")]
    [SerializeField] private UIThemeType selectedThemeType = UIThemeType.Default;

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

    public UIThemeType SelectedThemeType => selectedThemeType;

    private void Awake()
    {
        Instance = this;

        SetTheme(selectedThemeType);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Instance != this)
        {
            return;
        }

        SetTheme(selectedThemeType);
    }
#endif

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

            default:
                Debug.LogWarning($"UIThemeManager does not support section type: {sectionType}");
                return null;
        }
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
    }

    private void ClearActiveThemeLists()
    {
        activeBackgroundStyles.Clear();
        activeButtonStyles.Clear();
        activeTextStyles.Clear();
        activeInputStyles.Clear();
        activeDropdownStyles.Clear();
        activeScrollStyles.Clear();
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

        Debug.LogWarning($"UIThemeManager could not find scroll style: {scrollType}");
        return null;
    }

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
}