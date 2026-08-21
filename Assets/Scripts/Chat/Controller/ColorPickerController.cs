using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ColorPickerController : MonoBehaviour
{
    [Header("Popup")]
    [SerializeField] private Button dismissAreaButton;
    [SerializeField] private Button closeButton;

    [Header("Hue")]
    [SerializeField] private RawImage hueWheelImage;
    [SerializeField] private RectTransform hueSelector;

    [Header("Saturation / Value")]
    [SerializeField] private RawImage saturationValueImage;
    [SerializeField] private RectTransform saturationValueSelector;

    [Header("Preview")]
    [SerializeField] private Image previewSwatch;

    [Header("RGB")]
    [SerializeField] private Slider redSlider;
    [SerializeField] private TMP_InputField redInput;
    [SerializeField] private Slider greenSlider;
    [SerializeField] private TMP_InputField greenInput;
    [SerializeField] private Slider blueSlider;
    [SerializeField] private TMP_InputField blueInput;

    [Header("Hex")]
    [SerializeField] private TMP_InputField hexInput;

    [Header("Generated Textures")]
    [SerializeField, Range(64, 512)] private int hueTextureSize = 256;
    [SerializeField, Range(64, 512)] private int saturationValueTextureSize = 256;
    [SerializeField, Range(0.1f, 0.95f)] private float hueWheelInnerRadius = 0.68f;

    private Texture2D hueTexture;
    private Texture2D saturationValueTexture;
    private Action<Color> colorChanged;
    private Color currentColor = Color.white;
    private float hue;
    private float saturation;
    private float value = 1f;
    private bool syncingUi;
    private bool listenersRegistered;

    public bool IsOpen => gameObject.activeSelf;
    public Color CurrentColor => currentColor;

    private void Awake()
    {
        ConfigureInputs();
        ClearRuntimeUi();
        BuildHueTexture();
        BuildSaturationValueTexture();
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        RegisterListeners();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    private void OnDestroy()
    {
        UnregisterListeners();

        if (hueTexture != null)
        {
            Destroy(hueTexture);
        }

        if (saturationValueTexture != null)
        {
            Destroy(saturationValueTexture);
        }
    }

    public void Open(Color startColor, Action<Color> onColorChanged)
    {
        colorChanged = onColorChanged;
        startColor.a = 1f;
        currentColor = startColor;
        Color.RGBToHSV(currentColor, out hue, out saturation, out value);

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        SyncUiFromHsv(true);
    }

    public void Close()
    {
        colorChanged = null;
        ClearEditableText();
        gameObject.SetActive(false);
    }

    public void HandlePointer(ColorPickerPointerAreaType areaType, PointerEventData eventData)
    {
        if (eventData == null)
        {
            return;
        }

        switch (areaType)
        {
            case ColorPickerPointerAreaType.HueWheel:
                UpdateHueFromPointer(eventData);
                break;

            case ColorPickerPointerAreaType.SaturationValue:
                UpdateSaturationValueFromPointer(eventData);
                break;
        }
    }

    private void ConfigureInputs()
    {
        ConfigureRgbSlider(redSlider);
        ConfigureRgbSlider(greenSlider);
        ConfigureRgbSlider(blueSlider);

        ConfigureRgbInput(redInput);
        ConfigureRgbInput(greenInput);
        ConfigureRgbInput(blueInput);

        if (hexInput != null)
        {
            hexInput.lineType = TMP_InputField.LineType.SingleLine;
            hexInput.characterLimit = 7;
        }
    }

    private void ConfigureRgbSlider(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 255f;
        slider.wholeNumbers = true;
    }

    private void ConfigureRgbInput(TMP_InputField input)
    {
        if (input == null)
        {
            return;
        }

        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 3;
    }

    private void ClearRuntimeUi()
    {
        colorChanged = null;
        ClearEditableText();

        if (previewSwatch != null)
        {
            previewSwatch.color = Color.clear;
        }
    }

    private void ClearEditableText()
    {
        redInput?.SetTextWithoutNotify(string.Empty);
        greenInput?.SetTextWithoutNotify(string.Empty);
        blueInput?.SetTextWithoutNotify(string.Empty);
        hexInput?.SetTextWithoutNotify(string.Empty);
    }

    private void RegisterListeners()
    {
        if (listenersRegistered)
        {
            return;
        }

        if (dismissAreaButton != null)
        {
            dismissAreaButton.onClick.AddListener(Close);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        RegisterSlider(redSlider, OnRedSliderChanged);
        RegisterSlider(greenSlider, OnGreenSliderChanged);
        RegisterSlider(blueSlider, OnBlueSliderChanged);

        RegisterInput(redInput, OnRedInputEnded);
        RegisterInput(greenInput, OnGreenInputEnded);
        RegisterInput(blueInput, OnBlueInputEnded);

        if (hexInput != null)
        {
            hexInput.onEndEdit.AddListener(OnHexInputEnded);
        }

        listenersRegistered = true;
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered)
        {
            return;
        }

        if (dismissAreaButton != null)
        {
            dismissAreaButton.onClick.RemoveListener(Close);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        UnregisterSlider(redSlider, OnRedSliderChanged);
        UnregisterSlider(greenSlider, OnGreenSliderChanged);
        UnregisterSlider(blueSlider, OnBlueSliderChanged);

        UnregisterInput(redInput, OnRedInputEnded);
        UnregisterInput(greenInput, OnGreenInputEnded);
        UnregisterInput(blueInput, OnBlueInputEnded);

        if (hexInput != null)
        {
            hexInput.onEndEdit.RemoveListener(OnHexInputEnded);
        }

        listenersRegistered = false;
    }

    private void RegisterSlider(Slider slider, UnityEngine.Events.UnityAction<float> callback)
    {
        slider?.onValueChanged.AddListener(callback);
    }

    private void UnregisterSlider(Slider slider, UnityEngine.Events.UnityAction<float> callback)
    {
        slider?.onValueChanged.RemoveListener(callback);
    }

    private void RegisterInput(TMP_InputField input, UnityEngine.Events.UnityAction<string> callback)
    {
        input?.onEndEdit.AddListener(callback);
    }

    private void UnregisterInput(TMP_InputField input, UnityEngine.Events.UnityAction<string> callback)
    {
        input?.onEndEdit.RemoveListener(callback);
    }

    private void UpdateHueFromPointer(PointerEventData eventData)
    {
        RectTransform rect = hueWheelImage != null ? hueWheelImage.rectTransform : null;

        if (rect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            return;
        }

        float outerRadius = Mathf.Min(rect.rect.width, rect.rect.height) * 0.5f;
        float distance = localPoint.magnitude;

        if (distance > outerRadius || distance < outerRadius * hueWheelInnerRadius)
        {
            return;
        }

        float angle = Mathf.Atan2(localPoint.y, localPoint.x) * Mathf.Rad2Deg;
        hue = Mathf.Repeat(angle / 360f, 1f);
        SyncUiFromHsv(true);
    }

    private void UpdateSaturationValueFromPointer(PointerEventData eventData)
    {
        RectTransform rect = saturationValueImage != null ? saturationValueImage.rectTransform : null;

        if (rect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            return;
        }

        Rect area = rect.rect;
        saturation = Mathf.InverseLerp(area.xMin, area.xMax, Mathf.Clamp(localPoint.x, area.xMin, area.xMax));
        value = Mathf.InverseLerp(area.yMin, area.yMax, Mathf.Clamp(localPoint.y, area.yMin, area.yMax));
        SyncUiFromHsv(false);
    }

    private void SyncUiFromHsv(bool rebuildSaturationValueTexture)
    {
        currentColor = Color.HSVToRGB(hue, saturation, value);
        currentColor.a = 1f;

        if (rebuildSaturationValueTexture)
        {
            BuildSaturationValueTexture();
        }

        syncingUi = true;

        SetSelectorPositions();
        SetRgbControls(currentColor);
        SetHexControl(currentColor);

        if (previewSwatch != null)
        {
            previewSwatch.color = currentColor;
        }

        syncingUi = false;
        colorChanged?.Invoke(currentColor);
    }

    private void SyncUiFromRgb(Color color)
    {
        color.a = 1f;
        currentColor = color;
        Color.RGBToHSV(currentColor, out hue, out saturation, out value);
        BuildSaturationValueTexture();

        syncingUi = true;
        SetSelectorPositions();
        SetRgbControls(currentColor);
        SetHexControl(currentColor);

        if (previewSwatch != null)
        {
            previewSwatch.color = currentColor;
        }

        syncingUi = false;
        colorChanged?.Invoke(currentColor);
    }

    private void SetSelectorPositions()
    {
        if (hueSelector != null && hueWheelImage != null)
        {
            RectTransform wheelRect = hueWheelImage.rectTransform;
            float radius = Mathf.Min(wheelRect.rect.width, wheelRect.rect.height) * 0.5f;
            float selectorRadius = Mathf.Lerp(radius * hueWheelInnerRadius, radius, 0.5f);
            float angle = hue * Mathf.PI * 2f;
            hueSelector.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * selectorRadius;
        }

        if (saturationValueSelector != null && saturationValueImage != null)
        {
            Rect rect = saturationValueImage.rectTransform.rect;
            float x = Mathf.Lerp(rect.xMin, rect.xMax, saturation);
            float y = Mathf.Lerp(rect.yMin, rect.yMax, value);
            saturationValueSelector.anchoredPosition = new Vector2(x, y);
        }
    }

    private void SetRgbControls(Color color)
    {
        int r = Mathf.RoundToInt(color.r * 255f);
        int g = Mathf.RoundToInt(color.g * 255f);
        int b = Mathf.RoundToInt(color.b * 255f);

        redSlider?.SetValueWithoutNotify(r);
        greenSlider?.SetValueWithoutNotify(g);
        blueSlider?.SetValueWithoutNotify(b);

        redInput?.SetTextWithoutNotify(r.ToString());
        greenInput?.SetTextWithoutNotify(g.ToString());
        blueInput?.SetTextWithoutNotify(b.ToString());
    }

    private void SetHexControl(Color color)
    {
        if (hexInput != null)
        {
            hexInput.SetTextWithoutNotify($"#{ColorUtility.ToHtmlStringRGB(color)}");
        }
    }

    private void OnRedSliderChanged(float value)
    {
        if (!syncingUi)
        {
            ApplyRgbChannels(Mathf.RoundToInt(value), GetGreenByte(), GetBlueByte());
        }
    }

    private void OnGreenSliderChanged(float value)
    {
        if (!syncingUi)
        {
            ApplyRgbChannels(GetRedByte(), Mathf.RoundToInt(value), GetBlueByte());
        }
    }

    private void OnBlueSliderChanged(float value)
    {
        if (!syncingUi)
        {
            ApplyRgbChannels(GetRedByte(), GetGreenByte(), Mathf.RoundToInt(value));
        }
    }

    private void OnRedInputEnded(string text)
    {
        ApplyRgbInput(text, true, false, false);
    }

    private void OnGreenInputEnded(string text)
    {
        ApplyRgbInput(text, false, true, false);
    }

    private void OnBlueInputEnded(string text)
    {
        ApplyRgbInput(text, false, false, true);
    }

    private void ApplyRgbInput(string text, bool red, bool green, bool blue)
    {
        if (syncingUi)
        {
            return;
        }

        if (!int.TryParse(text, out int parsed))
        {
            SetRgbControls(currentColor);
            return;
        }

        parsed = Mathf.Clamp(parsed, 0, 255);
        int r = red ? parsed : GetRedByte();
        int g = green ? parsed : GetGreenByte();
        int b = blue ? parsed : GetBlueByte();
        ApplyRgbChannels(r, g, b);
    }

    private void ApplyRgbChannels(int r, int g, int b)
    {
        Color color = new Color32((byte)Mathf.Clamp(r, 0, 255), (byte)Mathf.Clamp(g, 0, 255), (byte)Mathf.Clamp(b, 0, 255), 255);
        SyncUiFromRgb(color);
    }

    private int GetRedByte()
    {
        return Mathf.RoundToInt(currentColor.r * 255f);
    }

    private int GetGreenByte()
    {
        return Mathf.RoundToInt(currentColor.g * 255f);
    }

    private int GetBlueByte()
    {
        return Mathf.RoundToInt(currentColor.b * 255f);
    }

    private void OnHexInputEnded(string text)
    {
        if (syncingUi)
        {
            return;
        }

        string hex = text?.Trim() ?? string.Empty;

        if (!hex.StartsWith("#", StringComparison.Ordinal))
        {
            hex = "#" + hex;
        }

        if (hex.Length == 7 && ColorUtility.TryParseHtmlString(hex, out Color parsed))
        {
            parsed.a = 1f;
            SyncUiFromRgb(parsed);
            return;
        }

        SetHexControl(currentColor);
    }

    private void BuildHueTexture()
    {
        int size = Mathf.Max(64, hueTextureSize);

        if (hueTexture == null || hueTexture.width != size || hueTexture.height != size)
        {
            if (hueTexture != null)
            {
                Destroy(hueTexture);
            }

            hueTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "RuntimeHueWheel"
            };
        }

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = size * 0.5f;
        float innerRadius = outerRadius * hueWheelInnerRadius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new Vector2(x, y) - center;
                float distance = offset.magnitude;
                int index = (y * size) + x;

                if (distance > outerRadius || distance < innerRadius)
                {
                    pixels[index] = Color.clear;
                    continue;
                }

                float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                float pixelHue = Mathf.Repeat(angle / 360f, 1f);
                pixels[index] = Color.HSVToRGB(pixelHue, 1f, 1f);
            }
        }

        hueTexture.SetPixels(pixels);
        hueTexture.Apply(false, false);

        if (hueWheelImage != null)
        {
            hueWheelImage.texture = hueTexture;
        }
    }

    private void BuildSaturationValueTexture()
    {
        int size = Mathf.Max(64, saturationValueTextureSize);

        if (saturationValueTexture == null || saturationValueTexture.width != size || saturationValueTexture.height != size)
        {
            if (saturationValueTexture != null)
            {
                Destroy(saturationValueTexture);
            }

            saturationValueTexture = new Texture2D(size, size, TextureFormat.RGB24, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "RuntimeSaturationValue"
            };
        }

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float pixelValue = y / (float)(size - 1);

            for (int x = 0; x < size; x++)
            {
                float pixelSaturation = x / (float)(size - 1);
                pixels[(y * size) + x] = Color.HSVToRGB(hue, pixelSaturation, pixelValue);
            }
        }

        saturationValueTexture.SetPixels(pixels);
        saturationValueTexture.Apply(false, false);

        if (saturationValueImage != null)
        {
            saturationValueImage.texture = saturationValueTexture;
        }
    }
}
