using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UI_ToolTip : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform toolTipRect;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Sizing")]
    [SerializeField] private Vector2 padding = new Vector2(14f, 10f);
    [SerializeField] private float minWidth = 120f;
    [SerializeField] private float maxWidth = 360f;
    [SerializeField] private float minHeight = 44f;
    [SerializeField] private float maxHeight = 220f;

    [Header("Screen Clamp")]
    [SerializeField] private Vector2 screenPadding = new Vector2(12f, 12f);

    private RectTransform parentRect;
    private Canvas parentCanvas;

    private void Awake()
    {
        FindMissingReferences();
        Hide();
    }

    public void Show(UIMessageData messageData, string message, Vector2 pointerScreenPosition)
    {
        if (messageData == null)
        {
            return;
        }

        FindMissingReferences();

        gameObject.SetActive(true);

        ApplyMessageStyle(messageData, message);
        ResizeToFitMessage(message);
        MoveToPointer(pointerScreenPosition, messageData.TooltipOffset);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    public void MoveToPointer(Vector2 pointerScreenPosition, Vector2 offset)
    {
        FindMissingReferences();

        if (toolTipRect == null || parentRect == null)
        {
            return;
        }

        Camera uiCamera = GetUICamera();

        bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            pointerScreenPosition,
            uiCamera,
            out Vector2 localPointerPosition
        );

        if (!converted)
        {
            return;
        }

        Vector2 size = toolTipRect.rect.size;
        Rect parentArea = parentRect.rect;

        Vector2 targetPosition = localPointerPosition + offset;

        float rightEdge = targetPosition.x + size.x;
        float bottomEdge = targetPosition.y - size.y;

        if (rightEdge > parentArea.xMax - screenPadding.x)
        {
            targetPosition.x = localPointerPosition.x - Mathf.Abs(offset.x) - size.x;
        }

        if (bottomEdge < parentArea.yMin + screenPadding.y)
        {
            targetPosition.y = localPointerPosition.y + Mathf.Abs(offset.y) + size.y;
        }

        float minX = parentArea.xMin + screenPadding.x;
        float maxX = parentArea.xMax - screenPadding.x - size.x;

        float minY = parentArea.yMin + screenPadding.y + size.y;
        float maxY = parentArea.yMax - screenPadding.y;

        targetPosition.x = ClampEvenIfRangeIsSmall(targetPosition.x, minX, maxX);
        targetPosition.y = ClampEvenIfRangeIsSmall(targetPosition.y, minY, maxY);

        toolTipRect.anchoredPosition = targetPosition;
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

    private void ApplyMessageStyle(UIMessageData messageData, string message)
    {
        if (messageText != null)
        {
            messageText.richText = true;
            messageText.textWrappingMode = TextWrappingModes.Normal;
            messageText.overflowMode = TextOverflowModes.Overflow;

            if (messageData.FontAsset != null)
            {
                messageText.font = messageData.FontAsset;
            }

            messageText.fontSize = messageData.FontSize;
            messageText.color = messageData.TextColor;
            messageText.text = message;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = messageData.BackgroundColor;
            backgroundImage.raycastTarget = false;
        }
    }

    private void ResizeToFitMessage(string message)
    {
        if (toolTipRect == null || messageText == null)
        {
            return;
        }

        float maxTextWidth = Mathf.Max(10f, maxWidth - padding.x * 2f);

        Vector2 preferredSize = messageText.GetPreferredValues(message, maxTextWidth, 0f);

        float finalWidth = Mathf.Clamp(preferredSize.x + padding.x * 2f, minWidth, maxWidth);

        float finalTextWidth = Mathf.Max(10f, finalWidth - padding.x * 2f);
        Vector2 wrappedPreferredSize = messageText.GetPreferredValues(message, finalTextWidth, 0f);

        float finalHeight = Mathf.Max(minHeight, wrappedPreferredSize.y + padding.y * 2f);

        if (maxHeight > 0f)
        {
            finalHeight = Mathf.Min(finalHeight, maxHeight);
        }

        toolTipRect.sizeDelta = new Vector2(finalWidth, finalHeight);

        RectTransform textRect = messageText.rectTransform;

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);

        textRect.offsetMin = new Vector2(padding.x, padding.y);
        textRect.offsetMax = new Vector2(-padding.x, -padding.y);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(toolTipRect);
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
            backgroundImage = GetComponent<Image>();
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
            toolTipRect.pivot = new Vector2(0f, 1f);
        }

        if (messageText != null)
        {
            messageText.raycastTarget = false;
        }

        if (backgroundImage != null)
        {
            backgroundImage.raycastTarget = false;
        }
    }

    private Camera GetUICamera()
    {
        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>();
        }

        if (parentCanvas == null)
        {
            return null;
        }

        if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return parentCanvas.worldCamera;
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