using UnityEngine;
using UnityEngine.EventSystems;

public enum ColorPickerPointerAreaType
{
    HueWheel,
    SaturationValue
}

[DisallowMultipleComponent]
public class ColorPickerPointerArea : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [SerializeField] private ColorPickerController colorPickerController;
    [SerializeField] private ColorPickerPointerAreaType areaType;

    private void Awake()
    {
        if (colorPickerController == null)
        {
            colorPickerController = GetComponentInParent<ColorPickerController>();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        colorPickerController?.HandlePointer(areaType, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        colorPickerController?.HandlePointer(areaType, eventData);
    }
}
