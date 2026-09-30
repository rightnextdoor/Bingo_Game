using TMPro;
using UnityEngine;

public static class ThemeTestTextFitter
{
    public static void Fit(TMP_Text text, float requestedSize, float minimumSize)
    {
        if (text == null)
            return;

        Vector2 area = text.rectTransform.rect.size;
        if (area.x <= 0f || area.y <= 0f)
            return;

        float maximum = Mathf.Max(1f, requestedSize);
        float minimum = Mathf.Min(maximum, Mathf.Max(1f, minimumSize));
        text.enableAutoSizing = false;
        text.fontSize = maximum;
        text.maxVisibleLines = int.MaxValue;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;

        Vector2 preferred = text.GetPreferredValues(text.text, 0f, 0f);
        if (Fits(preferred, area))
        {
            text.overflowMode = TextOverflowModes.Ellipsis;
            return;
        }

        text.textWrappingMode = TextWrappingModes.Normal;
        for (float size = maximum; size > minimum; size -= 1f)
        {
            text.fontSize = size;
            preferred = text.GetPreferredValues(text.text, area.x, 0f);
            if (Fits(preferred, area))
            {
                text.overflowMode = TextOverflowModes.Ellipsis;
                return;
            }
        }

        text.fontSize = minimum;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static bool Fits(Vector2 preferred, Vector2 available)
    {
        return preferred.x <= available.x + 0.5f && preferred.y <= available.y + 0.5f;
    }
}
