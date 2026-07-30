using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainMenuPatternInfoItem : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    #region Inspector Fields

    [Header("Components")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject highlightObject;
    [SerializeField] private TMP_Text patternNameText;

    [Header("Tooltip Theme")]
    [SerializeField] private UIThemeBackgroundType tooltipBackgroundType = UIThemeBackgroundType.PatternTooltip;
    [SerializeField] private UIThemeTextType tooltipTextType = UIThemeTextType.PatternTooltip;

    #endregion

    #region Private Fields

    private BingoPatternType patternType;
    private RectTransform rectTransform;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        CacheReferences();
        SetHighlighted(false);
    }

    private void OnDisable()
    {
        SetHighlighted(false);
        HideToolTip();
    }

    #endregion

    #region Setup

    public void Setup(BingoPatternType newPatternType)
    {
        CacheReferences();

        patternType = newPatternType;

        if (patternNameText != null)
        {
            patternNameText.text = patternType.ToString();
            patternNameText.raycastTarget = false;
        }

        if (backgroundImage != null)
        {
            backgroundImage.raycastTarget = true;
        }

        SetHighlighted(false);
    }

    #endregion

    #region Pointer Events

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHighlighted(true);
        ShowPatternToolTip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHighlighted(false);
        HideToolTip();
    }

    #endregion

    #region Tooltip

    private void ShowPatternToolTip()
    {
        if (ToolTipManager.instance == null ||
            GameModeManager.instance == null ||
            rectTransform == null)
        {
            return;
        }

        BingoPatternData patternData =
            GameModeManager.instance.GetBingoPatternData(patternType);

        if (patternData == null)
        {
            return;
        }

        string description = patternData.Description;

        if (string.IsNullOrWhiteSpace(description))
        {
            description = patternType.ToString();
        }

        TooltipVisualStyle visualStyle =
            new TooltipVisualStyle()
                .SetMessage(description)
                .RemoveImage();

        ApplyTooltipTheme(visualStyle);

        ToolTipManager.instance.ShowToolTip(
            visualStyle,
            rectTransform
        );
    }

    private void HideToolTip()
    {
        if (ToolTipManager.instance != null)
        {
            ToolTipManager.instance.HideToolTip();
        }
    }

    private void ApplyTooltipTheme(
    TooltipVisualStyle visualStyle)
    {
        if (visualStyle == null ||
            UIThemeManager.instance == null)
        {
            return;
        }

        UIThemeStyle backgroundStyle =
            UIThemeManager.instance.GetBackgroundStyle(
                tooltipBackgroundType
            );

        if (backgroundStyle != null)
        {
            visualStyle.SetBackgroundColor(
                backgroundStyle.Color
            );
        }

        UIThemeStyle textStyle =
            UIThemeManager.instance.GetTextStyle(
                tooltipTextType
            );

        if (textStyle != null)
        {
            visualStyle
                .SetFont(textStyle.FontAsset)
                .SetTextColor(textStyle.VertexColor)
                .SetNumberColor(textStyle.VertexColor);
        }
    }

    #endregion

    #region Helpers

    private void CacheReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (patternNameText == null)
        {
            patternNameText =
                GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void SetHighlighted(bool isHighlighted)
    {
        if (highlightObject != null)
        {
            highlightObject.SetActive(isHighlighted);
        }
    }

    #endregion
}