using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIThemeButton : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeButtonType buttonType;

    [Header("Components")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private Selectable selectable;
    [SerializeField] private TMP_Text buttonText;

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

        UIThemeStyle style = UIThemeManager.Instance.ApplyTheme(
            UIThemeSectionType.Button,
            buttonType
        ) as UIThemeStyle;

        UIThemeApplier.ApplyImageStyle(buttonImage, style);
        UIThemeApplier.ApplySelectableVisualStyle(selectable, style);
        UIThemeApplier.ApplyTextStyle(buttonText, style);
    }
}