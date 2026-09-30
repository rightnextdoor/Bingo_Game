using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BingoGame.UI.ThemeTest
{
    [DisallowMultipleComponent]
    public sealed class ThemeTestSectionManager : MonoBehaviour
    {
        [Header("Sections")]
        [Tooltip("ThemeTestSection prefab with its title and inner Content assigned.")]
        [SerializeField] private ThemeTestSectionController sectionPrefab;
        [Tooltip("Outer vertical SectionsScrollView Content, shared with popup and message sections.")]
        [SerializeField] private RectTransform content;

        [Space]
        [Header("Play Mode Refresh")]
        [Tooltip("Seconds between checks for theme selection and section list changes.")]
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

        private static readonly UIThemeSectionType[] SectionOrder =
        {
            UIThemeSectionType.Background,
            UIThemeSectionType.Button,
            UIThemeSectionType.Text,
            UIThemeSectionType.Input,
            UIThemeSectionType.Dropdown,
            UIThemeSectionType.Scroll,
            UIThemeSectionType.Slider,
            UIThemeSectionType.Toggle,
            UIThemeSectionType.Board
        };

        private readonly ThemeTestSectionController[] sections = new ThemeTestSectionController[SectionOrder.Length];
        private UIThemeManager themeManager;
        private UIThemeData activeTheme;
        private UIThemeType activeThemeType;
        private float nextRefresh;

        private void Awake()
        {
            if (sectionPrefab == null || !sectionPrefab.HasRequiredUI || content == null)
            {
                Debug.LogWarning("Theme sections require a Section Prefab with its UI and Card Manager references, and the outer scroll Content.", this);
                enabled = false;
                return;
            }

            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                if (child.TryGetComponent(out ThemeTestSectionController setupSection))
                    RemoveSection(setupSection);
            }
        }

        private void Start()
        {
            themeManager = UIThemeManager.instance;
            RefreshSections();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh)
                return;

            RefreshSections();
        }

        private void RefreshSections()
        {
            nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            if (content == null || sectionPrefab == null)
                return;

            themeManager = ThemeTestSelectorSectionManager.ReadyThemeManager;
            if (themeManager == null || themeManager != UIThemeManager.instance || !themeManager.isActiveAndEnabled)
                return;

            UIThemeType selectedType = themeManager.SelectedThemeType;
            UIThemeData selectedTheme = GetSelectedTheme(selectedType);
            bool changed = selectedTheme != activeTheme || selectedType != activeThemeType;
            if (changed)
                ClearGeneratedSections();

            activeTheme = selectedTheme;
            activeThemeType = selectedType;

            for (int i = 0; i < SectionOrder.Length; i++)
            {
                bool shouldExist = HasSectionEntries(activeTheme, SectionOrder[i]);
                if (shouldExist && sections[i] == null)
                {
                    ThemeTestSectionController section = Instantiate(sectionPrefab, content, false);
                    section.gameObject.SetActive(true);
                    section.Configure(SectionOrder[i], activeTheme);
                    section.gameObject.name = SectionOrder[i] + " Section";
                    sections[i] = section;
                    changed = true;
                }
                else if (!shouldExist && sections[i] != null)
                {
                    RemoveSection(sections[i]);
                    sections[i] = null;
                    changed = true;
                }

                if (sections[i] != null)
                    sections[i].RefreshCards();
            }

            if (!changed)
                return;

            for (int i = 0; i < sections.Length; i++)
            {
                if (sections[i] != null)
                    sections[i].transform.SetAsLastSibling();
            }

            LayoutRebuilder.MarkLayoutForRebuild(content);
        }

        private UIThemeData GetSelectedTheme(UIThemeType _themeType)
        {
            IReadOnlyList<UIThemeData> themes = themeManager.GetThemeDataList();
            for (int i = 0; i < themes.Count; i++)
            {
                UIThemeData theme = themes[i];
                if (theme != null && theme.ThemeType == _themeType)
                    return theme;
            }

            return null;
        }

        private static bool HasSectionEntries(UIThemeData _theme, UIThemeSectionType _sectionType)
        {
            if (_theme == null)
                return false;

            switch (_sectionType)
            {
                case UIThemeSectionType.Background: return HasEntries(_theme.BackgroundStyles);
                case UIThemeSectionType.Button: return HasEntries(_theme.ButtonStyles);
                case UIThemeSectionType.Text: return HasEntries(_theme.TextStyles);
                case UIThemeSectionType.Input: return HasEntries(_theme.InputStyles);
                case UIThemeSectionType.Dropdown: return HasEntries(_theme.DropdownStyles);
                case UIThemeSectionType.Scroll: return HasEntries(_theme.ScrollStyles);
                case UIThemeSectionType.Slider: return HasEntries(_theme.SliderStyles);
                case UIThemeSectionType.Toggle: return HasEntries(_theme.ToggleStyles);
                case UIThemeSectionType.Board: return HasEntries(_theme.BoardStyles);
                default: return false;
            }
        }

        private static bool HasEntries<T>(IReadOnlyList<T> _entries) where T : class
        {
            if (_entries == null)
                return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i] != null)
                    return true;
            }

            return false;
        }

        private void ClearGeneratedSections()
        {
            for (int i = 0; i < sections.Length; i++)
            {
                if (sections[i] != null)
                    RemoveSection(sections[i]);
                sections[i] = null;
            }
        }

        private static void RemoveSection(ThemeTestSectionController _section)
        {
            _section.gameObject.SetActive(false);
            Destroy(_section.gameObject);
        }

        private void OnDestroy()
        {
            ClearGeneratedSections();
        }
    }
}
