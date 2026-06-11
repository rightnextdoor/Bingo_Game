using TMPro;
using UnityEngine;

public class UIThemeText : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeTextType textType;

    [Header("Components")]
    [SerializeField] private TMP_Text text;

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
            UIThemeSectionType.Text,
            textType
        ) as UIThemeStyle;

        UIThemeApplier.ApplyTextStyle(text, style);
    }
}