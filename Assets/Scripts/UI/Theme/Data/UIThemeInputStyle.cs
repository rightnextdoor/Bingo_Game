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

    [Space]
    [Header("Text Selection")]
    [Tooltip("Highlight color behind selected input text. Alpha controls transparency.")]
    [SerializeField] private Color selectionColor = new Color(0.2f, 0.6f, 1f, 0.5f);

    [Header("Placeholder")]
    [SerializeField] private UIThemeStyle placeholderText = new();

    public UIThemeSectionType SectionType => UIThemeSectionType.Input;
    public UIThemeInputType InputType => inputType;

    public UIThemeStyle FieldImage => fieldImage;
    public UIThemeStyle FieldVisual => fieldVisual;

    public UIThemeStyle InputText => inputText;

    public Color SelectionColor => selectionColor;

    public UIThemeStyle PlaceholderText => placeholderText;
}
