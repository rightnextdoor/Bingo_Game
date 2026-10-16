using System.Globalization;
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
    [SerializeField] private UIThemeText playerNameTheme;

    [Header("Inline Identity")]
    [SerializeField] private RectTransform nameArea;
    [SerializeField] private TMP_Text playerNameText;

    [Header("Display")]
    [SerializeField] private Image playerIconImage;
    [SerializeField] private TMP_Text messageText;

    public float MessageFontSize => messageText != null ? messageText.fontSize : 0f;

    private RectTransform rowRect;
    private RectTransform iconRect;
    private RectTransform iconContainer;
    private bool referencesCached;
    private bool hasIdentity;
    private int identityStart;
    private int identityEnd;
    [SerializeField, Min(0f), Tooltip("Space after the first-line icon, in UI units.")] private float iconTextSpacing = 8f;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        if (referencesCached) return;
        rowRect = transform as RectTransform;
        iconRect = playerIconImage != null ? playerIconImage.rectTransform : null;
        iconContainer = iconRect != null ? iconRect.parent as RectTransform : null;
        if (playerNameText != null) playerNameText.OnPreRenderText += RenderIdentity;
        if (messageText != null) messageText.OnPreRenderText += RenderMessage;
        referencesCached = true;
    }

    private void OnDestroy()
    {
        if (playerNameText != null) playerNameText.OnPreRenderText -= RenderIdentity;
        if (messageText != null) messageText.OnPreRenderText -= RenderMessage;
    }

    private void SetInlineText(string _identity, string _message)
    {
        if (messageText == null) return;
        if (!hasIdentity || playerNameText == null)
        {
            identityStart = 0;
            identityEnd = 0;
            messageText.text = _message ?? string.Empty;
            return;
        }

        float firstLineInset = playerIconImage != null && playerIconImage.gameObject.activeSelf && iconRect != null
            ? iconRect.rect.width + iconTextSpacing : 0f;
        string prefix = "<pos=" + firstLineInset.ToString("0.###", CultureInfo.InvariantCulture) + "><size=" +
            playerNameText.fontSize.ToString("0.###", CultureInfo.InvariantCulture) + "><noparse>";
        string identity = _identity + ":";
        identityStart = prefix.Length;
        identityEnd = identityStart + identity.Length;
        string flow = prefix + identity + "</noparse></size><size=" +
            messageText.fontSize.ToString("0.###", CultureInfo.InvariantCulture) + "> " +
            (_message ?? string.Empty) + "</size>";
        playerNameText.text = flow;
        messageText.text = flow;
    }

    private void PrepareRuntimeTextRects()
    {
        if (playerNameText != null) StretchTextRect(playerNameText.rectTransform);
        if (messageText != null) StretchTextRect(messageText.rectTransform);
    }

    private static void StretchTextRect(RectTransform _rect)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.pivot = new Vector2(0f, 1f);
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;
    }

    private void RenderIdentity(TMP_TextInfo _textInfo)
    {
        if (hasIdentity && _textInfo.lineCount > 0 && iconContainer != null && iconContainer.parent == nameArea)
        {
            TMP_LineInfo firstLine = _textInfo.lineInfo[0];
            float centerY = (firstLine.ascender + firstLine.descender) * 0.5f;
            Vector3 worldCenter = playerNameText.rectTransform.TransformPoint(new Vector3(0f, centerY, 0f));
            Vector3 localCenter = nameArea.InverseTransformPoint(worldCenter);
            Vector3 position = iconContainer.localPosition;
            position.y = localCenter.y + (iconContainer.pivot.y - 0.5f) * iconContainer.rect.height;
            iconContainer.localPosition = position;
        }
        SetSpanVisibility(_textInfo, true);
    }

    private void RenderMessage(TMP_TextInfo _textInfo)
    {
        SetSpanVisibility(_textInfo, false);
    }

    private void SetSpanVisibility(TMP_TextInfo _textInfo, bool _identityOnly)
    {
        if (!hasIdentity || playerNameText == null) return;
        for (int i = 0; i < _textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = _textInfo.characterInfo[i];
            if (!character.isVisible) continue;
            bool identityCharacter = character.index >= identityStart && character.index < identityEnd;
            if (identityCharacter == _identityOnly) continue;
            Color32[] colors = _textInfo.meshInfo[character.materialReferenceIndex].colors32;
            for (int vertex = character.vertexIndex; vertex < character.vertexIndex + 4; vertex++)
                colors[vertex].a = 0;
        }
    }

    #endregion

    #region Display

    public void Setup(
        Sprite playerIcon,
        string displayText,
        UIThemeBackgroundType backgroundType,
        UIThemeTextType textType,
        bool useColorOverride,
        Color colorOverride,
        bool showIcon,
        string playerIdentity = null)
    {
        CacheReferences();
        Clear();
        PrepareRuntimeTextRects();

        backgroundTheme?.SetBackgroundType(backgroundType);
        hasIdentity = !string.IsNullOrWhiteSpace(playerIdentity);
        if (nameArea != null) nameArea.gameObject.SetActive(hasIdentity);
        if (playerNameTheme != null)
        {
            playerNameTheme.SetTextType(textType);
            if (useColorOverride) playerNameTheme.SetColorOverride(colorOverride);
        }
        if (playerNameText != null)
        {
            playerNameText.text = hasIdentity ? playerIdentity + ":" : string.Empty;
            if (playerNameTheme == null && useColorOverride)
            {
                Color nameColor = colorOverride;
                nameColor.a = 1f;
                playerNameText.color = nameColor;
            }
        }
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
        SetInlineText(playerIdentity, displayText);
    }

    public void Clear()
    {
        hasIdentity = false;
        playerNameTheme?.ClearColorOverride();
        if (playerNameText != null) playerNameText.text = string.Empty;
        if (nameArea != null) nameArea.gameObject.SetActive(false);
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
        CacheReferences();
        if (rowRect == null || messageText == null)
        {
            return 0f;
        }

        rowWidth = Mathf.Max(1f, rowWidth);
        rowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rowWidth);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);

        float textWidth = GetCurrentTextWidth(rowWidth);
        float preferredTextHeight = messageText.GetPreferredValues(messageText.text, textWidth, 0f).y;
        float currentTextHeight = Mathf.Max(0f, messageText.rectTransform.rect.height);
        float currentRowHeight = Mathf.Max(0f, rowRect.rect.height);
        float verticalChrome = Mathf.Max(0f, currentRowHeight - currentTextHeight);
        float preferredHeight = preferredTextHeight + verticalChrome;

        preferredHeight = Mathf.Max(preferredHeight, GetVisibleIconHeight() + verticalChrome);


        return Mathf.Max(1f, preferredHeight);
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
