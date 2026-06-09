using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "UIMessageData", menuName = "Bingo Game/UI/UI Message Data")]
public class UIMessageData : ScriptableObject
{
    [Header("Message")]
    [TextArea(2, 8)]
    [SerializeField] private string messageText;

    [Header("Font")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private int fontSize = 24;

    [Header("Colors")]
    [SerializeField] private Color textColor = new Color32(241, 243, 245, 255);
    [SerializeField] private Color numberColor = new Color32(20, 20, 20, 255);
    [SerializeField] private Color backgroundColor = new Color32(58, 66, 80, 255);

    [Header("Notification Timing")]
    [SerializeField] private float displaySeconds = 1.5f;
    [SerializeField] private float fadeOutSeconds = 0.35f;

    [Header("Tooltip Position")]
    [SerializeField] private Vector2 tooltipOffset = new Vector2(18f, -18f);

    public string MessageText => messageText;
    public TMP_FontAsset FontAsset => fontAsset;
    public int FontSize => fontSize;
    public Color TextColor => textColor;
    public Color NumberColor => numberColor;
    public Color BackgroundColor => backgroundColor;
    public float DisplaySeconds => displaySeconds;
    public float FadeOutSeconds => fadeOutSeconds;
    public Vector2 TooltipOffset => tooltipOffset;

    public string BuildMessage(Dictionary<string, string> replacements = null)
    {
        string finalMessage = string.IsNullOrWhiteSpace(messageText) ? string.Empty : messageText;

        if (replacements == null)
        {
            return finalMessage;
        }

        foreach (KeyValuePair<string, string> replacement in replacements)
        {
            finalMessage = finalMessage.Replace($"{{{replacement.Key}}}", replacement.Value);
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

    public static UIMessageData CreateRuntimeMessage(
    string message,
    int fontSize,
    Color textColor,
    Color numberColor,
    Color backgroundColor,
    float displaySeconds,
    float fadeOutSeconds,
    Vector2 tooltipOffset)
    {
        UIMessageData data = CreateInstance<UIMessageData>();

        data.messageText = message;
        data.fontSize = fontSize;
        data.textColor = textColor;
        data.numberColor = numberColor;
        data.backgroundColor = backgroundColor;
        data.displaySeconds = displaySeconds;
        data.fadeOutSeconds = fadeOutSeconds;
        data.tooltipOffset = tooltipOffset;

        return data;
    }
}