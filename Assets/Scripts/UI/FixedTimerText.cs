using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace BingoGame.UI
{
    internal sealed class FixedTimerText
    {
        private readonly TMP_Text text;
        private readonly RectTransform rect;
        private readonly bool originalAutoSizing;
        private readonly bool originalRichText;
        private readonly float originalFontSize;
        private readonly float originalMaximumFontSize;
        private readonly float originalMinimumFontSize;
        private readonly float originalWidthAdjustment;
        private readonly int originalMaximumLines;
        private readonly HorizontalAlignmentOptions originalHorizontalAlignment;
        private readonly TextWrappingModes originalWrappingMode;

        private TMP_FontAsset layoutFont;
        private TMP_FontAsset digitFont;
        private FontStyles digitStyle;
        private Vector2 layoutSize;
        private Vector4 layoutMargins;
        private string caption;
        private string displayText;
        private char widestDigit = '0';
        private float captionPosition;
        private float countdownPosition;
        private float fittedFontSize;
        private int maximumSeconds = -1;
        private int digitCount;
        private int displayedSeconds = -1;
        private bool minutesAndSeconds;
        private bool twoLines;
        private bool layoutReady;
        private bool timerFormattingApplied;

        public FixedTimerText(TMP_Text _text)
        {
            text = _text;
            if (text == null)
            {
                return;
            }

            rect = text.rectTransform;
            originalAutoSizing = text.enableAutoSizing;
            originalRichText = text.richText;
            originalFontSize = text.fontSize;
            originalMaximumFontSize = Mathf.Max(1f, originalAutoSizing ? text.fontSizeMax : text.fontSize);
            originalMinimumFontSize = Mathf.Clamp(text.fontSizeMin, 1f, originalMaximumFontSize);
            originalWidthAdjustment = text.characterWidthAdjustment;
            originalMaximumLines = text.maxVisibleLines;
            originalHorizontalAlignment = text.horizontalAlignment;
            originalWrappingMode = text.textWrappingMode;
        }

        public void SetSeconds(string _caption, int _seconds, bool _minutesAndSeconds = false)
        {
            if (text == null)
            {
                return;
            }

            _caption = _caption ?? string.Empty;
            _seconds = Mathf.Max(0, _seconds);
            bool captionChanged = !string.Equals(caption, _caption, StringComparison.Ordinal) ||
                minutesAndSeconds != _minutesAndSeconds;
            if (captionChanged)
            {
                ResetTimerState();
                caption = _caption;
                minutesAndSeconds = _minutesAndSeconds;
            }

            int previousDigitCount = digitCount;
            if (_seconds > maximumSeconds)
            {
                maximumSeconds = _seconds;
                digitCount = CountDigits(minutesAndSeconds ? _seconds / 60 : _seconds);
            }

            if (!text.gameObject.activeSelf)
            {
                text.gameObject.SetActive(true);
            }

            bool layoutChanged = ConfigureLayout(captionChanged || previousDigitCount != digitCount);
            if (!layoutReady)
            {
                return;
            }

            if (layoutChanged || displayedSeconds != _seconds)
            {
                displayedSeconds = _seconds;
                string countdown = FormatCountdown(_seconds);
                displayText = string.Format(
                    CultureInfo.InvariantCulture,
                    twoLines ? "<pos={0:0.###}>{1}\n<pos={2:0.###}>{3}" : "<pos={0:0.###}>{1}<pos={2:0.###}>{3}",
                    captionPosition, caption, countdownPosition, countdown);
                text.text = displayText;
            }
            else if (text.text != displayText)
            {
                text.text = displayText;
            }
        }

        public void Hide()
        {
            RestoreTextSettings();
            ResetTimerState();
            if (text != null && text.gameObject.activeSelf)
            {
                text.gameObject.SetActive(false);
            }
        }

        public void ShowPlain(string _value)
        {
            RestoreTextSettings();
            ResetTimerState();
            if (text == null)
            {
                return;
            }

            _value = _value ?? string.Empty;
            if (text.text != _value)
            {
                text.text = _value;
            }

            if (!text.gameObject.activeSelf)
            {
                text.gameObject.SetActive(true);
            }
        }

        private bool ConfigureLayout(bool _force)
        {
            if (rect == null || text.font == null)
            {
                layoutReady = false;
                return false;
            }

            Vector2 size = rect.rect.size;
            Vector4 margins = text.margin;
            float width = size.x - margins.x - margins.z;
            float height = size.y - margins.y - margins.w;
            if (width <= 0f || height <= 0f)
            {
                layoutReady = false;
                return false;
            }

            if (!_force && layoutReady && layoutFont == text.font && layoutSize == size &&
                layoutMargins == margins && digitStyle == text.fontStyle && !text.enableAutoSizing &&
                text.fontSize == fittedFontSize && text.horizontalAlignment == HorizontalAlignmentOptions.Left &&
                text.textWrappingMode == TextWrappingModes.NoWrap && text.richText)
            {
                return false;
            }

            layoutFont = text.font;
            layoutSize = size;
            layoutMargins = margins;
            text.enableAutoSizing = false;
            text.fontSize = originalMaximumFontSize;
            text.horizontalAlignment = HorizontalAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.characterWidthAdjustment = 0f;
            text.richText = true;
            text.maxVisibleLines = originalMaximumLines;

            UpdateWidestDigit();
            string referenceCountdown = new string(widestDigit, digitCount);
            if (minutesAndSeconds)
            {
                referenceCountdown += ":" + new string(widestDigit, 2);
            }

            string referenceText = caption + "  " + referenceCountdown;
            Vector2 referenceSize = Measure(referenceText);
            twoLines = originalMaximumLines > 1 &&
                referenceSize.x * originalMinimumFontSize / originalMaximumFontSize > width;
            if (twoLines)
            {
                referenceText = caption + "\n" + referenceCountdown;
                referenceSize = Measure(referenceText);
            }

            float scale = Mathf.Min(1f, width / Mathf.Max(1f, referenceSize.x), height / Mathf.Max(1f, referenceSize.y));
            fittedFontSize = Mathf.Clamp(originalMaximumFontSize * scale, originalMinimumFontSize, originalMaximumFontSize);
            text.fontSize = fittedFontSize;
            text.maxVisibleLines = twoLines ? 2 : 1;

            if (twoLines)
            {
                captionPosition = Mathf.Max(0f, (width - Measure(caption).x) * 0.5f);
                countdownPosition = Mathf.Max(0f, (width - Measure(referenceCountdown).x) * 0.5f);
            }
            else
            {
                captionPosition = Mathf.Max(0f, (width - Measure(referenceText).x) * 0.5f);
                float captionWidth = Measure(caption + "  " + widestDigit).x - Measure(widestDigit.ToString()).x;
                countdownPosition = captionPosition + captionWidth;
            }

            timerFormattingApplied = true;
            layoutReady = true;
            return true;
        }

        private void UpdateWidestDigit()
        {
            if (digitFont == text.font && digitStyle == text.fontStyle)
            {
                return;
            }

            digitFont = text.font;
            digitStyle = text.fontStyle;
            float widestWidth = 0f;
            for (char digit = '0'; digit <= '9'; digit++)
            {
                float width = Measure(digit.ToString()).x;
                if (width > widestWidth)
                {
                    widestDigit = digit;
                    widestWidth = width;
                }
            }
        }

        private Vector2 Measure(string _value)
        {
            Vector2 preferredSize = text.GetPreferredValues(_value);
            Vector4 margins = text.margin;
            preferredSize.x -= Mathf.Max(0f, margins.x) + Mathf.Max(0f, margins.z);
            preferredSize.y -= Mathf.Max(0f, margins.y) + Mathf.Max(0f, margins.w);
            return preferredSize;
        }

        private string FormatCountdown(int _seconds)
        {
            if (!minutesAndSeconds)
            {
                return _seconds.ToString(CultureInfo.InvariantCulture);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0}:{1:00}", _seconds / 60, _seconds % 60);
        }

        private static int CountDigits(int _value)
        {
            int digits = 1;
            while (_value >= 10)
            {
                _value /= 10;
                digits++;
            }

            return digits;
        }

        private void RestoreTextSettings()
        {
            if (!timerFormattingApplied || text == null)
            {
                return;
            }

            text.fontSize = originalFontSize;
            text.fontSizeMax = originalMaximumFontSize;
            text.fontSizeMin = originalMinimumFontSize;
            text.horizontalAlignment = originalHorizontalAlignment;
            text.textWrappingMode = originalWrappingMode;
            text.maxVisibleLines = originalMaximumLines;
            text.characterWidthAdjustment = originalWidthAdjustment;
            text.richText = originalRichText;
            text.enableAutoSizing = originalAutoSizing;
            timerFormattingApplied = false;
        }

        private void ResetTimerState()
        {
            caption = null;
            displayText = null;
            displayedSeconds = -1;
            maximumSeconds = -1;
            digitCount = 0;
            layoutReady = false;
        }
    }
}
