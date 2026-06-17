using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIThemeBackground : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeBackgroundType backgroundType;

    [Header("Components")]
    [SerializeField] private Image backgroundImage;
    private UIThemeManager themeManager;

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

        UIThemeStyle style = themeManager.ApplyTheme(
            UIThemeSectionType.Background,
            backgroundType
        ) as UIThemeStyle;

        UIThemeApplier.ApplyImageStyle(backgroundImage, style);
    }
}