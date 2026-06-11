using System;
using UnityEngine;

[Serializable]
public class UIThemeBackgroundStyle
{
    [SerializeField] private UIThemeBackgroundType backgroundType;
    [SerializeField] private UIThemeStyle style = new();

    public UIThemeSectionType SectionType => UIThemeSectionType.Background;
    public UIThemeBackgroundType BackgroundType => backgroundType;
    public UIThemeStyle Style => style;
}