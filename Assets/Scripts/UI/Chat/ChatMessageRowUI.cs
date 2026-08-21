using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatMessageRowUI : MonoBehaviour
{
    #region Fields

    [Header("Theme")]
    [SerializeField] private UIThemeBackground backgroundTheme;
    [SerializeField] private UIThemeText messageTheme;

    [Header("Display")]
    [SerializeField] private Image playerIconImage;
    [SerializeField] private TMP_Text messageText;

    private RectTransform rowRect;
    private RectTransform iconRect;
    private HorizontalLayoutGroup horizontalLayoutGroup;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        rowRect = transform as RectTransform;
        iconRect = playerIconImage != null ? playerIconImage.rectTransform : null;
        horizontalLayoutGroup = GetComponent<HorizontalLayoutGroup>();

        Clear();
    }

    #endregion

    #region Display

    public void Setup(
        Sprite playerIcon,
        string displayText,
        float textSize,
        UIThemeBackgroundType backgroundType,
        UIThemeTextType textType,
        bool useColorOverride,
        Color colorOverride,
        bool showIcon)
    {
        Clear();

        backgroundTheme?.SetBackgroundType(backgroundType);

        if (messageTheme != null)
        {
            messageTheme.SetTextType(textType);

            if (useColorOverride)
            {
                messageTheme.SetColorOverride(colorOverride);
            }
        }

        if (messageText != null)
        {
            messageText.fontSize = Mathf.Max(1f, textSize);
            messageText.text = displayText ?? string.Empty;

            if (messageTheme == null && useColorOverride)
            {
                Color finalColor = colorOverride;
                finalColor.a = 1f;
                messageText.color = finalColor;
            }
        }

        if (playerIconImage != null)
        {
            bool shouldShowIcon = showIcon && playerIcon != null;

            playerIconImage.sprite = shouldShowIcon ? playerIcon : null;
            playerIconImage.preserveAspect = true;
            playerIconImage.gameObject.SetActive(shouldShowIcon);
        }
    }

    public void Clear()
    {
        if (messageTheme != null)
        {
            messageTheme.ClearColorOverride();
        }

        if (messageText != null)
        {
            messageText.text = string.Empty;
        }

        if (playerIconImage != null)
        {
            playerIconImage.sprite = null;
            playerIconImage.gameObject.SetActive(false);
        }
    }

    #endregion

    #region Size

    public float GetPreferredHeight(float rowWidth)
    {
        if (rowRect == null || messageText == null)
        {
            return 0f;
        }

        rowWidth = Mathf.Max(1f, rowWidth);
        rowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rowWidth);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);

        float layoutHeight = LayoutUtility.GetPreferredHeight(rowRect);

        if (horizontalLayoutGroup != null)
        {
            float availableTextWidth = GetHorizontalLayoutTextWidth(rowWidth);
            float textHeight = messageText.GetPreferredValues(messageText.text, availableTextWidth, 0f).y;
            float iconHeight = GetVisibleIconHeight();
            float contentHeight = Mathf.Max(textHeight, iconHeight);

            return Mathf.Max(1f, contentHeight + horizontalLayoutGroup.padding.vertical);
        }

        float textWidth = GetCurrentTextWidth(rowWidth);
        float preferredTextHeight = messageText.GetPreferredValues(messageText.text, textWidth, 0f).y;
        float currentTextHeight = Mathf.Max(0f, messageText.rectTransform.rect.height);
        float currentRowHeight = Mathf.Max(0f, rowRect.rect.height);
        float verticalChrome = Mathf.Max(0f, currentRowHeight - currentTextHeight);
        float preferredHeight = preferredTextHeight + verticalChrome;

        preferredHeight = Mathf.Max(preferredHeight, GetVisibleIconHeight() + verticalChrome);

        if (layoutHeight > 0f)
        {
            preferredHeight = Mathf.Max(preferredHeight, layoutHeight);
        }

        return Mathf.Max(1f, preferredHeight);
    }

    private float GetHorizontalLayoutTextWidth(float rowWidth)
    {
        float width = rowWidth - horizontalLayoutGroup.padding.horizontal;

        if (playerIconImage != null && playerIconImage.gameObject.activeSelf)
        {
            float iconWidth = iconRect != null ? LayoutUtility.GetPreferredWidth(iconRect) : 0f;

            if (iconWidth <= 0f && iconRect != null)
            {
                iconWidth = iconRect.rect.width;
            }

            width -= Mathf.Max(0f, iconWidth);
            width -= horizontalLayoutGroup.spacing;
        }

        return Mathf.Max(1f, width);
    }

    private float GetCurrentTextWidth(float rowWidth)
    {
        RectTransform textRect = messageText.rectTransform;
        float width = textRect.rect.width;

        if (width > 1f)
        {
            return width;
        }

        if (textRect.parent == rowRect)
        {
            float anchorWidth = rowWidth * (textRect.anchorMax.x - textRect.anchorMin.x);
            width = anchorWidth + textRect.sizeDelta.x;
        }

        return Mathf.Max(1f, width);
    }

    private float GetVisibleIconHeight()
    {
        if (playerIconImage == null || !playerIconImage.gameObject.activeSelf || iconRect == null)
        {
            return 0f;
        }

        float height = LayoutUtility.GetPreferredHeight(iconRect);

        if (height <= 0f)
        {
            height = iconRect.rect.height;
        }

        return Mathf.Max(0f, height);
    }

    #endregion
}
