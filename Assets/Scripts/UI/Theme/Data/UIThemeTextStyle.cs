using System;
using UnityEngine;

[Serializable]
public class UIThemeTextStyle
{
    [SerializeField] private UIThemeTextType textType;
    [SerializeField] private UIThemeStyle style = new();

    public UIThemeSectionType SectionType => UIThemeSectionType.Text;
    public UIThemeTextType TextType => textType;
    public UIThemeStyle Style => style;
}