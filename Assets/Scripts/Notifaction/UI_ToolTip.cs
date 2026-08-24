using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum TooltipImageMode
{
    Default,
    Custom,
    None
}

public enum TooltipGrowthDirection
{
    Default,
    Up,
    Down
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
            messageData.TooltipOffset,
            messageData.TooltipMinimumWidth,
            messageData.TooltipMaximumWidth,
            messageData.TooltipMinimumHeight,
            messageData.TooltipMaximumHeight,
            messageData.TooltipGrowthDirection
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

        ResizeToFitMessage(
            message,
            messageData.TooltipMinimumWidth,
            messageData.TooltipMaximumWidth,
            messageData.TooltipMinimumHeight,
            messageData.TooltipMaximumHeight
        );

        SetTooltipPivot(messageData.TooltipGrowthDirection);
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
            visualStyle.TooltipOffset,
            visualStyle.TooltipMinimumWidth,
            visualStyle.TooltipMaximumWidth,
            visualStyle.TooltipMinimumHeight,
            visualStyle.TooltipMaximumHeight,
            visualStyle.TooltipGrowthDirection
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
        Vector2 tooltipOffset,
        float minimumWidth,
        float maximumWidth,
        float minimumHeight,
        float maximumHeight,
        TooltipGrowthDirection growthDirection)
    {
        ResizeToFitMessage(message, minimumWidth, maximumWidth, minimumHeight, maximumHeight);
        SetTooltipPivot(growthDirection);
        PositionNearTarget(targetRect, tooltipOffset, growthDirection);

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

    private void ResizeToFitMessage(
        string message,
        float minimumWidth,
        float maximumWidth,
        float minimumHeight,
        float maximumHeight)
    {
        if (toolTipRect == null || messageText == null)
        {
            return;
        }

        minimumWidth = Mathf.Max(1f, minimumWidth);
        maximumWidth = Mathf.Max(minimumWidth, maximumWidth);
        minimumHeight = Mathf.Max(1f, minimumHeight);
        maximumHeight = Mathf.Max(minimumHeight, maximumHeight);

        float maxTextWidth = Mathf.Max(10f, maximumWidth - TextPadding.x * 2f);
        Vector2 preferredSize = messageText.GetPreferredValues(message, maxTextWidth, 0f);

        float finalWidth = Mathf.Clamp(
            preferredSize.x + TextPadding.x * 2f,
            minimumWidth,
            maximumWidth
        );

        float finalTextWidth = Mathf.Max(10f, finalWidth - TextPadding.x * 2f);
        Vector2 wrappedPreferredSize = messageText.GetPreferredValues(message, finalTextWidth, 0f);

        float finalHeight = Mathf.Clamp(
            wrappedPreferredSize.y + TextPadding.y * 2f,
            minimumHeight,
            maximumHeight
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

    private void PositionNearTarget(
        RectTransform targetRect,
        Vector2 tooltipOffset,
        TooltipGrowthDirection growthDirection)
    {
        if (toolTipRect == null || parentRect == null || targetRect == null)
        {
            return;
        }

        targetRect.GetWorldCorners(targetWorldCorners);

        Vector2 targetMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 targetMax = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < targetWorldCorners.Length; i++)
        {
            Vector3 localCorner = parentRect.InverseTransformPoint(targetWorldCorners[i]);

            targetMin.x = Mathf.Min(targetMin.x, localCorner.x);
            targetMin.y = Mathf.Min(targetMin.y, localCorner.y);
            targetMax.x = Mathf.Max(targetMax.x, localCorner.x);
            targetMax.y = Mathf.Max(targetMax.y, localCorner.y);
        }

        Vector2 size = toolTipRect.rect.size;
        Rect parentArea = parentRect.rect;

        float preferredLeft = targetMin.x - size.x + tooltipOffset.x;
        float preferredBottom;

        switch (growthDirection)
        {
            case TooltipGrowthDirection.Up:
                preferredBottom = targetMax.y + tooltipOffset.y;
                break;

            case TooltipGrowthDirection.Down:
                preferredBottom = targetMin.y - size.y + tooltipOffset.y;
                break;

            default:
                preferredBottom = targetMin.y - size.y + tooltipOffset.y;

                if (!FitsVertically(preferredBottom, size.y, parentArea))
                {
                    preferredBottom = targetMax.y + tooltipOffset.y;
                }
                break;
        }

        Vector2 fittedBottomLeft = FitBottomLeftInsideParent(
            new Vector2(preferredLeft, preferredBottom),
            size,
            parentArea);

        SetAnchoredPositionFromBottomLeft(fittedBottomLeft, size);
    }

    private bool FitsVertically(float bottom, float height, Rect parentArea)
    {
        float minBottom = parentArea.yMin + ScreenPadding.y;
        float maxTop = parentArea.yMax - ScreenPadding.y;
        float top = bottom + height;

        return bottom >= minBottom && top <= maxTop;
    }

    private Vector2 FitBottomLeftInsideParent(Vector2 bottomLeft, Vector2 size, Rect parentArea)
    {
        float minLeft = parentArea.xMin + ScreenPadding.x;
        float maxLeft = parentArea.xMax - ScreenPadding.x - size.x;
        float minBottom = parentArea.yMin + ScreenPadding.y;
        float maxBottom = parentArea.yMax - ScreenPadding.y - size.y;

        bottomLeft.x = ClampEvenIfRangeIsSmall(bottomLeft.x, minLeft, maxLeft);
        bottomLeft.y = ClampEvenIfRangeIsSmall(bottomLeft.y, minBottom, maxBottom);

        return bottomLeft;
    }

    private void PositionFixed(Vector2 anchoredPosition)
    {
        if (toolTipRect == null || parentRect == null)
        {
            return;
        }

        Vector2 size = toolTipRect.rect.size;
        Rect parentArea = parentRect.rect;
        Vector2 pivot = toolTipRect.pivot;

        Vector2 bottomLeft = anchoredPosition - new Vector2(size.x * pivot.x, size.y * pivot.y);

        float minLeft = parentArea.xMin + ScreenPadding.x;
        float maxLeft = parentArea.xMax - ScreenPadding.x - size.x;
        float minBottom = parentArea.yMin + ScreenPadding.y;
        float maxBottom = parentArea.yMax - ScreenPadding.y - size.y;

        bottomLeft.x = ClampEvenIfRangeIsSmall(bottomLeft.x, minLeft, maxLeft);
        bottomLeft.y = ClampEvenIfRangeIsSmall(bottomLeft.y, minBottom, maxBottom);

        SetAnchoredPositionFromBottomLeft(bottomLeft, size);
    }

    private void SetTooltipPivot(TooltipGrowthDirection growthDirection)
    {
        if (toolTipRect == null)
        {
            return;
        }

        Vector2 anchoredPosition = toolTipRect.anchoredPosition;

        switch (growthDirection)
        {
            case TooltipGrowthDirection.Up:
                toolTipRect.pivot = new Vector2(0f, 0f);
                break;

            case TooltipGrowthDirection.Down:
            case TooltipGrowthDirection.Default:
            default:
                toolTipRect.pivot = new Vector2(0f, 1f);
                break;
        }

        toolTipRect.anchoredPosition = anchoredPosition;
    }

    private void SetAnchoredPositionFromBottomLeft(Vector2 bottomLeft, Vector2 size)
    {
        if (toolTipRect == null)
        {
            return;
        }

        Vector2 pivot = toolTipRect.pivot;

        toolTipRect.anchoredPosition = bottomLeft + new Vector2(
            size.x * pivot.x,
            size.y * pivot.y
        );
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