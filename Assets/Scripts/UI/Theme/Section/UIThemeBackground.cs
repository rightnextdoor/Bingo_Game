using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIThemeBackground : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeBackgroundType backgroundType;

    [Header("Components")]
    [SerializeField] private Image backgroundImage;

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
            UIThemeSectionType.Background,
            backgroundType
        ) as UIThemeStyle;

        UIThemeApplier.ApplyImageStyle(backgroundImage, style);
    }
}