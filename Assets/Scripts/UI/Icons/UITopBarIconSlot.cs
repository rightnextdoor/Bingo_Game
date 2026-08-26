using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UITopBarIconSlot : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerClickHandler
{
    [Header("Image")]
    [SerializeField] private Image iconImage;

    [Header("Icon Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color32(241, 243, 245, 255);
    [SerializeField] private Color pressedColor = new Color32(201, 209, 217, 255);
    [SerializeField] private Color disabledColor = new Color32(89, 97, 113, 180);

    [Header("Scale")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float pressedScale = 0.92f;

    private UIMessageType tooltipMessageType = UIMessageType.None;
    private ToolTipManager toolTipManager;
    private Action clickCallback;
    private Func<string> tooltipMessageProvider;
    private Coroutine tooltipDelayRoutine;
    private float tooltipOpenDelay;
    private bool hasIcon;
    private bool isHovering;
    private bool isPressed;
    private bool hoverTooltipShowing;

    private void Awake()
    {
        FindMissingReferences();
        CacheToolTipManager();
    }

    private void OnDisable()
    {
        CancelTooltipDelay();
        CloseHoverTooltip();

        isHovering = false;
        isPressed = false;
        ApplyVisualState();
    }

    public void Setup(
        Sprite iconSprite,
        Action onClicked,
        UIMessageType newTooltipMessageType,
        float newTooltipOpenDelay = 0f,
        Func<string> newTooltipMessageProvider = null)
    {
        CancelTooltipDelay();
        CloseHoverTooltip();
        FindMissingReferences();

        clickCallback = onClicked;
        tooltipMessageType = newTooltipMessageType;
        tooltipOpenDelay = Mathf.Max(0f, newTooltipOpenDelay);
        tooltipMessageProvider = newTooltipMessageProvider;

        SetIcon(iconSprite);

        isHovering = false;
        isPressed = false;
        hoverTooltipShowing = false;

        ApplyVisualState();
    }

    public void SetIcon(Sprite iconSprite)
    {
        FindMissingReferences();

        hasIcon = iconSprite != null;

        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;
            iconImage.enabled = hasIcon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = true;
        }

        ApplyVisualState();
    }

    public void CancelTooltipDelay()
    {
        if (tooltipDelayRoutine == null)
        {
            return;
        }

        StopCoroutine(tooltipDelayRoutine);
        tooltipDelayRoutine = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        ApplyVisualState();

        if (tooltipMessageType == UIMessageType.None || UIMessageCatalog.instance == null || !CacheToolTipManager())
        {
            return;
        }

        UIMessageData messageData = UIMessageCatalog.instance.GetMessage(tooltipMessageType);

        if (messageData == null || toolTipManager.IsShowing(tooltipMessageType))
        {
            return;
        }

        CancelTooltipDelay();

        if (tooltipOpenDelay <= 0f)
        {
            ShowHoverTooltip(messageData);
            return;
        }

        tooltipDelayRoutine = StartCoroutine(ShowHoverTooltipAfterDelay(messageData));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        isPressed = false;

        CancelTooltipDelay();
        CloseHoverTooltip();
        ApplyVisualState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        isPressed = true;
        ApplyVisualState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        ApplyVisualState();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        CancelTooltipDelay();
        CloseHoverTooltip();
        clickCallback?.Invoke();
    }

    private IEnumerator ShowHoverTooltipAfterDelay(UIMessageData messageData)
    {
        yield return new WaitForSecondsRealtime(tooltipOpenDelay);
        tooltipDelayRoutine = null;

        if (!isHovering || messageData == null || !CacheToolTipManager() || toolTipManager.IsShowing(tooltipMessageType))
        {
            yield break;
        }

        ShowHoverTooltip(messageData);
    }

    private void ShowHoverTooltip(UIMessageData messageData)
    {
        if (!isHovering || messageData == null || !CacheToolTipManager())
        {
            return;
        }

        RectTransform targetRect = transform as RectTransform;

        if (targetRect == null)
        {
            return;
        }

        string messageOverride = tooltipMessageProvider?.Invoke();
        toolTipManager.ShowToolTip(messageData, targetRect, messageOverride);
        hoverTooltipShowing = true;
    }

    private void CloseHoverTooltip()
    {
        if (!hoverTooltipShowing)
        {
            return;
        }

        if (CacheToolTipManager() && toolTipManager.IsShowing(tooltipMessageType))
        {
            toolTipManager.HideToolTip();
        }

        hoverTooltipShowing = false;
    }

    private void ApplyVisualState()
    {
        if (iconImage != null)
        {
            if (!hasIcon)
            {
                iconImage.color = disabledColor;
            }
            else if (isPressed)
            {
                iconImage.color = pressedColor;
            }
            else if (isHovering)
            {
                iconImage.color = hoverColor;
            }
            else
            {
                iconImage.color = normalColor;
            }
        }

        float targetScale = normalScale;

        if (isPressed)
        {
            targetScale = pressedScale;
        }
        else if (isHovering)
        {
            targetScale = hoverScale;
        }

        transform.localScale = Vector3.one * targetScale;
    }

    private void FindMissingReferences()
    {
        if (iconImage == null)
        {
            iconImage = GetComponentInChildren<Image>(true);
        }
    }

    private bool CacheToolTipManager()
    {
        if (toolTipManager == null)
        {
            toolTipManager = ToolTipManager.instance;
        }

        return toolTipManager != null;
    }
}
