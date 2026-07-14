using System.Collections.Generic;
using TMPro;
using UnityEngine;

[System.Serializable]
public class TooltipVisualStyle
{
    #region Fields

    [SerializeField] private string messageText = string.Empty;

    [SerializeField] private TooltipImageMode imageMode = TooltipImageMode.Default;
    [SerializeField] private Sprite customImage;

    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private int fontSize = 24;

    [SerializeField]
    private Color textColor =
        new Color32(241, 243, 245, 255);

    [SerializeField]
    private Color numberColor =
        new Color32(20, 20, 20, 255);

    [SerializeField]
    private Color backgroundColor =
        new Color32(58, 66, 80, 255);

    [SerializeField] private float displaySeconds = 1.5f;
    [SerializeField] private float fadeOutSeconds = 0.35f;

    [SerializeField]
    private Vector2 tooltipOffset =
        new Vector2(18f, -18f);

    #endregion

    #region Properties

    public string MessageText => messageText;
    public TooltipImageMode ImageMode => imageMode;
    public Sprite CustomImage => customImage;
    public TMP_FontAsset FontAsset => fontAsset;
    public int FontSize => fontSize;

    public Color TextColor => textColor;
    public Color NumberColor => numberColor;
    public Color BackgroundColor => backgroundColor;

    public float DisplaySeconds => displaySeconds;
    public float FadeOutSeconds => fadeOutSeconds;

    public Vector2 TooltipOffset => tooltipOffset;

    #endregion

    #region Setters

    public TooltipVisualStyle SetMessage(string message)
    {
        messageText = message ?? string.Empty;
        return this;
    }

    public TooltipVisualStyle SetImageMode(
    TooltipImageMode newImageMode)
    {
        imageMode = newImageMode;

        if (imageMode != TooltipImageMode.Custom)
        {
            customImage = null;
        }

        return this;
    }

    public TooltipVisualStyle SetImage(Sprite newImage)
    {
        if (newImage == null)
        {
            imageMode = TooltipImageMode.Default;
            customImage = null;
            return this;
        }

        imageMode = TooltipImageMode.Custom;
        customImage = newImage;

        return this;
    }

    public TooltipVisualStyle UseDefaultImage()
    {
        imageMode = TooltipImageMode.Default;
        customImage = null;

        return this;
    }

    public TooltipVisualStyle RemoveImage()
    {
        imageMode = TooltipImageMode.None;
        customImage = null;

        return this;
    }

    public TooltipVisualStyle SetFont(TMP_FontAsset newFontAsset)
    {
        fontAsset = newFontAsset;
        return this;
    }

    public TooltipVisualStyle SetFontSize(int newFontSize)
    {
        fontSize = Mathf.Max(1, newFontSize);
        return this;
    }

    public TooltipVisualStyle SetTextColor(Color newTextColor)
    {
        textColor = newTextColor;
        return this;
    }

    public TooltipVisualStyle SetNumberColor(Color newNumberColor)
    {
        numberColor = newNumberColor;
        return this;
    }

    public TooltipVisualStyle SetBackgroundColor(Color newBackgroundColor)
    {
        backgroundColor = newBackgroundColor;
        return this;
    }

    public TooltipVisualStyle SetDisplaySeconds(float newDisplaySeconds)
    {
        displaySeconds = Mathf.Max(0f, newDisplaySeconds);
        return this;
    }

    public TooltipVisualStyle SetFadeOutSeconds(float newFadeOutSeconds)
    {
        fadeOutSeconds = Mathf.Max(0f, newFadeOutSeconds);
        return this;
    }

    public TooltipVisualStyle SetTooltipOffset(Vector2 newTooltipOffset)
    {
        tooltipOffset = newTooltipOffset;
        return this;
    }

    #endregion

    #region Message

    public string BuildMessage(
        Dictionary<string, string> replacements = null)
    {
        string finalMessage = string.IsNullOrWhiteSpace(messageText)
            ? string.Empty
            : messageText;

        if (replacements == null)
        {
            return finalMessage;
        }

        foreach (KeyValuePair<string, string> replacement in replacements)
        {
            finalMessage = finalMessage.Replace(
                $"{{{replacement.Key}}}",
                replacement.Value
            );
        }

        return finalMessage;
    }

    public string GetTextColorHex()
    {
        return ColorUtility.ToHtmlStringRGBA(textColor);
    }

    public string GetNumberColorHex()
    {
        return ColorUtility.ToHtmlStringRGBA(numberColor);
    }

    #endregion
}