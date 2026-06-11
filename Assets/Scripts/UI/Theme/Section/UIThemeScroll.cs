using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIThemeScroll : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeScrollType scrollType;

    [Header("Root")]
    [SerializeField] private Image rootImage;

    [Header("Viewport")]
    [SerializeField] private Image viewportImage;

    [Header("Horizontal Scrollbar")]
    [SerializeField] private Image horizontalScrollbarImage;
    [SerializeField] private Scrollbar horizontalScrollbar;
    [SerializeField] private Image horizontalHandleImage;

    [Header("Vertical Scrollbar")]
    [SerializeField] private Image verticalScrollbarImage;
    [SerializeField] private Scrollbar verticalScrollbar;
    [SerializeField] private Image verticalHandleImage;

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

        UIThemeScrollStyle style = UIThemeManager.Instance.ApplyTheme(
            UIThemeSectionType.Scroll,
            scrollType
        ) as UIThemeScrollStyle;

        if (style == null)
        {
            return;
        }

        UIThemeApplier.ApplyImageStyle(rootImage, style.RootImage);
        UIThemeApplier.ApplyImageStyle(viewportImage, style.ViewportImage);

        UIThemeApplier.ApplyImageStyle(horizontalScrollbarImage, style.HorizontalScrollbarImage);
        UIThemeApplier.ApplySelectableVisualStyle(horizontalScrollbar, style.HorizontalScrollbarVisual);
        UIThemeApplier.ApplyImageStyle(horizontalHandleImage, style.HorizontalHandleImage);

        UIThemeApplier.ApplyImageStyle(verticalScrollbarImage, style.VerticalScrollbarImage);
        UIThemeApplier.ApplySelectableVisualStyle(verticalScrollbar, style.VerticalScrollbarVisual);
        UIThemeApplier.ApplyImageStyle(verticalHandleImage, style.VerticalHandleImage);
    }
}