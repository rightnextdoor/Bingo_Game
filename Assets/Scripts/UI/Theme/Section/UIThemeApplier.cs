using TMPro;
using UnityEngine.UI;

public static class UIThemeApplier
{
    public static void ApplyImageStyle(Image image, UIThemeStyle style)
    {
        if (image == null || style == null)
        {
            return;
        }

        if (style.SourceImage != null)
        {
            image.sprite = style.SourceImage;
        }

        image.color = style.Color;

        if (style.Material != null)
        {
            image.material = style.Material;
        }

        image.raycastTarget = style.RaycastTarget;
    }

    public static void ApplySelectableVisualStyle(Selectable selectable, UIThemeStyle style)
    {
        if (selectable == null || style == null)
        {
            return;
        }

        selectable.transition = style.Transition;

        ColorBlock colors = selectable.colors;
        colors.normalColor = style.NormalColor;
        colors.highlightedColor = style.HighlightedColor;
        colors.pressedColor = style.PressedColor;
        colors.selectedColor = style.SelectedColor;
        colors.disabledColor = style.DisabledColor;
        colors.colorMultiplier = style.ColorMultiplier;
        colors.fadeDuration = style.FadeDuration;

        selectable.colors = colors;
    }

    public static void ApplyTextStyle(TMP_Text text, UIThemeStyle style)
    {
        if (text == null || style == null)
        {
            return;
        }

        if (style.FontAsset != null)
        {
            text.font = style.FontAsset;
        }

        if (style.TextMaterial != null)
        {
            text.fontMaterial = style.TextMaterial;
        }

        text.color = style.VertexColor;

        if (style.ColorGradient != null)
        {
            text.colorGradientPreset = style.ColorGradient;
            text.enableVertexGradient = true;
        }
        else
        {
            text.enableVertexGradient = false;
        }

        text.raycastTarget = style.TextRaycastTarget;
    }
}