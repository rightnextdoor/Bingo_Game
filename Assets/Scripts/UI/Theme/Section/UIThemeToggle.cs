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
        if (UIThemeManager.Instance == null)
        {
            return;
        }

        UIThemeManager.Instance.Unregister(this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RegisterWithManager();
    }
#endif

    private void RegisterWithManager()
    {
        if (UIThemeManager.Instance == null)
        {
            return;
        }

        UIThemeManager.Instance.Register(this);
    }

    public void ReapplyTheme()
    {
        if (UIThemeManager.Instance == null)
        {
            return;
        }

        UIThemeToggleStyle style = UIThemeManager.Instance.ApplyTheme(
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