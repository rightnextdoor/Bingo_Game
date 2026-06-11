using System;
using UnityEngine;

[Serializable]
public class UIThemeDropdownStyle
{
    [SerializeField] private UIThemeDropdownType dropdownType = UIThemeDropdownType.Default;

    [Header("Dropdown Root")]
    [SerializeField] private UIThemeStyle dropdownImage = new();
    [SerializeField] private UIThemeStyle dropdownVisual = new();

    [Header("Label")]
    [SerializeField] private UIThemeStyle labelText = new();

    [Header("Arrow")]
    [SerializeField] private UIThemeStyle arrowImage = new();

    [Header("Template")]
    [SerializeField] private UIThemeStyle templateImage = new();

    [Header("Viewport")]
    [SerializeField] private UIThemeStyle viewportImage = new();

    [Header("Item")]
    [SerializeField] private UIThemeStyle itemVisual = new();
    [SerializeField] private UIThemeStyle itemBackgroundImage = new();
    [SerializeField] private UIThemeStyle itemCheckmarkImage = new();
    [SerializeField] private UIThemeStyle itemLabelText = new();

    [Header("Scrollbar")]
    [SerializeField] private UIThemeStyle scrollbarImage = new();
    [SerializeField] private UIThemeStyle scrollbarVisual = new();
    [SerializeField] private UIThemeStyle scrollbarHandleImage = new();

    public UIThemeSectionType SectionType => UIThemeSectionType.Dropdown;
    public UIThemeDropdownType DropdownType => dropdownType;

    public UIThemeStyle DropdownImage => dropdownImage;
    public UIThemeStyle DropdownVisual => dropdownVisual;

    public UIThemeStyle LabelText => labelText;

    public UIThemeStyle ArrowImage => arrowImage;

    public UIThemeStyle TemplateImage => templateImage;

    public UIThemeStyle ViewportImage => viewportImage;

    public UIThemeStyle ItemVisual => itemVisual;
    public UIThemeStyle ItemBackgroundImage => itemBackgroundImage;
    public UIThemeStyle ItemCheckmarkImage => itemCheckmarkImage;
    public UIThemeStyle ItemLabelText => itemLabelText;

    public UIThemeStyle ScrollbarImage => scrollbarImage;
    public UIThemeStyle ScrollbarVisual => scrollbarVisual;
    public UIThemeStyle ScrollbarHandleImage => scrollbarHandleImage;
}