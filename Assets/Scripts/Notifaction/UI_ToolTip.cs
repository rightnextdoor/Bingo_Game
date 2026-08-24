using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
            messageData.TooltipGrowthDirection,
            messageData.MessageType == UIMessageType.ChatHelp
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
            messageData.TooltipMaximumHeight,
            messageData.MessageType == UIMessageType.ChatHelp
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
            visualStyle.TooltipGrowthDirection,
            false
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
        TooltipGrowthDirection growthDirection,
        bool useChatHelpLayout)
    {
        ResizeToFitMessage(
            message,
            minimumWidth,
            maximumWidth,
            minimumHeight,
            maximumHeight,
            useChatHelpLayout);

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
        float maximumHeight,
        bool useChatHelpLayout)
    {
        if (toolTipRect == null || messageText == null)
        {
            return;
        }

        minimumWidth = Mathf.Max(1f, minimumWidth);
        maximumWidth = Mathf.Max(minimumWidth, maximumWidth);
        minimumHeight = Mathf.Max(1f, minimumHeight);
        maximumHeight = Mathf.Max(minimumHeight, maximumHeight);

        if (useChatHelpLayout)
        {
            ResizeChatHelpMessage(message, minimumWidth, maximumWidth, minimumHeight, maximumHeight);
            return;
        }

        ResizeDefaultMessage(message, minimumWidth, maximumWidth, minimumHeight, maximumHeight);
    }

    private void ResizeDefaultMessage(
        string message,
        float minimumWidth,
        float maximumWidth,
        float minimumHeight,
        float maximumHeight)
    {
        messageText.textWrappingMode = TextWrappingModes.Normal;
        messageText.overflowMode = TextOverflowModes.Ellipsis;
        messageText.text = message;

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

        ApplyTooltipSize(finalWidth, finalHeight);
    }

    private void ResizeChatHelpMessage(
        string message,
        float minimumWidth,
        float maximumWidth,
        float minimumHeight,
        float maximumHeight)
    {
        ParseChatHelpMessage(
            message,
            out string title,
            out List<string> commands,
            out List<string> descriptions);

        if (commands.Count == 0)
        {
            ResizeDefaultMessage(message, minimumWidth, maximumWidth, minimumHeight, maximumHeight);
            return;
        }

        messageText.textWrappingMode = TextWrappingModes.NoWrap;
        messageText.overflowMode = TextOverflowModes.Overflow;

        float widestCommand = 0f;
        float widestDescription = 0f;

        for (int i = 0; i < commands.Count; i++)
        {
            widestCommand = Mathf.Max(widestCommand, MeasurePlainTextWidth(commands[i]));
            widestDescription = Mathf.Max(widestDescription, MeasurePlainTextWidth(descriptions[i]));
        }

        float titleWidth = MeasurePlainTextWidth(title);
        float columnGap = TextPadding.x;
        float requiredTextWidth = Mathf.Max(
            titleWidth,
            widestCommand + columnGap + widestDescription);

        float finalWidth = Mathf.Clamp(
            requiredTextWidth + TextPadding.x * 2f,
            minimumWidth,
            maximumWidth);

        float finalTextWidth = Mathf.Max(10f, finalWidth - TextPadding.x * 2f);
        float descriptionStart = Mathf.Min(widestCommand + columnGap, finalTextWidth);
        float descriptionWidth = Mathf.Max(0f, finalTextWidth - descriptionStart);

        string formattedMessage = BuildChatHelpText(
            title,
            commands,
            descriptions,
            descriptionStart,
            descriptionWidth);

        messageText.text = formattedMessage;

        Vector2 preferredSize = messageText.GetPreferredValues(
            formattedMessage,
            finalTextWidth,
            0f);

        float finalHeight = Mathf.Clamp(
            preferredSize.y + TextPadding.y * 2f,
            minimumHeight,
            maximumHeight);

        ApplyTooltipSize(finalWidth, finalHeight);
        messageText.ForceMeshUpdate(true, true);
    }

    private void ParseChatHelpMessage(
        string message,
        out string title,
        out List<string> commands,
        out List<string> descriptions)
    {
        title = string.Empty;
        commands = new List<string>();
        descriptions = new List<string>();

        string normalizedMessage = (message ?? string.Empty).Replace("\r\n", "\n");
        string[] lines = normalizedMessage.Split('\n');
        bool titleFound = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i] ?? string.Empty;

            if (!titleFound && !string.IsNullOrWhiteSpace(line))
            {
                title = line.Trim();
                titleFound = true;
                continue;
            }

            int separatorIndex = line.IndexOf('\t');

            if (separatorIndex < 0)
            {
                continue;
            }

            commands.Add(line.Substring(0, separatorIndex).TrimEnd());
            descriptions.Add(line.Substring(separatorIndex + 1).Trim());
        }
    }

    private string BuildChatHelpText(
        string title,
        IReadOnlyList<string> commands,
        IReadOnlyList<string> descriptions,
        float descriptionStart,
        float descriptionWidth)
    {
        StringBuilder builder = new StringBuilder();

        builder.Append("<align=\"center\"><noparse>");
        builder.Append(title);
        builder.AppendLine("</noparse></align>");
        builder.AppendLine();
        builder.Append("<align=\"left\">");

        string descriptionPosition = descriptionStart.ToString("0.##", CultureInfo.InvariantCulture);

        for (int i = 0; i < commands.Count; i++)
        {
            string description = TruncateTextToWidth(descriptions[i], descriptionWidth);

            builder.Append("<noparse>");
            builder.Append(commands[i]);
            builder.Append("</noparse>");
            builder.Append("<pos=");
            builder.Append(descriptionPosition);
            builder.Append("px><noparse>");
            builder.Append(description);
            builder.Append("</noparse>");

            if (i < commands.Count - 1)
            {
                builder.AppendLine();
            }
        }

        builder.Append("</align>");
        return builder.ToString();
    }

    private string TruncateTextToWidth(string text, float maximumWidth)
    {
        string value = text ?? string.Empty;

        if (string.IsNullOrEmpty(value) || maximumWidth <= 0f)
        {
            return string.Empty;
        }

        if (MeasurePlainTextWidth(value) <= maximumWidth)
        {
            return value;
        }

        const string ellipsis = "...";
        float ellipsisWidth = MeasurePlainTextWidth(ellipsis);

        if (ellipsisWidth > maximumWidth)
        {
            return string.Empty;
        }

        int low = 0;
        int high = value.Length;

        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            string candidate = value.Substring(0, middle).TrimEnd() + ellipsis;

            if (MeasurePlainTextWidth(candidate) <= maximumWidth)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return value.Substring(0, low).TrimEnd() + ellipsis;
    }

    private float MeasurePlainTextWidth(string text)
    {
        bool richText = messageText.richText;
        messageText.richText = false;

        Vector2 preferredSize = messageText.GetPreferredValues(
            text ?? string.Empty,
            0f,
            0f);

        messageText.richText = richText;
        return preferredSize.x;
    }

    private void ApplyTooltipSize(float width, float height)
    {
        toolTipRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        toolTipRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

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