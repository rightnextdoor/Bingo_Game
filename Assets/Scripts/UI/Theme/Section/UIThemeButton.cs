using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIThemeButton : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeButtonType buttonType;

    private UIThemeManager themeManager;

    [Header("Components")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private Selectable selectable;
    [SerializeField] private TMP_Text buttonText;

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

    public UIThemeButtonType ButtonType => buttonType;

    public void SetButtonType(UIThemeButtonType newButtonType)
    {
        buttonType = newButtonType;
        ReapplyTheme();
    }

    public void ReapplyTheme()
    {
        if (!CacheThemeManager())
        {
            return;
        }

        UIThemeStyle style = themeManager.ApplyTheme(
            UIThemeSectionType.Button,
            buttonType
        ) as UIThemeStyle;

        UIThemeApplier.ApplyImageStyle(buttonImage, style);
        UIThemeApplier.ApplySelectableVisualStyle(selectable, style);
        UIThemeApplier.ApplyTextStyle(buttonText, style);
    }
}