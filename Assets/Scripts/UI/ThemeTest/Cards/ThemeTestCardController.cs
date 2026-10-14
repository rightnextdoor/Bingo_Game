using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BingoGame.UI.ThemeTest
{
    [DisallowMultipleComponent]
    public sealed class ThemeTestCardController : MonoBehaviour
    {
        [Header("Card UI")]
        [Tooltip("Neutral title outside the themed preview.")]
        [SerializeField] private TMP_Text cardTitle;
        [Tooltip("Dropdown UI control. Its background options are populated from UIThemeManager.")]
        [SerializeField] private TMP_Dropdown backgroundDropdown;
        [Tooltip("Theme component on the separate backing Image, not on PreviewContent.")]
        [SerializeField] private UIThemeBackground previewBackground;

        [Space]
        [Header("Preview Components")]
        [Tooltip("UIThemeBackground on ImagePreview.")]
        [SerializeField] private UIThemeBackground imagePreview;
        [Tooltip("UIThemeButton on ButtonPreview.")]
        [SerializeField] private UIThemeButton buttonPreview;
        [Tooltip("UIThemeText on TextPreview.")]
        [SerializeField] private UIThemeText textPreview;
        [Tooltip("UIThemeInput on InputPreview.")]
        [SerializeField] private UIThemeInput inputPreview;
        [Tooltip("UIThemeDropdown on DropdownPreview.")]
        [SerializeField] private UIThemeDropdown dropdownPreview;
        [Tooltip("UIThemeScroll on ScrollPreview.")]
        [SerializeField] private UIThemeScroll scrollPreview;
        [Tooltip("UIThemeSlider on SliderPreview.")]
        [SerializeField] private UIThemeSlider sliderPreview;
        [Tooltip("UIThemeToggle on TogglePreview.")]
        [SerializeField] private UIThemeToggle togglePreview;

        private ThemeTestCardBackground background;
        private ThemeTestCardTextFit textFit;
        private IUIThemeTarget activePreview;

        public bool HasRequiredUI => cardTitle != null && backgroundDropdown != null && previewBackground != null
            && imagePreview != null && buttonPreview != null && textPreview != null && inputPreview != null
            && dropdownPreview != null && scrollPreview != null && sliderPreview != null && togglePreview != null;

        private void Awake()
        {
            if (!HasRequiredUI)
            {
                Debug.LogWarning("Assign all card UI and preview theme components on ThemeTestCardController.", this);
                enabled = false;
                return;
            }

            background = new ThemeTestCardBackground(backgroundDropdown, previewBackground);
            textFit = new ThemeTestCardTextFit(GetComponentsInChildren<TMP_Text>(true));
        }

        private void OnEnable()
        {
            _ = UnityEngine.UI.CanvasUpdateRegistry.instance;
            Canvas.willRenderCanvases += FitText;
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= FitText;
        }

        public void Configure(UIThemeSectionType _sectionType, Enum _entryType)
        {
            if (background == null)
                return;

            HidePreviews();
            switch (_sectionType)
            {
                case UIThemeSectionType.Background when _entryType is UIThemeBackgroundType type:
                    imagePreview.SetBackgroundType(type);
                    Activate(imagePreview);
                    break;
                case UIThemeSectionType.Button when _entryType is UIThemeButtonType type:
                    buttonPreview.SetButtonType(type);
                    Activate(buttonPreview);
                    break;
                case UIThemeSectionType.Text when _entryType is UIThemeTextType type:
                    textPreview.SetTextType(type);
                    Activate(textPreview);
                    break;
                case UIThemeSectionType.Input when _entryType is UIThemeInputType type:
                    inputPreview.SetInputType(type);
                    Activate(inputPreview);
                    break;
                case UIThemeSectionType.Dropdown when _entryType is UIThemeDropdownType type:
                    dropdownPreview.SetDropdownType(type);
                    Activate(dropdownPreview);
                    break;
                case UIThemeSectionType.Scroll when _entryType is UIThemeScrollType type:
                    scrollPreview.SetScrollType(type);
                    Activate(scrollPreview);
                    break;
                case UIThemeSectionType.Slider when _entryType is UIThemeSliderType type:
                    sliderPreview.SetSliderType(type);
                    Activate(sliderPreview);
                    break;
                case UIThemeSectionType.Toggle when _entryType is UIThemeToggleType type:
                    togglePreview.SetToggleType(type);
                    Activate(togglePreview);
                    break;
                default:
                    Debug.LogWarning("Card section and entry type do not match.", this);
                    return;
            }

            cardTitle.text = _entryType.ToString();
            background.Refresh(true);
            RefreshPreview();
        }

        private void Activate<T>(T _preview) where T : MonoBehaviour, IUIThemeTarget
        {
            activePreview = _preview;
            _preview.gameObject.SetActive(true);
            _preview.ReapplyTheme();
        }

        private void HidePreviews()
        {
            imagePreview.gameObject.SetActive(false);
            buttonPreview.gameObject.SetActive(false);
            textPreview.gameObject.SetActive(false);
            inputPreview.gameObject.SetActive(false);
            dropdownPreview.gameObject.SetActive(false);
            scrollPreview.gameObject.SetActive(false);
            sliderPreview.gameObject.SetActive(false);
            togglePreview.gameObject.SetActive(false);
            activePreview = null;
        }

        public void RefreshPreview()
        {
            activePreview?.ReapplyTheme();
            background?.Refresh(false);
        }

        private void FitText()
        {
            textFit?.Fit();
        }

        private void OnDestroy()
        {
            background?.Dispose();
        }
    }

    internal sealed class ThemeTestCardBackground
    {
        private readonly TMP_Dropdown dropdown;
        private readonly UIThemeBackground preview;
        private readonly List<UIThemeBackgroundType> types = new();
        private readonly List<UIThemeBackgroundType> nextTypes = new();
        private readonly List<TMP_Dropdown.OptionData> options = new();
        private UIThemeData theme;

        public ThemeTestCardBackground(TMP_Dropdown _dropdown, UIThemeBackground _preview)
        {
            dropdown = _dropdown;
            preview = _preview;
            dropdown.onValueChanged.AddListener(OnSelectionChanged);
        }

        public void Refresh(bool _resetSelection)
        {
            UIThemeManager manager = UIThemeManager.instance;
            UIThemeData selectedTheme = null;
            if (manager != null)
            {
                IReadOnlyList<UIThemeData> themes = manager.GetThemeDataList();
                for (int i = 0; i < themes.Count; i++)
                {
                    if (themes[i] != null && themes[i].ThemeType == manager.SelectedThemeType)
                    {
                        selectedTheme = themes[i];
                        break;
                    }
                }
            }

            nextTypes.Clear();
            if (selectedTheme != null)
            {
                IReadOnlyList<UIThemeBackgroundStyle> entries = selectedTheme.BackgroundStyles;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i] != null && !nextTypes.Contains(entries[i].BackgroundType))
                        nextTypes.Add(entries[i].BackgroundType);
                }
            }

            bool changed = _resetSelection || theme != selectedTheme || types.Count != nextTypes.Count;
            for (int i = 0; !changed && i < types.Count; i++)
                changed = types[i] != nextTypes[i];

            if (changed)
            {
                bool wasNone = dropdown.value == 0;
                UIThemeBackgroundType previous = dropdown.value > 0 && dropdown.value <= types.Count
                    ? types[dropdown.value - 1] : default;
                bool reset = _resetSelection || theme != selectedTheme || types.Count == 0;
                types.Clear();
                types.AddRange(nextTypes);
                options.Clear();
                options.Add(new TMP_Dropdown.OptionData("None"));
                for (int i = 0; i < types.Count; i++)
                    options.Add(new TMP_Dropdown.OptionData(types[i].ToString()));

                dropdown.Hide();
                dropdown.ClearOptions();
                dropdown.AddOptions(options);
                int selected = reset ? (types.Count > 0 ? 1 : 0)
                    : (wasNone ? 0 : types.IndexOf(previous) + 1);
                dropdown.SetValueWithoutNotify(selected);
                dropdown.RefreshShownValue();
                theme = selectedTheme;
            }

            OnSelectionChanged(dropdown.value);
        }

        private void OnSelectionChanged(int _index)
        {
            bool show = _index > 0 && _index <= types.Count;
            preview.gameObject.SetActive(show);
            if (show)
                preview.SetBackgroundType(types[_index - 1]);
        }

        public void Dispose()
        {
            if (dropdown != null)
                dropdown.onValueChanged.RemoveListener(OnSelectionChanged);
        }
    }

    internal sealed class ThemeTestCardTextFit
    {
        private readonly TMP_Text[] texts;
        private readonly float[] sizes;
        private readonly Vector2[] areas;
        private readonly string[] contents;
        private readonly TMP_FontAsset[] fonts;

        public ThemeTestCardTextFit(TMP_Text[] _texts)
        {
            texts = _texts;
            sizes = new float[texts.Length];
            areas = new Vector2[texts.Length];
            contents = new string[texts.Length];
            fonts = new TMP_FontAsset[texts.Length];
            for (int i = 0; i < texts.Length; i++)
                sizes[i] = texts[i].enableAutoSizing ? texts[i].fontSizeMax : texts[i].fontSize;
        }

        public void Fit()
        {
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || !text.isActiveAndEnabled)
                    continue;
                Vector2 area = text.rectTransform.rect.size;
                if (area.x <= 0f || area.y <= 0f)
                    continue;
                if (areas[i] == area && contents[i] == text.text && fonts[i] == text.font)
                    continue;

                ThemeTestTextFitter.Fit(text, sizes[i], 12f);
                areas[i] = area;
                contents[i] = text.text;
                fonts[i] = text.font;
            }
        }
    }
}
