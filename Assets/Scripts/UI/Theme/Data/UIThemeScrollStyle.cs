using System;
using UnityEngine;

[Serializable]
public class UIThemeScrollStyle
{
    [SerializeField] private UIThemeScrollType scrollType = UIThemeScrollType.Default;

    [Header("Root")]
    [SerializeField] private UIThemeStyle rootImage = new();

    [Header("Viewport")]
    [SerializeField] private UIThemeStyle viewportImage = new();

    [Header("Horizontal Scrollbar")]
    [SerializeField] private UIThemeStyle horizontalScrollbarImage = new();
    [SerializeField] private UIThemeStyle horizontalScrollbarVisual = new();
    [SerializeField] private UIThemeStyle horizontalHandleImage = new();

    [Header("Vertical Scrollbar")]
    [SerializeField] private UIThemeStyle verticalScrollbarImage = new();
    [SerializeField] private UIThemeStyle verticalScrollbarVisual = new();
    [SerializeField] private UIThemeStyle verticalHandleImage = new();

    public UIThemeSectionType SectionType => UIThemeSectionType.Scroll;
    public UIThemeScrollType ScrollType => scrollType;

    public UIThemeStyle RootImage => rootImage;

    public UIThemeStyle ViewportImage => viewportImage;

    public UIThemeStyle HorizontalScrollbarImage => horizontalScrollbarImage;
    public UIThemeStyle HorizontalScrollbarVisual => horizontalScrollbarVisual;
    public UIThemeStyle HorizontalHandleImage => horizontalHandleImage;

    public UIThemeStyle VerticalScrollbarImage => verticalScrollbarImage;
    public UIThemeStyle VerticalScrollbarVisual => verticalScrollbarVisual;
    public UIThemeStyle VerticalHandleImage => verticalHandleImage;
}