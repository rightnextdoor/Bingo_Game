using TMPro;
using UnityEngine;

[ExecuteAlways]
public class UIThemeText : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeTextType textType;

    private UIThemeManager themeManager;
    private bool hasColorOverride;
    private Color colorOverride;

    [Header("Components")]
    [SerializeField] private TMP_Text text;

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

    public UIThemeTextType TextType => textType;

    public void SetTextType(UIThemeTextType newTextType)
    {
        textType = newTextType;
        ReapplyTheme();
    }

    public void SetColorOverride(Color color)
    {
        hasColorOverride = true;
        colorOverride = color;
        ApplyColorOverride();
    }

    public void ClearColorOverride()
    {
        if (!hasColorOverride)
        {
            return;
        }

        hasColorOverride = false;
        ReapplyTheme();
    }

    public void ReapplyTheme()
    {
        if (!CacheThemeManager())
        {
            return;
        }

        UIThemeStyle style = themeManager.ApplyTheme(
            UIThemeSectionType.Text,
            textType
        ) as UIThemeStyle;

        UIThemeApplier.ApplyTextStyle(text, style);
        ApplyColorOverride();
    }

    private void ApplyColorOverride()
    {
        if (!hasColorOverride || text == null)
        {
            return;
        }

        Color finalColor = colorOverride;
        finalColor.a = 1f;
        text.color = finalColor;
    }
}