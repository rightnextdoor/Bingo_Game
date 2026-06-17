using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIThemeToggle : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeToggleType toggleType = UIThemeToggleType.RadioButton;

    private UIThemeManager themeManager;

    [Header("Toggle Root")]
    [SerializeField] private Toggle toggle;

    [Header("Images")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image checkmarkImage;

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

        UIThemeToggleStyle style = themeManager.ApplyTheme(
            UIThemeSectionType.Toggle,
            toggleType
        ) as UIThemeToggleStyle;

        if (style == null)
        {
            return;
        }

        UIThemeApplier.ApplySelectableVisualStyle(toggle, style.ToggleVisual);
        UIThemeApplier.ApplyImageStyle(backgroundImage, style.BackgroundImage);
        UIThemeApplier.ApplyImageStyle(checkmarkImage, style.CheckmarkImage);
    }
}