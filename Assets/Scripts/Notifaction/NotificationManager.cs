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

    [Header("Notification UI")]
    [SerializeField] private CanvasGroup notificationAreaCanvasGroup;
    [SerializeField] private Image notificationBackground;
    [SerializeField] private TMP_Text notificationText;

    [Header("Queue Timing")]
    [SerializeField] private float delayBetweenMessages = 0.35f;

    private readonly Queue<NotificationRequest> notificationQueue = new Queue<NotificationRequest>();

    private Coroutine notificationRoutine;
    private bool isPlayingNotification;

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
            Debug.LogWarning("Cannot send notification because UIMessageData is null.");
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
        ApplyNotificationVisuals(messageData);

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

    private void ApplyNotificationVisuals(UIMessageData messageData)
    {
        if (notificationBackground != null)
        {
            notificationBackground.gameObject.SetActive(true);
            notificationBackground.color = messageData.BackgroundColor;
            notificationBackground.raycastTarget = false;
        }

        if (notificationText != null)
        {
            notificationText.gameObject.SetActive(true);
            notificationText.richText = true;
            notificationText.text = messageData.BuildMessage();

            if (messageData.FontAsset != null)
            {
                notificationText.font = messageData.FontAsset;
            }

            notificationText.fontSize = messageData.FontSize;
            notificationText.color = messageData.TextColor;

            notificationText.textWrappingMode = TextWrappingModes.Normal;
            notificationText.overflowMode = TextOverflowModes.Ellipsis;
            notificationText.enableAutoSizing = false;
            notificationText.alignment = TextAlignmentOptions.Center;
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
        return notificationAreaCanvasGroup != null &&
               notificationText != null;
    }
}