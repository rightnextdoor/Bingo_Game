using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIThemeSlider : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeSliderType sliderType;

    [Header("Slider Root")]
    [SerializeField] private Slider slider;

    [Header("Images")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image handleImage;

    private void OnEnable()
    {
        RegisterWithManager();
    }

    private void Start()
    {
        RegisterWithManager();
    }

    private void OnDisable()
    {
        if (UIThemeManager.instance == null)
        {
            return;
        }

        UIThemeManager.instance.Unregister(this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RegisterWithManager();
    }
#endif

    private void RegisterWithManager()
    {
        if (UIThemeManager.instance == null)
        {
            return;
        }

        UIThemeManager.instance.Register(this);
    }

    public void ReapplyTheme()
    {
        if (UIThemeManager.instance == null)
        {
            return;
        }

        UIThemeSliderStyle style = UIThemeManager.instance.ApplyTheme(
            UIThemeSectionType.Slider,
            sliderType
        ) as UIThemeSliderStyle;

        if (style == null)
        {
            return;
        }

        UIThemeApplier.ApplySelectableVisualStyle(slider, style.SliderVisual);
        UIThemeApplier.ApplyImageStyle(backgroundImage, style.BackgroundImage);
        UIThemeApplier.ApplyImageStyle(fillImage, style.FillImage);
        UIThemeApplier.ApplyImageStyle(handleImage, style.HandleImage);
    }
}