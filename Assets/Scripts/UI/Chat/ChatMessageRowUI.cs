using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatMessageRowUI : MonoBehaviour, IUIThemeTarget
{
    #region Fields

    [Header("Row")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private UIThemeBackground automaticBackgroundTheme;

    [Header("Message")]
    [SerializeField] private Image playerIconImage;
    [SerializeField] private TMP_Text messageText;

    [Header("Measurement")]
    [SerializeField, Min(0f)] private float horizontalTextPadding = 16f;
    [SerializeField, Min(0f)] private float verticalTextPadding = 8f;
    [SerializeField, Min(0f)] private float iconWidth = 36f;
    [SerializeField, Min(0f)] private float iconTextSpacing = 8f;
    [SerializeField, Min(1f)] private float minimumRowHeight = 48f;

    private ChatMessageData messageData;
    private ChatSettingsData settingsData;
    private string displayIdentity = string.Empty;
    private int messageIndex = -1;
    private UIThemeManager themeManager;

    public int MessageIndex => messageIndex;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        if (automaticBackgroundTheme != null)
        {
            automaticBackgroundTheme.enabled = false;
        }

        Clear();
    }

    private void OnEnable()
    {
        RegisterWithThemeManager();
    }

    private void OnDisable()
    {
        if (themeManager != null)
        {
            themeManager.Unregister(this);
            themeManager = null;
        }
    }

    #endregion

    #region Setup

    public void Setup(ChatMessageData message, string identity, int index, ChatSettingsData settings)
    {
        Clear();

        if (message == null)
        {
            return;
        }

        messageData = message;
        displayIdentity = string.IsNullOrWhiteSpace(identity) ? GetFallbackIdentity(message) : identity;
        messageIndex = index;
        settingsData = settings?.Clone() ?? new ChatSettingsData();

        if (messageText != null)
        {
            messageText.text = BuildMessageText(displayIdentity, message.message);
        }

        if (playerIconImage != null)
        {
            Sprite sprite = UIIconManager.instance != null ? UIIconManager.instance.GetPlayerIconSpriteById(message.senderIconId) : null;
            playerIconImage.sprite = sprite;
            playerIconImage.enabled = sprite != null;
            playerIconImage.preserveAspect = true;
        }

        ReapplyTheme();
    }

    public void Clear()
    {
        messageData = null;
        settingsData = null;
        displayIdentity = string.Empty;
        messageIndex = -1;

        if (messageText != null)
        {
            messageText.text = string.Empty;
        }

        if (playerIconImage != null)
        {
            playerIconImage.sprite = null;
            playerIconImage.enabled = false;
        }
    }

    #endregion

    #region Measurement

    public float MeasurePreferredHeight(ChatMessageData message, string identity, float rowWidth)
    {
        if (messageText == null || message == null)
        {
            return minimumRowHeight;
        }

        string finalIdentity = string.IsNullOrWhiteSpace(identity) ? GetFallbackIdentity(message) : identity;
        string finalText = BuildMessageText(finalIdentity, message.message);

        float availableWidth = Mathf.Max(1f, rowWidth - horizontalTextPadding - iconWidth - iconTextSpacing);
        Vector2 preferred = messageText.GetPreferredValues(finalText, availableWidth, 0f);

        return Mathf.Max(minimumRowHeight, preferred.y + verticalTextPadding);
    }

    private string BuildMessageText(string identity, string message)
    {
        string safeIdentity = string.IsNullOrWhiteSpace(identity) ? "Player" : identity.Trim();
        string safeMessage = message ?? string.Empty;
        return $"{safeIdentity}: {safeMessage}";
    }

    private string GetFallbackIdentity(ChatMessageData message)
    {
        if (message == null)
        {
            return "Player";
        }

        string playerName = string.IsNullOrWhiteSpace(message.senderPlayerName) ? "Player" : message.senderPlayerName.Trim();
        string userId = message.senderUserId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return playerName;
        }

        string shortId = userId.Length <= 4 ? userId : userId.Substring(0, 4);
        return $"{playerName} #{shortId}";
    }

    #endregion

    #region Theme

    private void RegisterWithThemeManager()
    {
        themeManager ??= UIThemeManager.instance;
        themeManager?.Register(this);
    }

    public void ReapplyTheme()
    {
        themeManager ??= UIThemeManager.instance;

        if (themeManager == null)
        {
            return;
        }

        UIThemeBackgroundType backgroundType = messageIndex >= 0 && (messageIndex % 2) != 0
            ? UIThemeBackgroundType.ChatMessageRowB
            : UIThemeBackgroundType.ChatMessageRowA;

        UIThemeStyle backgroundStyle = themeManager.GetBackgroundStyle(backgroundType);
        UIThemeApplier.ApplyImageStyle(backgroundImage, backgroundStyle);

        UIThemeTextType textType = GetTextType();
        UIThemeStyle textStyle = themeManager.GetTextStyle(textType);
        UIThemeApplier.ApplyTextStyle(messageText, textStyle);

        ApplyEffectiveTextColor(textType, textStyle);
    }

    private UIThemeTextType GetTextType()
    {
        if (messageData != null && messageData.isPrivate)
        {
            return UIThemeTextType.ChatPrivate;
        }

        return messageData != null && messageData.isFromCurrentUser
            ? UIThemeTextType.ChatCurrentUser
            : UIThemeTextType.ChatOtherUser;
    }

    private void ApplyEffectiveTextColor(UIThemeTextType textType, UIThemeStyle textStyle)
    {
        if (messageText == null)
        {
            return;
        }

        Color color = textStyle != null ? textStyle.VertexColor : messageText.color;
        ChatSettingsData settings = settingsData ?? ChatSettingsManager.instance?.CurrentSettings;

        if (settings != null)
        {
            switch (textType)
            {
                case UIThemeTextType.ChatCurrentUser:
                    if (settings.overrideCurrentUserMessageColor)
                    {
                        color = settings.currentUserMessageColor;
                    }
                    break;

                case UIThemeTextType.ChatOtherUser:
                    if (settings.overrideOtherUserMessageColor)
                    {
                        color = settings.otherUserMessageColor;
                    }
                    break;

                case UIThemeTextType.ChatPrivate:
                    if (settings.overridePrivateMessageColor)
                    {
                        color = settings.privateMessageColor;
                    }
                    break;
            }
        }

        color.a = 1f;
        messageText.color = color;
    }

    #endregion
}
