using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIThemeButton : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeButtonType buttonType;

    [Header("Components")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private Selectable selectable;
    [SerializeField] private TMP_Text buttonText;

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
            UIThemeSectionType.Button,
            buttonType
        ) as UIThemeStyle;

        UIThemeApplier.ApplyImageStyle(buttonImage, style);
        UIThemeApplier.ApplySelectableVisualStyle(selectable, style);
        UIThemeApplier.ApplyTextStyle(buttonText, style);
    }
}