using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIThemeInput : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeInputType inputType;

    private UIThemeManager themeManager;

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

    public UIThemeInputType InputType => inputType;

    public void SetInputType(UIThemeInputType newInputType)
    {
        inputType = newInputType;
        ReapplyTheme();
    }

    public void ReapplyTheme()
    {
        if (!CacheThemeManager())
        {
            return;
        }

        UIThemeInputStyle style = themeManager.ApplyTheme(
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