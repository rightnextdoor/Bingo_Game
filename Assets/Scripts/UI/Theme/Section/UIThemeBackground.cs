using UnityEngine;
using UnityEngine.UI;

public class UIThemeBackground : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeBackgroundType backgroundType;

    [Header("Components")]
    [SerializeField] private Image backgroundImage;

    private void OnEnable()
    {
        if (UIThemeManager.Instance == null)
        {
            return;
        }

        UIThemeManager.Instance.Register(this);
    }

    private void OnDisable()
    {
        if (UIThemeManager.Instance == null)
        {
            return;
        }

        UIThemeManager.Instance.Unregister(this);
    }

    public void ReapplyTheme()
    {
        if (UIThemeManager.Instance == null)
        {
            return;
        }

        UIThemeStyle style = UIThemeManager.Instance.ApplyTheme(
            UIThemeSectionType.Background,
            backgroundType
        ) as UIThemeStyle;

        UIThemeApplier.ApplyImageStyle(backgroundImage, style);
    }
}