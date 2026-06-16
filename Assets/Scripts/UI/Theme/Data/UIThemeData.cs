using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Bingo Game/UI/Theme Data", fileName = "NewUIThemeData")]
public class UIThemeData : ScriptableObject
{
    [SerializeField] private UIThemeType themeType = UIThemeType.Default;

    [Header("Background Section")]
    [SerializeField] private List<UIThemeBackgroundStyle> backgroundStyles = new();

    [Header("Button Section")]
    [SerializeField] private List<UIThemeButtonStyle> buttonStyles = new();

    [Header("Text Section")]
    [SerializeField] private List<UIThemeTextStyle> textStyles = new();

    [Header("Input Section")]
    [SerializeField] private List<UIThemeInputStyle> inputStyles = new();

    [Header("Dropdown Section")]
    [SerializeField] private List<UIThemeDropdownStyle> dropdownStyles = new();

    [Header("Scroll Section")]
    [SerializeField] private List<UIThemeScrollStyle> scrollStyles = new();

    [Header("Slider Section")]
    [SerializeField] private List<UIThemeSliderStyle> sliderStyles = new();

    [Header("Toggle Section")]
    [SerializeField] private List<UIThemeToggleStyle> toggleStyles = new();

    public UIThemeType ThemeType => themeType;

    public IReadOnlyList<UIThemeBackgroundStyle> BackgroundStyles => backgroundStyles;
    public IReadOnlyList<UIThemeButtonStyle> ButtonStyles => buttonStyles;
    public IReadOnlyList<UIThemeTextStyle> TextStyles => textStyles;
    public IReadOnlyList<UIThemeInputStyle> InputStyles => inputStyles;
    public IReadOnlyList<UIThemeDropdownStyle> DropdownStyles => dropdownStyles;
    public IReadOnlyList<UIThemeScrollStyle> ScrollStyles => scrollStyles;
    public IReadOnlyList<UIThemeSliderStyle> SliderStyles => sliderStyles;
    public IReadOnlyList<UIThemeToggleStyle> ToggleStyles => toggleStyles;

#if UNITY_EDITOR
    private void OnValidate()
    {
        UIThemeManager.RefreshThemeData(this);
    }
#endif
}