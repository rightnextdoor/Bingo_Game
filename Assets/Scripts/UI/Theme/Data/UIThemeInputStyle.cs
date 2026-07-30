using System;
using UnityEngine;

[Serializable]
public class UIThemeInputStyle
{
    [SerializeField] private UIThemeInputType inputType = UIThemeInputType.Default;

    [Header("Field")]
    [SerializeField] private UIThemeStyle fieldImage = new();
    [SerializeField] private UIThemeStyle fieldVisual = new();

    [Header("Text")]
    [SerializeField] private UIThemeStyle inputText = new();

    [Header("Placeholder")]
    [SerializeField] private UIThemeStyle placeholderText = new();

    public UIThemeSectionType SectionType => UIThemeSectionType.Input;
    public UIThemeInputType InputType => inputType;

    public UIThemeStyle FieldImage => fieldImage;
    public UIThemeStyle FieldVisual => fieldVisual;

    public UIThemeStyle InputText => inputText;

    public UIThemeStyle PlaceholderText => placeholderText;
}