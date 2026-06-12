using TMPro;
using UnityEngine;

[ExecuteAlways]
public class UIThemeText : MonoBehaviour, IUIThemeTarget
{
    [SerializeField] private UIThemeTextType textType;

    [Header("Components")]
    [SerializeField] private TMP_Text text;

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

    public void SetTextType(UIThemeTextType newTextType)
    {
        textType = newTextType;
        ReapplyTheme();
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