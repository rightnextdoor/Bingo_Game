using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum TooltipImageMode
{
    Default,
    Custom,
    None
}

[DisallowMultipleComponent]
public class UI_ToolTip : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform toolTipRect;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private CanvasGroup canvasGroup;

    private static readonly Vector2 TextPadding = new Vector2(14f, 10f);
    private static readonly Vector2 ScreenPadding = new Vector2(12f, 12f);

    private const float MinWidth = 120f;
    private const float MaxWidth = 360f;
    private const float MinHeight = 44f;
    private const float MaxHeight = 220f;

    private RectTransform parentRect;
    private Canvas parentCanvas;
    private Sprite defaultBackgroundImage;

    private readonly Vector3[] targetWorldCorners = new Vector3[4];

    private void Awake()
    {
        FindMissingReferences();
        SaveDefaultBackgroundImage();
    }

    private void SaveDefaultBackgroundImage()
    {
        if (backgroundImage != null)
        {
            defaultBackgroundImage = backgroundImage.sprite;
        }
    }

    public void ShowNearTarget(
     UIMessageData messageData,
     string message,
     RectTransform targetRect)
    {
        if (messageData == null || targetRect == null)
        {
            return;
        }

        PrepareToShow();

        ApplyMessageStyle(
            messageData.FontAsset,
            messageData.FontSize,
            messageData.TextColor,
            messageData.BackgroundColor,
            messageData.ImageMode,
            messageData.CustomImage,
            message
        );

        FinishShowing(
            message,
            targetRect,
            messageData.TooltipOffset
        );
    }


    public void ShowFixed(UIMessageData messageData, string message)
    {
        if (messageData == null)
        {
            return;
        }

        PrepareToShow();

        ApplyMessageStyle(
            messageData.FontAsset,
            messageData.FontSize,
            messageData.TextColor,
            messageData.BackgroundColor,
            messageData.ImageMode,
            messageData.CustomImage,
            message
        );

        ResizeToFitMessage(message);
        PositionFixed(messageData.TooltipOffset);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    public void ShowNearTarget(
    TooltipVisualStyle visualStyle,
    RectTransform targetRect)
    {
        if (visualStyle == null || targetRect == null)
        {
            return;
        }

        string message = visualStyle.BuildMessage();

        PrepareToShow();

        ApplyMessageStyle(
            visualStyle.FontAsset,
            visualStyle.FontSize,
            visualStyle.TextColor,
            visualStyle.BackgroundColor,
            visualStyle.ImageMode,
            visualStyle.CustomImage,
            message
        );

        FinishShowing(
            message,
            targetRect,
            visualStyle.TooltipOffset
        );
    }

    public void Hide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (messageText != null)
        {
            messageText.text = string.Empty;
        }

        gameObject.SetActive(false);
    }

    private void PrepareToShow()
    {
        FindMissingReferences();

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    private void FinishShowing(
        string message,
        RectTransform targetRect,
        Vector2 tooltipOffset)
    {
        ResizeToFitMessage(message);
        PositionNearTarget(targetRect, tooltipOffset);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private void ApplyMessageStyle(
    TMP_FontAsset fontAsset,
    int fontSize,
    Color textColor,
    Color backgroundColor,
    TooltipImageMode imageMode,
    Sprite customImage,
    string message)
    {
        if (backgroundImage != null)
        {
            backgroundImage.gameObject.SetActive(true);
            backgroundImage.color = backgroundColor;
            backgroundImage.raycastTarget = false;

            ApplyBackgroundImage(
                imageMode,
                customImage
            );
        }

        if (messageText != null)
        {
            messageText.gameObject.SetActive(true);
            messageText.richText = true;
            messageText.textWrappingMode =
                TextWrappingModes.Normal;
            messageText.overflowMode =
                TextOverflowModes.Ellipsis;
            messageText.enableAutoSizing = false;

            if (fontAsset != null)
            {
                messageText.font = fontAsset;
            }

            messageText.fontSize = fontSize;
            messageText.color = textColor;
            messageText.text = message;
        }
    }

    private void ApplyBackgroundImage(
    TooltipImageMode imageMode,
    Sprite customImage)
    {
        if (backgroundImage == null)
        {
            return;
        }

        switch (imageMode)
        {
            case TooltipImageMode.Default:
                backgroundImage.sprite =
                    defaultBackgroundImage;
                break;

            case TooltipImageMode.Custom:
                backgroundImage.sprite =
                    customImage != null
                        ? customImage
                        : defaultBackgroundImage;
                break;

            case TooltipImageMode.None:
                backgroundImage.sprite = null;
                break;
        }
    }

    private void ResizeToFitMessage(string message)
    {
        if (toolTipRect == null || messageText == null)
        {
            return;
        }

        float maxTextWidth = Mathf.Max(10f, MaxWidth - TextPadding.x * 2f);

        Vector2 preferredSize = messageText.GetPreferredValues(message, maxTextWidth, 0f);

        float finalWidth = Mathf.Clamp(
            preferredSize.x + TextPadding.x * 2f,
            MinWidth,
            MaxWidth
        );

        float finalTextWidth = Mathf.Max(10f, finalWidth - TextPadding.x * 2f);

        Vector2 wrappedPreferredSize = messageText.GetPreferredValues(message, finalTextWidth, 0f);

        float finalHeight = Mathf.Clamp(
            wrappedPreferredSize.y + TextPadding.y * 2f,
            MinHeight,
            MaxHeight
        );

        toolTipRect.sizeDelta = new Vector2(finalWidth, finalHeight);

        RectTransform textRect = messageText.rectTransform;

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);

        textRect.offsetMin = new Vector2(TextPadding.x, TextPadding.y);
        textRect.offsetMax = new Vector2(-TextPadding.x, -TextPadding.y);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(toolTipRect);
    }

    private void PositionNearTarget(RectTransform targetRect, Vector2 tooltipOffset)
    {
        if (toolTipRect == null ||
            parentRect == null ||
            targetRect == null)
        {
            return;
        }

        targetRect.GetWorldCorners(targetWorldCorners);

        Vector2 targetMin = new Vector2(
            float.MaxValue,
            float.MaxValue
        );

        Vector2 targetMax = new Vector2(
            float.MinValue,
            float.MinValue
        );

        for (int i = 0; i < targetWorldCorners.Length; i++)
        {
            Vector3 localCorner =
                parentRect.InverseTransformPoint(
                    targetWorldCorners[i]
                );

            targetMin.x = Mathf.Min(
                targetMin.x,
                localCorner.x
            );

            targetMin.y = Mathf.Min(
                targetMin.y,
                localCorner.y
            );

            targetMax.x = Mathf.Max(
                targetMax.x,
                localCorner.x
            );

            targetMax.y = Mathf.Max(
                targetMax.y,
                localCorner.y
            );
        }

        Vector2 size = toolTipRect.rect.size;
        Rect parentArea = parentRect.rect;

        float horizontalOffset = Mathf.Abs(tooltipOffset.x);
        float verticalOffset = Mathf.Abs(tooltipOffset.y);

        float targetX =
            targetMin.x - horizontalOffset - size.x;

        float targetY =
            targetMin.y - verticalOffset;

        if (targetX < parentArea.xMin + ScreenPadding.x)
        {
            targetX =
                targetMax.x + horizontalOffset;
        }

        if (targetX + size.x >
            parentArea.xMax - ScreenPadding.x)
        {
            targetX =
                parentArea.xMax -
                ScreenPadding.x -
                size.x;
        }

        if (targetY - size.y <
            parentArea.yMin + ScreenPadding.y)
        {
            targetY =
                targetMax.y +
                verticalOffset +
                size.y;
        }

        if (targetY >
            parentArea.yMax - ScreenPadding.y)
        {
            targetY =
                parentArea.yMax -
                ScreenPadding.y;
        }

        float minX =
            parentArea.xMin +
            ScreenPadding.x;

        float maxX =
            parentArea.xMax -
            ScreenPadding.x -
            size.x;

        float minY =
            parentArea.yMin +
            ScreenPadding.y +
            size.y;

        float maxY =
            parentArea.yMax -
            ScreenPadding.y;

        targetX = ClampEvenIfRangeIsSmall(
            targetX,
            minX,
            maxX
        );

        targetY = ClampEvenIfRangeIsSmall(
            targetY,
            minY,
            maxY
        );

        toolTipRect.anchoredPosition =
            new Vector2(targetX, targetY);
    }

    private void PositionFixed(Vector2 anchoredPosition)
    {
        if (toolTipRect == null || parentRect == null)
        {
            return;
        }

        Vector2 size = toolTipRect.rect.size;
        Rect parentArea = parentRect.rect;

        float minX = parentArea.xMin + ScreenPadding.x;
        float maxX = parentArea.xMax - ScreenPadding.x - size.x;
        float minY = parentArea.yMin + ScreenPadding.y + size.y;
        float maxY = parentArea.yMax - ScreenPadding.y;

        toolTipRect.anchoredPosition = new Vector2(
            ClampEvenIfRangeIsSmall(anchoredPosition.x, minX, maxX),
            ClampEvenIfRangeIsSmall(anchoredPosition.y, minY, maxY));
    }

    private void FindMissingReferences()
    {
        if (toolTipRect == null)
        {
            toolTipRect = GetComponent<RectTransform>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponentInChildren<Image>(true);
        }

        if (messageText == null)
        {
            messageText = GetComponentInChildren<TMP_Text>(true);
        }

        if (parentRect == null)
        {
            parentRect = transform.parent as RectTransform;
        }

        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>();
        }

        if (toolTipRect != null)
        {
            toolTipRect.anchorMin = new Vector2(0.5f, 0.5f);
            toolTipRect.anchorMax = new Vector2(0.5f, 0.5f);
            toolTipRect.pivot = new Vector2(0f, 1f);
        }

        if (backgroundImage != null)
        {
            backgroundImage.raycastTarget = false;
        }

        if (messageText != null)
        {
            messageText.raycastTarget = false;
        }
    }

    private float ClampEvenIfRangeIsSmall(float value, float min, float max)
    {
        if (min > max)
        {
            return (min + max) * 0.5f;
        }

        return Mathf.Clamp(value, min, max);
    }
}