using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Bingo Game/UI/Theme Data", fileName = "NewUIThemeData")]
public class UIThemeData : ScriptableObject
{
    [SerializeField] private UIThemeType themeType = UIThemeType.Default;

    [Header("Background Section")]
    [SerializeField] private UIThemeStyle mainBackground = new();
    [SerializeField] private UIThemeStyle headerBackground = new();
    [SerializeField] private UIThemeStyle secondaryBackground = new();

    [Header("Button Section")]
    [SerializeField] private UIThemeStyle primaryButton = new();
    [SerializeField] private UIThemeStyle secondaryButton = new();
    [SerializeField] private UIThemeStyle closeButton = new();

    [Header("Text Section")]
    [SerializeField] private UIThemeStyle titleText = new();
    [SerializeField] private UIThemeStyle subtitleText = new();

    [Header("Input Section")]
    [SerializeField] private UIThemeInputStyle input = new();

    [Header("Dropdown Section")]
    [SerializeField] private UIThemeDropdownStyle dropdown = new();

    [Header("Scroll Section")]
    [SerializeField] private UIThemeScrollStyle scroll = new();

    public UIThemeType ThemeType => themeType;

    public UIThemeStyle MainBackground => mainBackground;
    public UIThemeStyle HeaderBackground => headerBackground;
    public UIThemeStyle SecondaryBackground => secondaryBackground;

    public UIThemeStyle PrimaryButton => primaryButton;
    public UIThemeStyle SecondaryButton => secondaryButton;
    public UIThemeStyle CloseButton => closeButton;

    public UIThemeStyle TitleText => titleText;
    public UIThemeStyle SubtitleText => subtitleText;

    public UIThemeInputStyle Input => input;

    public UIThemeDropdownStyle Dropdown => dropdown;
    public UIThemeScrollStyle Scroll => scroll;
}