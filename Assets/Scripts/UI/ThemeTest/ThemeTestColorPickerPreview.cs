using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ThemeTestColorPickerPreview : MonoBehaviour
{
    [SerializeField] private Color startingColor = new Color(1f, 0.35f, 0.15f, 1f);

    private readonly List<ColorPickerController> pickers = new List<ColorPickerController>();
    private readonly HashSet<ColorPickerController> initialized = new HashSet<ColorPickerController>();

    private void Start()
    {
        RefreshPreviews();
    }

    private void LateUpdate()
    {
        RefreshPreviews();
    }

    private void RefreshPreviews()
    {
        GetComponentsInChildren(true, pickers);
        initialized.RemoveWhere(picker => picker == null || picker.transform.parent != transform);

        foreach (ColorPickerController picker in pickers)
        {
            if (picker == null || picker.transform.parent != transform || !picker.enabled)
                continue;

            if (!Application.IsPlaying(gameObject))
            {
                if (!picker.gameObject.activeSelf)
                    picker.gameObject.SetActive(true);
                continue;
            }

            bool firstOpen = initialized.Add(picker);
            if (!firstOpen && picker.IsOpen)
                continue;

            Color color = firstOpen ? startingColor : picker.CurrentColor;
            if (!picker.gameObject.activeSelf)
                picker.gameObject.SetActive(true);

            picker.Open(color, null);
        }
    }

    private void OnDisable()
    {
        initialized.Clear();
        pickers.Clear();
    }
}
