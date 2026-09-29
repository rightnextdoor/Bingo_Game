using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class UIMessageCatalog : MonoBehaviour
{
    public static UIMessageCatalog instance;

    #region Fields

    [SerializeField] private List<UIMessageData> messages = new List<UIMessageData>();
    [SerializeField] private UIMessageDefaultStyle defaultStyle;

    public UIMessageDefaultStyle DefaultStyle => defaultStyle;

    private static bool warnedAboutMissingDefault;

    #endregion

    #region Unity Lifecycle

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
        warnedAboutMissingDefault = false;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Messages

    /// <summary>
    /// Shared visual resolution for notifications, tooltips, and preview cards.
    /// None selects the complete catalog default; Default selects its image only.
    /// </summary>
    public static UIMessageVisualStyle ResolveVisualStyle(
        TooltipImageMode imageMode, Sprite customImage, int fontSize,
        Color textColor, Color backgroundColor)
    {
        UIMessageDefaultStyle fallback = instance != null ? instance.defaultStyle : null;
        bool needsDefault = imageMode != TooltipImageMode.Custom || customImage == null;
        if (fallback == null && needsDefault && !warnedAboutMissingDefault)
        {
            Debug.LogWarning("Assign a UIMessageDefaultStyle to UIMessageCatalog. " +
                "Messages will use a plain background until a default style is available.");
            warnedAboutMissingDefault = true;
        }
        else if (fallback != null)
        {
            warnedAboutMissingDefault = false;
        }

        Sprite image = fallback != null ? fallback.BackgroundImage : null;
        if (imageMode == TooltipImageMode.Custom && customImage != null)
            image = customImage;

        if (imageMode == TooltipImageMode.None)
        {
            // Deterministic defaults if setup is incomplete; never reuse prefab or previous-message styling.
            fontSize = fallback != null ? fallback.FontSize : 24;
            textColor = fallback != null ? fallback.TextColor : new Color32(241, 243, 245, 255);
            backgroundColor = fallback != null ? fallback.BackgroundColor : new Color32(58, 66, 80, 255);
        }

        return new UIMessageVisualStyle(Mathf.Max(1, fontSize), textColor, backgroundColor, image);
    }

    public UIMessageData GetMessage(UIMessageType messageType)
    {
        if (messageType == UIMessageType.None)
        {
            return null;
        }

        for (int i = 0; i < messages.Count; i++)
        {
            UIMessageData messageData = messages[i];

            if (messageData != null && messageData.MessageType == messageType)
            {
                return messageData;
            }
        }

        Debug.LogWarning($"UIMessageCatalog could not find UIMessageData for {messageType}.");
        return null;
    }

    #endregion
}

public readonly struct UIMessageVisualStyle
{
    public int FontSize { get; }
    public Color TextColor { get; }
    public Color BackgroundColor { get; }
    public Sprite BackgroundImage { get; }

    public UIMessageVisualStyle(int fontSize, Color textColor, Color backgroundColor, Sprite backgroundImage)
    {
        FontSize = fontSize;
        TextColor = textColor;
        BackgroundColor = backgroundColor;
        BackgroundImage = backgroundImage;
    }
}
