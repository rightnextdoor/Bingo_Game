using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BingoGame.UI.ThemeTest
{
    [DisallowMultipleComponent]
    public sealed class ThemeTestSelectorController : MonoBehaviour
    {
        [Header("Selector UI")]
        [Tooltip("Text displaying the theme type name.")]
        [SerializeField] private TMP_Text themeNameText;
        [Tooltip("Toggle used as the selector's radio button.")]
        [SerializeField] private Toggle radioButton;

        public event Action<UIThemeType> Selected;

        public UIThemeType ThemeType { get; private set; }
        public bool HasRequiredUI => themeNameText != null && radioButton != null;

        private void OnEnable()
        {
            if (radioButton == null)
                return;

            radioButton.onValueChanged.RemoveListener(OnValueChanged);
            radioButton.onValueChanged.AddListener(OnValueChanged);
        }

        private void OnDisable()
        {
            if (radioButton != null)
                radioButton.onValueChanged.RemoveListener(OnValueChanged);
        }

        public void Configure(UIThemeType themeType, ToggleGroup toggleGroup)
        {
            ThemeType = themeType;
            themeNameText.text = themeType.ToString();
            radioButton.group = null;
            radioButton.SetIsOnWithoutNotify(false);
            radioButton.group = toggleGroup;
        }

        public void SetSelectedWithoutNotify(bool selected)
        {
            radioButton.SetIsOnWithoutNotify(selected);
        }

        private void OnValueChanged(bool isOn)
        {
            if (isOn)
                Selected?.Invoke(ThemeType);
        }
    }
}
