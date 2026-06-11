using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIThemeInput : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeInputType inputType;

    [Header("Field Components")]
    [SerializeField] private Image fieldImage;
    [SerializeField] private Selectable selectable;

    [Header("Text Components")]
    [SerializeField] private TMP_Text inputText;
    [SerializeField] private TMP_Text placeholderText;

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

        UIThemeInputStyle style = UIThemeManager.Instance.ApplyTheme(
            UIThemeSectionType.Input,
            inputType
        ) as UIThemeInputStyle;

        if (style == null)
        {
            return;
        }

        UIThemeApplier.ApplyImageStyle(fieldImage, style.FieldImage);
        UIThemeApplier.ApplySelectableVisualStyle(selectable, style.FieldVisual);
        UIThemeApplier.ApplyTextStyle(inputText, style.InputText);
        UIThemeApplier.ApplyTextStyle(placeholderText, style.PlaceholderText);
    }
}