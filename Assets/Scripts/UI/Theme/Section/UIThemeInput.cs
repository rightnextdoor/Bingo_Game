using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIThemeInput : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeInputType inputType;

    [Header("Field Components")]
    [SerializeField] private Image fieldImage;
    [SerializeField] private Selectable selectable;

    [Header("Text Components")]
    [SerializeField] private TMP_Text placeholderText;
    [SerializeField] private TMP_Text inputText;

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

        UIThemeInputStyle style = UIThemeManager.instance.ApplyTheme(
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