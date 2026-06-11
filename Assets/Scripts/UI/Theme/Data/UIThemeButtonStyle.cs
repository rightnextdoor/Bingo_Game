using System;
using UnityEngine;

[Serializable]
public class UIThemeButtonStyle
{
    [SerializeField] private UIThemeButtonType buttonType;
    [SerializeField] private UIThemeStyle style = new();

    public UIThemeSectionType SectionType => UIThemeSectionType.Button;
    public UIThemeButtonType ButtonType => buttonType;
    public UIThemeStyle Style => style;
}