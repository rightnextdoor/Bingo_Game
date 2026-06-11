using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIThemeDropdown : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeDropdownType dropdownType;

    [Header("Dropdown Root")]
    [SerializeField] private Image dropdownImage;
    [SerializeField] private Selectable dropdownSelectable;

    [Header("Label")]
    [SerializeField] private TMP_Text labelText;

    [Header("Arrow")]
    [SerializeField] private Image arrowImage;

    [Header("Template")]
    [SerializeField] private Image templateImage;

    [Header("Viewport")]
    [SerializeField] private Image viewportImage;

    [Header("Item")]
    [SerializeField] private Selectable itemSelectable;
    [SerializeField] private Image itemBackgroundImage;
    [SerializeField] private Image itemCheckmarkImage;
    [SerializeField] private TMP_Text itemLabelText;

    [Header("Scrollbar")]
    [SerializeField] private Image scrollbarImage;
    [SerializeField] private Scrollbar scrollbar;
    [SerializeField] private Image scrollbarHandleImage;

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

        UIThemeDropdownStyle style = UIThemeManager.Instance.ApplyTheme(
            UIThemeSectionType.Dropdown,
            dropdownType
        ) as UIThemeDropdownStyle;

        if (style == null)
        {
            return;
        }

        UIThemeApplier.ApplyImageStyle(dropdownImage, style.DropdownImage);
        UIThemeApplier.ApplySelectableVisualStyle(dropdownSelectable, style.DropdownVisual);

        UIThemeApplier.ApplyTextStyle(labelText, style.LabelText);

        UIThemeApplier.ApplyImageStyle(arrowImage, style.ArrowImage);

        UIThemeApplier.ApplyImageStyle(templateImage, style.TemplateImage);
        UIThemeApplier.ApplyImageStyle(viewportImage, style.ViewportImage);

        UIThemeApplier.ApplySelectableVisualStyle(itemSelectable, style.ItemVisual);
        UIThemeApplier.ApplyImageStyle(itemBackgroundImage, style.ItemBackgroundImage);
        UIThemeApplier.ApplyImageStyle(itemCheckmarkImage, style.ItemCheckmarkImage);
        UIThemeApplier.ApplyTextStyle(itemLabelText, style.ItemLabelText);

        UIThemeApplier.ApplyImageStyle(scrollbarImage, style.ScrollbarImage);
        UIThemeApplier.ApplySelectableVisualStyle(scrollbar, style.ScrollbarVisual);
        UIThemeApplier.ApplyImageStyle(scrollbarHandleImage, style.ScrollbarHandleImage);
    }
}