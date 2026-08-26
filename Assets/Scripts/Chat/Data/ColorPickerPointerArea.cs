using UnityEngine;
using UnityEngine.EventSystems;

public enum ColorPickerPointerAreaType
{
    HueWheel,
    SaturationValue
}

[DisallowMultipleComponent]
public class ColorPickerPointerArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private ColorPickerController colorPickerController;
    [SerializeField] private ColorPickerPointerAreaType areaType;

    private bool pointerActive;

    private void Awake()
    {
        if (colorPickerController == null)
        {
            colorPickerController = GetComponentInParent<ColorPickerController>();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerActive = colorPickerController != null && colorPickerController.BeginPointer(areaType, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (pointerActive)
        {
            colorPickerController?.DragPointer(areaType, eventData);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pointerActive)
        {
            return;
        }

        pointerActive = false;
        colorPickerController?.EndPointer(areaType);
    }

    private void OnDisable()
    {
        if (!pointerActive)
        {
            return;
        }

        pointerActive = false;
        colorPickerController?.EndPointer(areaType);
    }
}
