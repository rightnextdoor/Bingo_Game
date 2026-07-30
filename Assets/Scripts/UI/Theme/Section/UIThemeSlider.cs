using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIThemeSlider : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeSliderType sliderType;

    private UIThemeManager themeManager;

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
        if (themeManager != null)
        {
            themeManager.Unregister(this);
            themeManager = null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RegisterWithManager();
    }
#endif

    private void RegisterWithManager()
    {
        if (!CacheThemeManager())
        {
            return;
        }

        themeManager.Register(this);
    }

    private bool CacheThemeManager()
    {
        if (themeManager == null)
        {
            themeManager = UIThemeManager.instance;
        }

        return themeManager != null;
    }

    public void ReapplyTheme()
    {
        if (!CacheThemeManager())
        {
            return;
        }

        UIThemeSliderStyle style = themeManager.ApplyTheme(
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