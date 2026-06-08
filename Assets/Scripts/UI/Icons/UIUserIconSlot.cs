using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIUserIconSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Images")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.25f, 1f);

    [Header("Scale")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float pressedScale = 0.95f;

    private UserIconData iconData;
    private Action<UserIconData> clickCallback;

    private bool isSelected;
    private bool isHovering;
    private bool isPressed;

    public UserIconData IconData => iconData;

    public void Setup(UserIconData newIconData, Action<UserIconData> onClicked)
    {
        iconData = newIconData;
        clickCallback = onClicked;

        if (iconImage != null)
        {
            iconImage.sprite = iconData != null ? iconData.IconSprite : null;
            iconImage.enabled = iconImage.sprite != null;
            iconImage.raycastTarget = false;
        }

        if (backgroundImage != null)
        {
            backgroundImage.raycastTarget = true;
        }

        SetSelected(false);
        ApplyVisualState();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        ApplyVisualState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        ApplyVisualState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        isPressed = false;
        ApplyVisualState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
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
        if (iconData == null)
        {
            return;
        }

        clickCallback?.Invoke(iconData);
    }

    private void ApplyVisualState()
    {
        if (backgroundImage != null)
        {
            if (isSelected)
            {
                backgroundImage.color = selectedColor;
            }
            else if (isPressed)
            {
                backgroundImage.color = pressedColor;
            }
            else if (isHovering)
            {
                backgroundImage.color = hoverColor;
            }
            else
            {
                backgroundImage.color = normalColor;
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
}