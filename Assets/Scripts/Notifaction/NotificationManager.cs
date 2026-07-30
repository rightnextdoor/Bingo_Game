using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class NotificationManager : MonoBehaviour
{
    public static NotificationManager instance;

    #region Data

    private class NotificationRequest
    {
        public UIMessageData MessageData { get; }
        public string MessageOverride { get; }

        public NotificationRequest(UIMessageData messageData, string messageOverride)
        {
            MessageData = messageData;
            MessageOverride = messageOverride;
        }
    }

    #endregion

    #region Fields

    [Header("Notification UI")]
    [SerializeField] private CanvasGroup notificationAreaCanvasGroup;
    [SerializeField] private Image notificationBackground;
    [SerializeField] private TMP_Text notificationText;
    private Sprite defaultBackgroundImage;

    [Header("Notification Size")]
    [SerializeField, Min(1f)] private float maximumWidth = 850f;
    [SerializeField, Min(1)] private int minimumFontSize = 24;

    [Header("Queue Timing")]
    [SerializeField] private float delayBetweenMessages = 0.35f;

    private RectTransform notificationAreaRect;

    private readonly Queue<NotificationRequest> notificationQueue = new Queue<NotificationRequest>();

    private Coroutine notificationRoutine;
    private bool isPlayingNotification;

    #endregion

    #region Unity Lifecycle

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;

        notificationAreaRect = notificationAreaCanvasGroup != null ? notificationAreaCanvasGroup.transform as RectTransform : null;

        SaveDefaultBackgroundImage();
        HideNotificationInstant();
    }

    private void SaveDefaultBackgroundImage()
    {
        if (notificationBackground != null)
        {
            defaultBackgroundImage = notificationBackground.sprite;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Notifications

    public void SendNotification(UIMessageType messageType, string messageOverride = null)
    {
        if (UIMessageCatalog.instance == null)
        {
            Debug.LogWarning("Cannot send notification because UIMessageCatalog.instance was not found.");
            return;
        }

        UIMessageData messageData = UIMessageCatalog.instance.GetMessage(messageType);

        if (messageData == null)
        {
            return;
        }

        notificationQueue.Enqueue(new NotificationRequest(messageData, messageOverride));

        if (!isPlayingNotification)
        {
            notificationRoutine = StartCoroutine(ProcessNotificationQueue());
        }
    }

    private IEnumerator ProcessNotificationQueue()
    {
        isPlayingNotification = true;

        while (notificationQueue.Count > 0)
        {
            NotificationRequest request = notificationQueue.Dequeue();

            yield return PlayNotification(request);

            if (notificationQueue.Count > 0 && delayBetweenMessages > 0f)
            {
                yield return new WaitForSecondsRealtime(delayBetweenMessages);
            }
        }

        isPlayingNotification = false;
        notificationRoutine = null;
    }

    private IEnumerator PlayNotification(NotificationRequest request)
    {
        if (request == null || request.MessageData == null)
        {
            yield break;
        }

        if (!HasRequiredUI())
        {
            Debug.LogWarning("NotificationManager is missing NotificationArea CanvasGroup or NotificationText.");
            yield break;
        }

        UIMessageData messageData = request.MessageData;

        ShowNotificationInstant();
        ApplyNotificationVisuals(messageData, request.MessageOverride);

        float displaySeconds = Mathf.Max(0f, messageData.DisplaySeconds);
        float fadeOutSeconds = Mathf.Max(0f, messageData.FadeOutSeconds);

        if (displaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(displaySeconds);
        }

        if (fadeOutSeconds > 0f)
        {
            yield return FadeOutNotification(fadeOutSeconds);
        }

        HideNotificationInstant();
    }

    private void ApplyNotificationVisuals(UIMessageData messageData, string messageOverride)
    {
        if (notificationBackground != null)
        {
            notificationBackground.gameObject.SetActive(true);
            notificationBackground.color = messageData.BackgroundColor;
            notificationBackground.raycastTarget = false;

            ApplyBackgroundImage(messageData.ImageMode, messageData.CustomImage);
        }

        if (notificationText == null)
        {
            return;
        }

        string message = string.IsNullOrWhiteSpace(messageOverride) ? messageData.BuildMessage() : messageOverride;

        notificationText.gameObject.SetActive(true);
        notificationText.richText = true;

        if (messageData.FontAsset != null)
        {
            notificationText.font = messageData.FontAsset;
        }

        notificationText.color = messageData.TextColor;
        notificationText.enableAutoSizing = false;
        notificationText.alignment = TextAlignmentOptions.Center;
        notificationText.overflowMode = TextOverflowModes.Ellipsis;
        notificationText.text = message;

        FitNotificationToMessage(message, messageData.FontSize);
    }

    private void FitNotificationToMessage(string message, int requestedFontSize)
    {
        if (notificationAreaRect == null || notificationText == null)
        {
            return;
        }

        RectTransform textRect = notificationText.rectTransform;

        float leftPadding = textRect.offsetMin.x;
        float rightPadding = -textRect.offsetMax.x;
        float horizontalPadding = leftPadding + rightPadding;

        float maxTextWidth = Mathf.Max(1f, maximumWidth - horizontalPadding);

        int startingFontSize = Mathf.Max(minimumFontSize, requestedFontSize);
        int resolvedFontSize = startingFontSize;

        notificationText.textWrappingMode = TextWrappingModes.NoWrap;

        while (resolvedFontSize > minimumFontSize)
        {
            notificationText.fontSize = resolvedFontSize;

            Vector2 preferredSize = notificationText.GetPreferredValues(message, 0f, 0f);

            if (preferredSize.x <= maxTextWidth)
            {
                break;
            }

            resolvedFontSize--;
        }

        notificationText.fontSize = resolvedFontSize;

        Vector2 finalPreferredSize = notificationText.GetPreferredValues(message, 0f, 0f);

        if (finalPreferredSize.x <= maxTextWidth)
        {
            float notificationWidth = finalPreferredSize.x + horizontalPadding;

            notificationAreaRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Min(notificationWidth, maximumWidth));

            notificationText.textWrappingMode = TextWrappingModes.NoWrap;
            return;
        }

        notificationAreaRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maximumWidth);
        notificationText.fontSize = minimumFontSize;
        notificationText.textWrappingMode = TextWrappingModes.Normal;
    }

    private void ApplyBackgroundImage(TooltipImageMode imageMode, Sprite customImage)
    {
        if (notificationBackground == null)
        {
            return;
        }

        switch (imageMode)
        {
            case TooltipImageMode.Default:
                notificationBackground.sprite = defaultBackgroundImage;
                break;

            case TooltipImageMode.Custom:
                notificationBackground.sprite = customImage != null ? customImage : defaultBackgroundImage;
                break;

            case TooltipImageMode.None:
                notificationBackground.sprite = null;
                break;
        }
    }

    #endregion

    #region Display

    private IEnumerator FadeOutNotification(float fadeOutSeconds)
    {
        float elapsed = 0f;

        while (elapsed < fadeOutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsed / fadeOutSeconds);
            float alpha = Mathf.Lerp(1f, 0f, progress);

            SetNotificationAlpha(alpha);

            yield return null;
        }

        SetNotificationAlpha(0f);
    }

    private void ShowNotificationInstant()
    {
        GameObject notificationArea = notificationAreaCanvasGroup.gameObject;

        if (!notificationArea.activeSelf)
        {
            notificationArea.SetActive(true);
        }

        notificationAreaCanvasGroup.alpha = 1f;
        notificationAreaCanvasGroup.blocksRaycasts = false;
        notificationAreaCanvasGroup.interactable = false;
    }

    private void HideNotificationInstant()
    {
        if (notificationText != null)
        {
            notificationText.text = string.Empty;
        }

        if (notificationAreaCanvasGroup != null)
        {
            notificationAreaCanvasGroup.alpha = 0f;
            notificationAreaCanvasGroup.blocksRaycasts = false;
            notificationAreaCanvasGroup.interactable = false;
            notificationAreaCanvasGroup.gameObject.SetActive(false);
        }
    }

    private void SetNotificationAlpha(float alpha)
    {
        if (notificationAreaCanvasGroup != null)
        {
            notificationAreaCanvasGroup.alpha = alpha;
        }
    }

    private bool HasRequiredUI()
    {
        return notificationAreaCanvasGroup != null && notificationText != null;
    }

    #endregion
}