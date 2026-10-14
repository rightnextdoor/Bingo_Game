using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BingoGame.UI.ThemeTest
{
    [DisallowMultipleComponent]
    public sealed class ThemeTestBoardCardController : MonoBehaviour
    {
        [Header("Card UI")]
        [Tooltip("Neutral board-entry title.")]
        [SerializeField] private TMP_Text cardTitle;
        [Tooltip("Dropdown UI control; options are populated from the selected theme.")]
        [SerializeField] private TMP_Dropdown backgroundDropdown;
        [Tooltip("UIThemeBackground on the separate backing Image.")]
        [SerializeField] private UIThemeBackground previewBackground;

        [Space]
        [Header("Board Preview")]
        [Tooltip("Existing UIThemeBoard on BoardPreview.")]
        [SerializeField] private UIThemeBoard boardPreview;
        [Tooltip("Checkbox controlling automatic pattern playback.")]
        [SerializeField] private Toggle playHighlightsToggle;
        [Tooltip("Scene-only highlight player for the preplaced cells.")]
        [SerializeField] private ThemeTestBoardHighlightPreview highlightPreview;

        private ThemeTestCardBackground background;
        private ThemeTestCardTextFit textFit;
        private bool configured;

        public bool HasRequiredUI => cardTitle != null && backgroundDropdown != null && previewBackground != null
            && boardPreview != null && playHighlightsToggle != null && highlightPreview != null;

        private void Awake()
        {
            if (!HasRequiredUI)
            {
                Debug.LogWarning("Assign all UI and board preview references on ThemeTestBoardCardController.", this);
                enabled = false;
                return;
            }

            background = new ThemeTestCardBackground(backgroundDropdown, previewBackground);
            textFit = new ThemeTestCardTextFit(GetComponentsInChildren<TMP_Text>(true));
        }

        private void OnEnable()
        {
            _ = CanvasUpdateRegistry.instance;
            Canvas.willRenderCanvases += FitText;
            if (playHighlightsToggle != null)
                playHighlightsToggle.onValueChanged.AddListener(OnPlaybackChanged);
            if (configured)
                OnPlaybackChanged(playHighlightsToggle.isOn);
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= FitText;
            if (playHighlightsToggle != null)
                playHighlightsToggle.onValueChanged.RemoveListener(OnPlaybackChanged);
            if (highlightPreview != null)
                highlightPreview.SetPlaying(false);
        }

        public void Configure(UIThemeBoardType _boardType)
        {
            if (background == null)
                return;

            cardTitle.text = _boardType.ToString();
            boardPreview.gameObject.SetActive(true);
            boardPreview.SetBoardType(_boardType);
            background.Refresh(true);
            configured = true;
            OnPlaybackChanged(playHighlightsToggle.isOn);
            RefreshPreview();
        }

        public void RefreshPreview()
        {
            if (!configured)
                return;
            boardPreview.ReapplyTheme();
            background.Refresh(false);
            highlightPreview.RefreshVisuals();
            playHighlightsToggle.SetIsOnWithoutNotify(highlightPreview.IsPlaying);
        }

        private void OnPlaybackChanged(bool _play)
        {
            if (!configured)
                return;
            highlightPreview.SetPlaying(_play);
            playHighlightsToggle.SetIsOnWithoutNotify(highlightPreview.IsPlaying);
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
}
