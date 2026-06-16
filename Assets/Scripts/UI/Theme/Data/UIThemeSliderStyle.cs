using System;
using UnityEngine;

[Serializable]
public class UIThemeSliderStyle
{
    [SerializeField] private UIThemeSliderType sliderType = UIThemeSliderType.Default;

    [Header("Slider Root")]
    [SerializeField] private UIThemeStyle sliderVisual = new();

    [Header("Background")]
    [SerializeField] private UIThemeStyle backgroundImage = new();

    [Header("Fill")]
    [SerializeField] private UIThemeStyle fillImage = new();

    [Header("Handle")]
    [SerializeField] private UIThemeStyle handleImage = new();

    public UIThemeSectionType SectionType => UIThemeSectionType.Slider;
    public UIThemeSliderType SliderType => sliderType;

    public UIThemeStyle SliderVisual => sliderVisual;
    public UIThemeStyle BackgroundImage => backgroundImage;
    public UIThemeStyle FillImage => fillImage;
    public UIThemeStyle HandleImage => handleImage;
}