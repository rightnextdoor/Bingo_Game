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
    [SerializeField] private RawImage redGradientImage;
    [SerializeField] private TMP_InputField redInput;
    [SerializeField] private Slider greenSlider;
    [SerializeField] private RawImage greenGradientImage;
    [SerializeField] private TMP_InputField greenInput;
    [SerializeField] private Slider blueSlider;
    [SerializeField] private RawImage blueGradientImage;
    [SerializeField] private TMP_InputField blueInput;

    [Header("Hex")]
    [SerializeField] private TMP_InputField hexInput;

    [Header("Editor Preview")]
    [SerializeField] private Color editorPreviewColor = new Color(1f, 0.35f, 0.15f, 1f);

    private const int HueTextureSize = 512;
    private const int SaturationValueTextureSize = 512;
    private const int RgbGradientTextureWidth = 256;
    private const float HueWheelInnerRadius = 0.84f;

    private Texture2D hueTexture;
    private Texture2D saturationValueTexture;
    private Texture2D redGradientTexture;
    private Texture2D greenGradientTexture;
    private Texture2D blueGradientTexture;
    private Action<Color> colorChanged;
    private Color currentColor = Color.white;
    private float hue;
    private float saturation;
    private float value = 1f;
    private bool syncingUi;
    private bool listenersRegistered;
    private bool huePointerActive;
    private bool saturationValuePointerActive;

    public bool IsOpen => gameObject.activeSelf;
    public Color CurrentColor => currentColor;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        RefreshEditorPreview();
    }

    private void RefreshEditorPreview()
    {
        Color previewColor = editorPreviewColor;
        previewColor.a = 1f;

        Color.RGBToHSV(previewColor, out hue, out saturation, out value);

        BuildHueTexture();
        BuildSaturationValueTexture();
        BuildRgbGradientTextures(previewColor);

        if (previewSwatch != null)
        {
            previewSwatch.color = previewColor;
        }

        SetSelectorPositions();
    }
#endif

    private void Awake()
    {
        ConfigureInputs();
        ClearRuntimeUi();
        BuildHueTexture();
        BuildSaturationValueTexture();
        BuildRgbGradientTextures(currentColor);
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        RegisterListeners();
    }

    private void OnDisable()
    {
        huePointerActive = false;
        saturationValuePointerActive = false;
        UnregisterListeners();
    }

    private void OnDestroy()
    {
        UnregisterListeners();

        DestroyGeneratedTexture(ref hueTexture);
        DestroyGeneratedTexture(ref saturationValueTexture);
        DestroyGeneratedTexture(ref redGradientTexture);
        DestroyGeneratedTexture(ref greenGradientTexture);
        DestroyGeneratedTexture(ref blueGradientTexture);
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
        huePointerActive = false;
        saturationValuePointerActive = false;
        colorChanged = null;
        ClearEditableText();
        gameObject.SetActive(false);
    }

    public bool BeginPointer(ColorPickerPointerAreaType areaType, PointerEventData eventData)
    {
        if (eventData == null)
        {
            return false;
        }

        switch (areaType)
        {
            case ColorPickerPointerAreaType.HueWheel:
                huePointerActive = TryBeginHuePointer(eventData);
                return huePointerActive;

            case ColorPickerPointerAreaType.SaturationValue:
                saturationValuePointerActive = TryBeginSaturationValuePointer(eventData);
                return saturationValuePointerActive;

            default:
                return false;
        }
    }

    public void DragPointer(ColorPickerPointerAreaType areaType, PointerEventData eventData)
    {
        if (eventData == null)
        {
            return;
        }

        switch (areaType)
        {
            case ColorPickerPointerAreaType.HueWheel:
                if (huePointerActive)
                {
                    UpdateHueFromPointer(eventData, false);
                }
                break;

            case ColorPickerPointerAreaType.SaturationValue:
                if (saturationValuePointerActive)
                {
                    UpdateSaturationValueFromPointer(eventData);
                }
                break;
        }
    }

    public void EndPointer(ColorPickerPointerAreaType areaType)
    {
        switch (areaType)
        {
            case ColorPickerPointerAreaType.HueWheel:
                huePointerActive = false;
                break;

            case ColorPickerPointerAreaType.SaturationValue:
                saturationValuePointerActive = false;
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
            hexInput.contentType = TMP_InputField.ContentType.Custom;
            hexInput.characterValidation = TMP_InputField.CharacterValidation.None;
            hexInput.inputType = TMP_InputField.InputType.Standard;
            hexInput.keyboardType = TouchScreenKeyboardType.Default;
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

        input.contentType = TMP_InputField.ContentType.Custom;
        input.characterValidation = TMP_InputField.CharacterValidation.Digit;
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

        RegisterRgbInput(redInput, OnRedInputChanged, OnRedInputEnded);
        RegisterRgbInput(greenInput, OnGreenInputChanged, OnGreenInputEnded);
        RegisterRgbInput(blueInput, OnBlueInputChanged, OnBlueInputEnded);

        if (hexInput != null)
        {
            hexInput.onValueChanged.AddListener(OnHexInputChanged);
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

        UnregisterRgbInput(redInput, OnRedInputChanged, OnRedInputEnded);
        UnregisterRgbInput(greenInput, OnGreenInputChanged, OnGreenInputEnded);
        UnregisterRgbInput(blueInput, OnBlueInputChanged, OnBlueInputEnded);

        if (hexInput != null)
        {
            hexInput.onValueChanged.RemoveListener(OnHexInputChanged);
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

    private void RegisterRgbInput(TMP_InputField input, UnityEngine.Events.UnityAction<string> changedCallback, UnityEngine.Events.UnityAction<string> endedCallback)
    {
        if (input == null)
        {
            return;
        }

        input.onValueChanged.AddListener(changedCallback);
        input.onEndEdit.AddListener(endedCallback);
    }

    private void UnregisterRgbInput(TMP_InputField input, UnityEngine.Events.UnityAction<string> changedCallback, UnityEngine.Events.UnityAction<string> endedCallback)
    {
        if (input == null)
        {
            return;
        }

        input.onValueChanged.RemoveListener(changedCallback);
        input.onEndEdit.RemoveListener(endedCallback);
    }

    private bool TryBeginHuePointer(PointerEventData eventData)
    {
        RectTransform rect = hueWheelImage != null ? hueWheelImage.rectTransform : null;

        if (!TryGetPointerLocalPoint(rect, eventData, out Vector2 localPoint))
        {
            return false;
        }

        float outerRadius = Mathf.Min(rect.rect.width, rect.rect.height) * 0.5f;
        float innerRadius = outerRadius * HueWheelInnerRadius;
        float distance = localPoint.magnitude;

        if (distance < innerRadius || distance > outerRadius)
        {
            return false;
        }

        UpdateHueFromLocalPoint(localPoint);
        return true;
    }

    private void UpdateHueFromPointer(PointerEventData eventData, bool requirePointerInsideRing)
    {
        RectTransform rect = hueWheelImage != null ? hueWheelImage.rectTransform : null;

        if (!TryGetPointerLocalPoint(rect, eventData, out Vector2 localPoint))
        {
            return;
        }

        if (requirePointerInsideRing)
        {
            float outerRadius = Mathf.Min(rect.rect.width, rect.rect.height) * 0.5f;
            float innerRadius = outerRadius * HueWheelInnerRadius;
            float distance = localPoint.magnitude;

            if (distance < innerRadius || distance > outerRadius)
            {
                return;
            }
        }

        if (localPoint.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        UpdateHueFromLocalPoint(localPoint);
    }

    private void UpdateHueFromLocalPoint(Vector2 localPoint)
    {
        float angle = Mathf.Atan2(localPoint.y, localPoint.x) * Mathf.Rad2Deg;
        hue = Mathf.Repeat(angle / 360f, 1f);
        SyncUiFromHsv(true);
    }

    private bool TryBeginSaturationValuePointer(PointerEventData eventData)
    {
        RectTransform rect = saturationValueImage != null ? saturationValueImage.rectTransform : null;

        if (!TryGetPointerLocalPoint(rect, eventData, out Vector2 localPoint) || !rect.rect.Contains(localPoint))
        {
            return false;
        }

        UpdateSaturationValueFromLocalPoint(localPoint);
        return true;
    }

    private void UpdateSaturationValueFromPointer(PointerEventData eventData)
    {
        RectTransform rect = saturationValueImage != null ? saturationValueImage.rectTransform : null;

        if (!TryGetPointerLocalPoint(rect, eventData, out Vector2 localPoint))
        {
            return;
        }

        UpdateSaturationValueFromLocalPoint(localPoint);
    }

    private void UpdateSaturationValueFromLocalPoint(Vector2 localPoint)
    {
        Rect area = saturationValueImage.rectTransform.rect;
        float clampedX = Mathf.Clamp(localPoint.x, area.xMin, area.xMax);
        float clampedY = Mathf.Clamp(localPoint.y, area.yMin, area.yMax);

        saturation = Mathf.InverseLerp(area.xMin, area.xMax, clampedX);
        value = Mathf.InverseLerp(area.yMin, area.yMax, clampedY);
        SyncUiFromHsv(false);
    }

    private bool TryGetPointerLocalPoint(RectTransform rect, PointerEventData eventData, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;

        return rect != null &&
               eventData != null &&
               RectTransformUtility.ScreenPointToLocalPointInRectangle(
                   rect,
                   eventData.position,
                   eventData.pressEventCamera,
                   out localPoint);
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
        SetHueSelectorPosition();
        SetSaturationValueSelectorPosition();
    }

    private void SetHueSelectorPosition()
    {
        if (hueSelector == null || hueWheelImage == null)
        {
            return;
        }

        RectTransform wheelRect = hueWheelImage.rectTransform;
        float outerRadius = Mathf.Min(wheelRect.rect.width, wheelRect.rect.height) * 0.5f;
        float innerRadius = outerRadius * HueWheelInnerRadius;
        float selectorHalfSize = Mathf.Max(hueSelector.rect.width, hueSelector.rect.height) * 0.5f;

        float safeInnerRadius = innerRadius + selectorHalfSize;
        float safeOuterRadius = outerRadius - selectorHalfSize;
        float selectorRadius = safeInnerRadius <= safeOuterRadius
            ? (safeInnerRadius + safeOuterRadius) * 0.5f
            : (innerRadius + outerRadius) * 0.5f;

        float angle = hue * Mathf.PI * 2f;
        hueSelector.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * selectorRadius;
    }

    private void SetSaturationValueSelectorPosition()
    {
        if (saturationValueSelector == null || saturationValueImage == null)
        {
            return;
        }

        Rect area = saturationValueImage.rectTransform.rect;
        float halfWidth = Mathf.Min(saturationValueSelector.rect.width * 0.5f, area.width * 0.5f);
        float halfHeight = Mathf.Min(saturationValueSelector.rect.height * 0.5f, area.height * 0.5f);

        float visualMinX = area.xMin + halfWidth;
        float visualMaxX = area.xMax - halfWidth;
        float visualMinY = area.yMin + halfHeight;
        float visualMaxY = area.yMax - halfHeight;

        float x = Mathf.Lerp(visualMinX, visualMaxX, saturation);
        float y = Mathf.Lerp(visualMinY, visualMaxY, value);
        saturationValueSelector.anchoredPosition = new Vector2(x, y);
    }

    private void SetRgbControls(Color color)
    {
        BuildRgbGradientTextures(color);

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

    private void OnRedInputChanged(string text)
    {
        ApplyRgbInputWhileTyping(redInput, text, true, false, false);
    }

    private void OnGreenInputChanged(string text)
    {
        ApplyRgbInputWhileTyping(greenInput, text, false, true, false);
    }

    private void OnBlueInputChanged(string text)
    {
        ApplyRgbInputWhileTyping(blueInput, text, false, false, true);
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

    private void ApplyRgbInputWhileTyping(TMP_InputField input, string text, bool red, bool green, bool blue)
    {
        if (syncingUi || string.IsNullOrEmpty(text) || !int.TryParse(text, out int parsed))
        {
            return;
        }

        if (parsed > 255)
        {
            parsed = 255;
            input?.SetTextWithoutNotify("255");
            input?.MoveTextEnd(false);
        }

        int r = red ? parsed : GetRedByte();
        int g = green ? parsed : GetGreenByte();
        int b = blue ? parsed : GetBlueByte();
        ApplyRgbChannels(r, g, b);
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

    private void OnHexInputChanged(string text)
    {
        if (syncingUi || hexInput == null)
        {
            return;
        }

        string sanitized = SanitizeHexInput(text);

        if (sanitized == text)
        {
            return;
        }

        hexInput.SetTextWithoutNotify(sanitized);
        hexInput.MoveTextEnd(false);
    }

    private string SanitizeHexInput(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        char[] characters = new char[7];
        int length = 0;
        int hexDigitCount = 0;

        if (text[0] == '#')
        {
            characters[length++] = '#';
        }

        for (int i = 0; i < text.Length && hexDigitCount < 6; i++)
        {
            char character = text[i];

            if (character == '#')
            {
                continue;
            }

            if (!IsHexDigit(character))
            {
                continue;
            }

            characters[length++] = char.ToUpperInvariant(character);
            hexDigitCount++;
        }

        return new string(characters, 0, length);
    }

    private bool IsHexDigit(char character)
    {
        return (character >= '0' && character <= '9') ||
               (character >= 'A' && character <= 'F') ||
               (character >= 'a' && character <= 'f');
    }

    private void OnHexInputEnded(string text)
    {
        if (syncingUi)
        {
            return;
        }

        string sanitized = SanitizeHexInput(text);
        string hexDigits = sanitized.StartsWith("#", StringComparison.Ordinal) ? sanitized.Substring(1) : sanitized;

        if (hexDigits.Length == 6 && ColorUtility.TryParseHtmlString("#" + hexDigits, out Color parsed))
        {
            parsed.a = 1f;
            SyncUiFromRgb(parsed);
            return;
        }

        SetHexControl(currentColor);
    }


    private void BuildRgbGradientTextures(Color color)
    {
        color.a = 1f;

        BuildRgbGradientTexture(
            ref redGradientTexture,
            redGradientImage,
            new Color(0f, color.g, color.b, 1f),
            new Color(1f, color.g, color.b, 1f),
            "RedGradient");

        BuildRgbGradientTexture(
            ref greenGradientTexture,
            greenGradientImage,
            new Color(color.r, 0f, color.b, 1f),
            new Color(color.r, 1f, color.b, 1f),
            "GreenGradient");

        BuildRgbGradientTexture(
            ref blueGradientTexture,
            blueGradientImage,
            new Color(color.r, color.g, 0f, 1f),
            new Color(color.r, color.g, 1f, 1f),
            "BlueGradient");
    }

    private void BuildRgbGradientTexture(ref Texture2D texture, RawImage targetImage, Color startColor, Color endColor, string textureName)
    {
        if (targetImage == null)
        {
            return;
        }

        if (texture == null || texture.width != RgbGradientTextureWidth || texture.height != 1)
        {
            DestroyGeneratedTexture(ref texture);

            texture = new Texture2D(RgbGradientTextureWidth, 1, TextureFormat.RGB24, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
                name = Application.isPlaying ? $"Runtime{textureName}" : $"Editor{textureName}Preview"
            };
        }

        Color[] pixels = new Color[RgbGradientTextureWidth];

        for (int x = 0; x < RgbGradientTextureWidth; x++)
        {
            float t = x / (float)(RgbGradientTextureWidth - 1);
            pixels[x] = Color.Lerp(startColor, endColor, t);
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        targetImage.texture = texture;
        targetImage.color = Color.white;
    }

    private void BuildHueTexture()
    {
        int size = Mathf.Max(64, HueTextureSize);

        if (hueTexture == null || hueTexture.width != size || hueTexture.height != size)
        {
            DestroyGeneratedTexture(ref hueTexture);

            hueTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
                name = Application.isPlaying ? "RuntimeHueWheel" : "EditorHueWheelPreview"
            };
        }

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = size * 0.5f;
        float innerRadius = outerRadius * HueWheelInnerRadius;

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
        int size = Mathf.Max(64, SaturationValueTextureSize);

        if (saturationValueTexture == null || saturationValueTexture.width != size || saturationValueTexture.height != size)
        {
            DestroyGeneratedTexture(ref saturationValueTexture);

            saturationValueTexture = new Texture2D(size, size, TextureFormat.RGB24, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
                name = Application.isPlaying ? "RuntimeSaturationValue" : "EditorSaturationValuePreview"
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

    private void DestroyGeneratedTexture(ref Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(texture);
        }
        else
        {
            DestroyImmediate(texture);
        }

        texture = null;
    }
}
