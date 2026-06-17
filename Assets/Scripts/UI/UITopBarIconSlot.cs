using System;
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

    private UIMessageData tooltipMessageData;
    private ToolTipManager toolTipManager;

    private Action clickCallback;

    private bool hasIcon;
    private bool isHovering;
    private bool isPressed;

    private void Awake()
    {
        FindMissingReferences();
        CacheToolTipManager();
    }

    private void OnDisable()
    {
        if (isHovering && CacheToolTipManager())
        {
            toolTipManager.HideToolTip();
        }

        isHovering = false;
        isPressed = false;
    }

    public void Setup(Sprite iconSprite, Action onClicked, UIMessageData newTooltipMessageData)
    {
        FindMissingReferences();

        clickCallback = onClicked;
        tooltipMessageData = newTooltipMessageData;

        SetIcon(iconSprite);

        isHovering = false;
        isPressed = false;

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

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        ApplyVisualState();

        if (tooltipMessageData != null && CacheToolTipManager())
        {
            toolTipManager.ShowToolTip(tooltipMessageData, GetComponent<RectTransform>());
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        isPressed = false;
        ApplyVisualState();

        if (CacheToolTipManager())
        {
            toolTipManager.HideToolTip();
        }
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

        if (CacheToolTipManager())
        {
            toolTipManager.HideToolTip();
        }

        clickCallback?.Invoke();
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