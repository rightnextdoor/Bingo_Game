using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class NotificationManager : MonoBehaviour
{
    public static NotificationManager instance;

    private class NotificationRequest
    {
        public UIMessageData MessageData { get; }

        public NotificationRequest(UIMessageData messageData)
        {
            MessageData = messageData;
        }
    }

    [Header("UI References - Assign Later")]
    [SerializeField] private CanvasGroup notificationCanvasGroup;
    [SerializeField] private Image notificationBackground;
    [SerializeField] private TMP_Text notificationText;

    [Header("Fallback Timing")]
    [SerializeField] private float fallbackDisplaySeconds = 1.5f;
    [SerializeField] private float fallbackFadeOutSeconds = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool logWhenNoUIAssigned = true;

    private readonly Queue<NotificationRequest> notificationQueue = new Queue<NotificationRequest>();

    private Coroutine notificationRoutine;
    private bool isPlayingNotification;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("Duplicate NotificationManager found. Disabling this duplicate.");
            enabled = false;
            return;
        }

        instance = this;

        HideNotificationInstant();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void SendNotification(UIMessageData messageData)
    {
        if (messageData == null)
        {
            Debug.LogWarning("Cannot send notification because message data is null.");
            return;
        }

        notificationQueue.Enqueue(new NotificationRequest(messageData));

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

        UIMessageData messageData = request.MessageData;
        string message = messageData.BuildMessage();

        ApplyNotificationVisuals(messageData, message);

        if (!HasNotificationUI())
        {
            if (logWhenNoUIAssigned)
            {
                Debug.Log($"Notification: {message}");
            }

            yield break;
        }

        float displaySeconds = GetDisplaySeconds(messageData);
        float fadeOutSeconds = GetFadeOutSeconds(messageData);

        ShowNotificationInstant();

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

    private void ApplyNotificationVisuals(UIMessageData messageData, string message)
    {
        if (notificationText != null)
        {
            notificationText.richText = true;
            notificationText.text = message;

            if (messageData.FontAsset != null)
            {
                notificationText.font = messageData.FontAsset;
            }

            notificationText.fontSize = messageData.FontSize;
            notificationText.color = messageData.TextColor;
        }

        if (notificationBackground != null)
        {
            notificationBackground.color = messageData.BackgroundColor;
        }
    }

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
        if (notificationCanvasGroup != null)
        {
            notificationCanvasGroup.gameObject.SetActive(true);
            notificationCanvasGroup.alpha = 1f;
            notificationCanvasGroup.blocksRaycasts = false;
            notificationCanvasGroup.interactable = false;
        }
        else if (notificationText != null)
        {
            notificationText.gameObject.SetActive(true);
        }
    }

    private void HideNotificationInstant()
    {
        SetNotificationAlpha(0f);

        if (notificationCanvasGroup != null)
        {
            notificationCanvasGroup.gameObject.SetActive(false);
            notificationCanvasGroup.blocksRaycasts = false;
            notificationCanvasGroup.interactable = false;
        }
        else if (notificationText != null)
        {
            notificationText.gameObject.SetActive(false);
        }

        if (notificationText != null)
        {
            notificationText.text = string.Empty;
        }
    }

    private void SetNotificationAlpha(float alpha)
    {
        if (notificationCanvasGroup != null)
        {
            notificationCanvasGroup.alpha = alpha;
        }
    }

    private bool HasNotificationUI()
    {
        return notificationCanvasGroup != null && notificationText != null;
    }

    private float GetDisplaySeconds(UIMessageData messageData)
    {
        if (messageData.DisplaySeconds > 0f)
        {
            return messageData.DisplaySeconds;
        }

        return fallbackDisplaySeconds;
    }

    private float GetFadeOutSeconds(UIMessageData messageData)
    {
        if (messageData.FadeOutSeconds > 0f)
        {
            return messageData.FadeOutSeconds;
        }

        return fallbackFadeOutSeconds;
    }
}