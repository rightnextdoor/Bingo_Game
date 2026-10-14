using TMPro;
using UnityEngine;

namespace BingoGame.UI
{
    public readonly struct UITextFitResult
    {
        public float FontSize { get; }
        public int LineCount { get; }
        public Vector2 RequiredSize { get; }
        public bool Fits { get; }
        public bool Wraps { get; }

        internal UITextFitResult(float _fontSize, int _lineCount, Vector2 _requiredSize, bool _fits, bool _wraps)
        {
            FontSize = _fontSize;
            LineCount = _lineCount;
            RequiredSize = _requiredSize;
            Fits = _fits;
            Wraps = _wraps;
        }
    }

    public static class UITextFitter
    {
        private const float FitTolerance = 0.5f;

        public static UITextFitResult FitSingleLine(TMP_Text _text, string _content, float _preferredSize,
            Vector2 _availableSize, float _minimumSize)
        {
            return Fit(_text, _content, _preferredSize, _availableSize, _minimumSize, 1);
        }

        public static UITextFitResult FitMultipleLines(TMP_Text _text, string _content, float _preferredSize,
            Vector2 _availableSize, float _minimumSize, int _maximumLines)
        {
            return Fit(_text, _content, _preferredSize, _availableSize, _minimumSize, Mathf.Max(1, _maximumLines));
        }

        private static UITextFitResult Fit(TMP_Text _text, string _content, float _preferredSize,
            Vector2 _availableSize, float _minimumSize, int _maximumLines)
        {
            float preferredSize = Mathf.Max(1f, _preferredSize);
            float minimumSize = Mathf.Clamp(_minimumSize, 1f, preferredSize);
            if (_text == null || _text.font == null || _availableSize.x <= 0f || _availableSize.y <= 0f)
                return new UITextFitResult(preferredSize, 0, Vector2.zero, false, false);

            string originalText = _text.text;
            float originalSize = _text.fontSize;
            bool originalAutoSizing = _text.enableAutoSizing;
            TextWrappingModes originalWrapping = _text.textWrappingMode;
            TextOverflowModes originalOverflow = _text.overflowMode;
            int originalMaximumLines = _text.maxVisibleLines;
            int originalMaximumCharacters = _text.maxVisibleCharacters;
            int originalMaximumWords = _text.maxVisibleWords;
            Vector4 originalMargin = _text.margin;

            try
            {
                _text.text = _content ?? string.Empty;
                _text.enableAutoSizing = false;
                _text.overflowMode = TextOverflowModes.Overflow;
                _text.maxVisibleLines = int.MaxValue;
                _text.maxVisibleCharacters = int.MaxValue;
                _text.maxVisibleWords = int.MaxValue;

                UITextFitResult result;
                float size = preferredSize;
                while (true)
                {
                    result = Measure(_text, size, _availableSize, originalMargin, false, 1);
                    if (result.Fits) return result;
                    if (_maximumLines > 1)
                    {
                        result = Measure(_text, size, _availableSize, originalMargin, true, _maximumLines);
                        if (result.Fits) return result;
                    }
                    if (size <= minimumSize) return result;
                    size = Mathf.Max(minimumSize, size - 1f);
                }
            }
            finally
            {
                _text.text = originalText;
                _text.fontSize = originalSize;
                _text.enableAutoSizing = originalAutoSizing;
                _text.textWrappingMode = originalWrapping;
                _text.overflowMode = originalOverflow;
                _text.maxVisibleLines = originalMaximumLines;
                _text.maxVisibleCharacters = originalMaximumCharacters;
                _text.maxVisibleWords = originalMaximumWords;
                _text.margin = originalMargin;
                _text.ForceMeshUpdate(true, true);
            }
        }

        private static UITextFitResult Measure(TMP_Text _text, float _size, Vector2 _availableSize,
            Vector4 _originalMargin, bool _wrap, int _maximumLines)
        {
            _text.fontSize = _size;
            _text.textWrappingMode = _wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            Vector2 usableSize = new Vector2(
                Mathf.Max(0f, _availableSize.x - _originalMargin.x - _originalMargin.z),
                Mathf.Max(0f, _availableSize.y - _originalMargin.y - _originalMargin.w));

            _text.margin = Vector4.zero;
            Vector2 requiredSize = _text.GetPreferredValues(_text.text, _wrap ? Mathf.Max(0.01f, usableSize.x) : 0f, 0f);
            Vector4 measurementMargin = _originalMargin;
            measurementMargin.z += _text.rectTransform.rect.width - _availableSize.x;
            _text.margin = measurementMargin;
            _text.ForceMeshUpdate(true, true);
            int lines = string.IsNullOrEmpty(_text.text) ? 0 : _text.textInfo.lineCount;
            bool fits = lines <= _maximumLines && requiredSize.x <= usableSize.x + FitTolerance &&
                requiredSize.y <= usableSize.y + FitTolerance;
            Vector2 totalSize = requiredSize + new Vector2(_originalMargin.x + _originalMargin.z,
                _originalMargin.y + _originalMargin.w);
            return new UITextFitResult(_size, lines, totalSize, fits, _wrap);
        }
    }
}
