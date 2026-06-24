using System;
using UnityEngine;

[Serializable]
public class UIThemeToggleStyle
{
    [SerializeField] private UIThemeToggleType toggleType = UIThemeToggleType.RadioButton;

    [Header("Toggle Root")]
    [SerializeField] private UIThemeStyle toggleVisual = new();

    [Header("Background")]
    [SerializeField] private UIThemeStyle backgroundImage = new();

    [Header("Checkmark")]
    [SerializeField] private UIThemeStyle checkmarkImage = new();

    [Header("Label")]
    [SerializeField] private UIThemeStyle labelText = new();

    public UIThemeSectionType SectionType => UIThemeSectionType.Toggle;
    public UIThemeToggleType ToggleType => toggleType;

    public UIThemeStyle ToggleVisual => toggleVisual;
    public UIThemeStyle BackgroundImage => backgroundImage;
    public UIThemeStyle CheckmarkImage => checkmarkImage;

    public UIThemeStyle LabelText => labelText;
}