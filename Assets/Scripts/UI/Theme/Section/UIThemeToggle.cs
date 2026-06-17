using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIThemeToggle : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeToggleType toggleType = UIThemeToggleType.RadioButton;

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

        UIThemeToggleStyle style = UIThemeManager.instance.ApplyTheme(
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