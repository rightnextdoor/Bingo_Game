using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BingoGame.UI.ThemeTest
{
    [DisallowMultipleComponent]
    public sealed class ThemeTestSelectorSectionManager : MonoBehaviour
    {
        [Header("Selector Section")]
        [Tooltip("Selector prefab with its text and radio button assigned.")]
        [SerializeField] private ThemeTestSelectorController selectorPrefab;
        [Tooltip("Content of the top theme selector scroll view. Its setup children are removed in Play mode.")]
        [SerializeField] private RectTransform content;
        [Tooltip("Shared ToggleGroup for the generated theme selectors.")]
        [SerializeField] private ToggleGroup toggleGroup;

        private readonly List<ThemeTestSelectorController> selectors = new();
        private readonly HashSet<UIThemeType> themeTypes = new();
        private UIThemeManager themeManager;
        private UIThemeType selectedThemeType;
        private bool hasSelection;
        private bool rebuildRequested = true;
        private float nextRetry;

        public static UIThemeManager ReadyThemeManager { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetReadyState()
        {
            ReadyThemeManager = null;
        }

        private void Awake()
        {
            if (!HasRequiredUI())
            {
                Debug.LogWarning("Theme selectors require a Selector Prefab with both UI references, Content, and a Toggle Group.", this);
                enabled = false;
                return;
            }

            toggleGroup.allowSwitchOff = false;
            ClearContent();
        }

        private void Start()
        {
            RebuildSelectors();
        }

        private void OnEnable()
        {
            rebuildRequested = true;
            nextRetry = 0f;
        }

        private void OnDisable()
        {
            if (ReadyThemeManager == themeManager)
                ReadyThemeManager = null;
        }

        private void Update()
        {
            if (!rebuildRequested && ReadyThemeManager != null
                && ReadyThemeManager == UIThemeManager.instance && ReadyThemeManager.isActiveAndEnabled)
                return;
            if (Time.unscaledTime < nextRetry)
                return;

            TryRebuildSelectors();
        }

        public void RebuildSelectors()
        {
            rebuildRequested = true;
            TryRebuildSelectors();
        }

        private void TryRebuildSelectors()
        {
            nextRetry = Time.unscaledTime + 0.25f;
            ReadyThemeManager = null;
            if (!HasRequiredUI())
                return;

            themeManager = UIThemeManager.instance;
            if (themeManager == null || !themeManager.isActiveAndEnabled)
                return;

            IReadOnlyList<UIThemeData> themes = themeManager.GetThemeDataList();
            bool hasDefault = false;
            for (int i = 0; i < themes.Count; i++)
            {
                if (themes[i] != null && themes[i].ThemeType == UIThemeType.Default)
                {
                    hasDefault = true;
                    break;
                }
            }

            if (!hasDefault)
                return;

            ClearContent();
            themeTypes.Clear();
            hasSelection = false;
            toggleGroup.allowSwitchOff = false;

            AddSelector(UIThemeType.Default);
            for (int i = 0; i < themes.Count; i++)
            {
                UIThemeData theme = themes[i];
                if (theme != null)
                    AddSelector(theme.ThemeType);
            }

            toggleGroup.SetAllTogglesOff(false);
            selectors[0].SetSelectedWithoutNotify(true);
            ApplySelection(UIThemeType.Default);
            rebuildRequested = false;
            LayoutRebuilder.MarkLayoutForRebuild(content);
        }

        private bool HasRequiredUI()
        {
            return selectorPrefab != null && selectorPrefab.HasRequiredUI
                && content != null && toggleGroup != null;
        }

        private void AddSelector(UIThemeType _themeType)
        {
            if (!themeTypes.Add(_themeType))
                return;

            ThemeTestSelectorController selector = Instantiate(selectorPrefab, content, false);
            selector.Configure(_themeType, toggleGroup);
            selector.Selected += ApplySelection;
            selector.gameObject.SetActive(true);
            selectors.Add(selector);
        }

        private void ApplySelection(UIThemeType _themeType)
        {
            if (themeManager == null || !themeManager.isActiveAndEnabled
                || (hasSelection && selectedThemeType == _themeType))
                return;

            ReadyThemeManager = null;
            themeManager.SetTheme(_themeType);
            selectedThemeType = _themeType;
            hasSelection = true;
            ReadyThemeManager = themeManager;
        }

        private void ClearContent()
        {
            ReleaseSelectors(false);
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                GameObject child = content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private void ReleaseSelectors(bool _destroyObjects)
        {
            for (int i = 0; i < selectors.Count; i++)
            {
                ThemeTestSelectorController selector = selectors[i];
                if (selector == null)
                    continue;

                selector.Selected -= ApplySelection;
                if (_destroyObjects)
                {
                    selector.gameObject.SetActive(false);
                    Destroy(selector.gameObject);
                }
            }

            selectors.Clear();
        }

        private void OnDestroy()
        {
            ReleaseSelectors(true);
        }
    }
}
