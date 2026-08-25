using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatMessageScrollController : MonoBehaviour
{
    #region Fields

    private const float ViewportPaddingMultiplier = 0.5f;
    private const float BottomPixelTolerance = 2f;

    [Header("Scroll View")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform viewport;

    [Header("Message")]
    [SerializeField] private ChatMessageRowUI messagePrefab;

    [Header("State UI")]
    [SerializeField] private TMP_Text emptyChatText;
    [SerializeField] private Button newMessagesButton;

    private readonly List<ChatMessageData> messages = new List<ChatMessageData>();
    private readonly List<MessagePresentation> presentations = new List<MessagePresentation>();
    private readonly List<float> messageHeights = new List<float>();
    private readonly List<float> messageOffsets = new List<float>();
    private readonly List<ChatMessageRowUI> messageViewPool = new List<ChatMessageRowUI>();
    private readonly List<int> boundMessageIndices = new List<int>();

    private ChatConversationData conversation;
    private ChatSettingsData userSettings = new ChatSettingsData();
    private ChatMessageRowUI measurementView;

    private float cachedContentWidth = -1f;
    private float cachedMessageTextSize = -1f;
    private float cachedMessageSpacing = -1f;
    private UIThemeType cachedThemeType;
    private bool hasCachedThemeType;

    private bool initialized;
    private bool suppressScrollRefresh;
    private bool followingBottom = true;
    private bool hasNewMessagesBelow;
    private bool hasSavedInactiveViewAnchor;
    private bool hasObservedNewestMessage;
    private ViewAnchor savedInactiveViewAnchor;
    private string observedNewestMessageId = string.Empty;

    public ChatConversationData Conversation => conversation;
    public bool IsFollowingBottom => followingBottom;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        ResolveReferences();
        ClearDisplay();
        InitializeContent();
    }

    private void OnEnable()
    {
        RegisterListeners();
        RegisterNewMessagesButton();
        RefreshUserSettings();

        if (conversation != null)
        {
            RefreshAfterInactive();
        }
        else
        {
            RefreshNewMessagesButton();
        }
    }

    private void Start()
    {
        RegisterListeners();
        RegisterNewMessagesButton();
    }

    private void OnDisable()
    {
        SaveInactiveViewState();
        UnregisterListeners();
        UnregisterNewMessagesButton();
    }

    private void OnDestroy()
    {
        UnregisterListeners();
        UnregisterNewMessagesButton();
        ClearMessageViewPool();
        DestroyMeasurementView();
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        float currentWidth = GetContentWidth();
        float currentTextSize = GetMessageTextSize();
        float currentSpacing = GetMessageSpacing();
        UIThemeType currentThemeType = UIThemeManager.instance != null ? UIThemeManager.instance.SelectedThemeType : default;

        bool layoutChanged =
            Mathf.Abs(currentWidth - cachedContentWidth) > 0.5f ||
            Mathf.Abs(currentTextSize - cachedMessageTextSize) > 0.01f ||
            Mathf.Abs(currentSpacing - cachedMessageSpacing) > 0.01f ||
            (hasCachedThemeType && currentThemeType != cachedThemeType);

        if (!layoutChanged)
        {
            if (!hasCachedThemeType && UIThemeManager.instance != null)
            {
                cachedThemeType = currentThemeType;
                hasCachedThemeType = true;
            }

            return;
        }

        bool keepBottomPinned = followingBottom || IsAtBottom();
        ViewAnchor anchor = keepBottomPinned ? default : CaptureViewAnchor();

        RebuildMessageLayout();

        if (keepBottomPinned)
        {
            ScrollToBottom();
        }
        else
        {
            RestoreViewAnchor(anchor);
        }
    }

    #endregion

    #region Setup

    private void ResolveReferences()
    {
        if (scrollRect == null)
        {
            scrollRect = GetComponentInChildren<ScrollRect>(true);
        }

        if (content == null && scrollRect != null)
        {
            content = scrollRect.content;
        }

        if (viewport == null && scrollRect != null)
        {
            viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
        }
    }

    private void InitializeContent()
    {
        initialized = scrollRect != null && content != null && viewport != null && messagePrefab != null;

        if (!initialized)
        {
            return;
        }

        Vector2 anchorMin = content.anchorMin;
        Vector2 anchorMax = content.anchorMax;
        Vector2 pivot = content.pivot;

        anchorMin.y = 1f;
        anchorMax.y = 1f;
        pivot.y = 1f;

        content.anchorMin = anchorMin;
        content.anchorMax = anchorMax;
        content.pivot = pivot;

        ClearExistingContentChildren();
        CreateMeasurementView();

        cachedContentWidth = GetContentWidth();
        cachedMessageTextSize = GetMessageTextSize();
        cachedMessageSpacing = GetMessageSpacing();

        if (UIThemeManager.instance != null)
        {
            cachedThemeType = UIThemeManager.instance.SelectedThemeType;
            hasCachedThemeType = true;
        }
    }

    private void ClearExistingContentChildren()
    {
        if (content == null)
        {
            return;
        }

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);

            if (child == null)
            {
                continue;
            }

            if (messagePrefab != null && child.gameObject == messagePrefab.gameObject)
            {
                child.gameObject.SetActive(false);
                continue;
            }

            child.gameObject.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    #endregion

    #region Listeners

    private void RegisterListeners()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
            scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }

        if (ChatManager.instance != null)
        {
            ChatManager.instance.ConversationMessagesChanged -= OnConversationMessagesChanged;
            ChatManager.instance.ConversationMessagesChanged += OnConversationMessagesChanged;
        }

        if (ChatSettingsManager.instance != null)
        {
            ChatSettingsManager.instance.ChatSettingsChanged -= OnUserChatSettingsChanged;
            ChatSettingsManager.instance.ChatSettingsChanged += OnUserChatSettingsChanged;
        }

        if (PlayerProfileRegistry.instance != null)
        {
            PlayerProfileRegistry.instance.ProfileChanged -= OnProfileChanged;
            PlayerProfileRegistry.instance.ProfileChanged += OnProfileChanged;
        }
    }

    private void UnregisterListeners()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
        }

        if (ChatManager.instance != null)
        {
            ChatManager.instance.ConversationMessagesChanged -= OnConversationMessagesChanged;
        }

        if (ChatSettingsManager.instance != null)
        {
            ChatSettingsManager.instance.ChatSettingsChanged -= OnUserChatSettingsChanged;
        }

        if (PlayerProfileRegistry.instance != null)
        {
            PlayerProfileRegistry.instance.ProfileChanged -= OnProfileChanged;
        }
    }

    private void RegisterNewMessagesButton()
    {
        if (newMessagesButton == null)
        {
            return;
        }

        newMessagesButton.onClick.RemoveListener(OnNewMessagesClicked);
        newMessagesButton.onClick.AddListener(OnNewMessagesClicked);
    }

    private void UnregisterNewMessagesButton()
    {
        if (newMessagesButton != null)
        {
            newMessagesButton.onClick.RemoveListener(OnNewMessagesClicked);
        }
    }

    #endregion

    #region Conversation

    public void SetConversation(ChatConversationData newConversation)
    {
        if (IsSameConversation(newConversation, conversation))
        {
            conversation = newConversation;
            RefreshConversation();
            return;
        }

        conversation = newConversation;
        followingBottom = true;
        hasSavedInactiveViewAnchor = false;
        hasObservedNewestMessage = false;
        observedNewestMessageId = string.Empty;
        SetHasNewMessagesBelow(false);

        RebuildMessageData();
        RebuildMessageLayout();
        ScrollToBottom();
        RecordObservedNewestMessage();
    }

    public void RefreshConversation()
    {
        if (conversation == null)
        {
            ClearDisplay();
            return;
        }

        bool keepBottomPinned = followingBottom || IsAtBottom();
        string previousNewestMessageId = GetNewestMessageId();
        ViewAnchor anchor = keepBottomPinned ? default : CaptureViewAnchor();

        RebuildMessageData();
        RebuildMessageLayout();

        string currentNewestMessageId = GetNewestMessageId();
        bool hasNewNewestMessage = !string.Equals(previousNewestMessageId, currentNewestMessageId, StringComparison.Ordinal);
        bool currentUserSentNewestMessage = HasCurrentUserSentNewestMessage(hasNewNewestMessage);

        if (keepBottomPinned || currentUserSentNewestMessage)
        {
            ScrollToBottom();
        }
        else
        {
            RestoreViewAnchor(anchor);

            if (hasNewNewestMessage)
            {
                SetHasNewMessagesBelow(true);
            }
        }

        RecordObservedNewestMessage();
    }

    private void RefreshAfterInactive()
    {
        bool keepBottomPinned = followingBottom;
        bool hasNewMessagesWhileInactive = HasConversationChangedSinceObserved();
        ViewAnchor anchor = hasSavedInactiveViewAnchor ? savedInactiveViewAnchor : CaptureViewAnchor();

        RebuildMessageData();
        RebuildMessageLayout();

        if (keepBottomPinned)
        {
            ScrollToBottom();
        }
        else
        {
            RestoreViewAnchor(anchor);

            if (hasNewMessagesWhileInactive)
            {
                SetHasNewMessagesBelow(true);
            }
            else
            {
                RefreshNewMessagesButton();
            }
        }

        hasSavedInactiveViewAnchor = false;
        RecordObservedNewestMessage();
    }

    private void SaveInactiveViewState()
    {
        if (!initialized || conversation == null)
        {
            return;
        }

        if (IsAtBottom())
        {
            followingBottom = true;
            hasSavedInactiveViewAnchor = false;
        }
        else
        {
            followingBottom = false;
            savedInactiveViewAnchor = CaptureViewAnchor();
            hasSavedInactiveViewAnchor = true;
        }

        RecordObservedNewestMessage();
    }

    public void ClearDisplay()
    {
        conversation = null;

        messages.Clear();
        presentations.Clear();
        messageHeights.Clear();
        messageOffsets.Clear();

        ReleaseAllMessageViews();

        if (emptyChatText != null)
        {
            emptyChatText.text = string.Empty;
            emptyChatText.gameObject.SetActive(false);
        }

        if (content != null)
        {
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        }

        followingBottom = true;
        hasSavedInactiveViewAnchor = false;
        hasObservedNewestMessage = false;
        observedNewestMessageId = string.Empty;
        SetHasNewMessagesBelow(false);
    }

    private void RebuildMessageData()
    {
        messages.Clear();

        if (conversation?.messages != null)
        {
            for (int i = 0; i < conversation.messages.Count; i++)
            {
                ChatMessageData message = conversation.messages[i];

                if (message != null)
                {
                    messages.Add(message);
                }
            }
        }

        UpdateEmptyState();
    }

    private void OnConversationMessagesChanged(ChatConversationData changedConversation)
    {
        if (!IsSameConversation(changedConversation, conversation))
        {
            return;
        }

        conversation = changedConversation;
        RefreshConversation();
    }

    private bool IsSameConversation(ChatConversationData first, ChatConversationData second)
    {
        return first != null && second != null && string.Equals(first.Key, second.Key, StringComparison.Ordinal);
    }

    #endregion

    #region Presentation / Layout

    private void RebuildMessageLayout()
    {
        if (!initialized)
        {
            return;
        }

        presentations.Clear();
        messageHeights.Clear();
        messageOffsets.Clear();

        float contentWidth = GetContentWidth();
        float textSize = GetMessageTextSize();
        float spacing = GetMessageSpacing();
        float currentOffset = 0f;

        cachedContentWidth = contentWidth;
        cachedMessageTextSize = textSize;
        cachedMessageSpacing = spacing;

        if (UIThemeManager.instance != null)
        {
            cachedThemeType = UIThemeManager.instance.SelectedThemeType;
            hasCachedThemeType = true;
        }

        List<PlayerProfileData> conversationProfiles = BuildConversationProfiles();
        ChatMessageRowUI measureView = GetMeasurementView();

        for (int i = 0; i < messages.Count; i++)
        {
            MessagePresentation presentation = BuildPresentation(messages[i], i, conversationProfiles);
            presentations.Add(presentation);

            float height = 1f;

            if (measureView != null)
            {
                ApplyPresentation(measureView, presentation, textSize);
                height = Mathf.Max(1f, measureView.GetPreferredHeight(contentWidth));
            }

            messageOffsets.Add(currentOffset);
            messageHeights.Add(height);

            currentOffset += height;

            if (i < messages.Count - 1)
            {
                currentOffset += spacing;
            }
        }

        SetContentHeight(currentOffset);
        RefreshVisibleMessageViews();
    }

    private MessagePresentation BuildPresentation(ChatMessageData message, int messageIndex, IReadOnlyList<PlayerProfileData> conversationProfiles)
    {
        UIThemeBackgroundType backgroundType = messageIndex % 2 == 0
            ? UIThemeBackgroundType.ChatMessageRowA
            : UIThemeBackgroundType.ChatMessageRowB;

        if (message == null)
        {
            return new MessagePresentation(null, string.Empty, backgroundType, UIThemeTextType.Default, false, Color.white, false);
        }

        if (message.isLocalSystemMessage)
        {
            return new MessagePresentation(
                null,
                message.message ?? string.Empty,
                backgroundType,
                UIThemeTextType.Error,
                false,
                Color.white,
                false);
        }

        string identity = ResolveMessageIdentity(message, conversationProfiles);
        string displayText = $"{identity}: {message.message ?? string.Empty}";
        Sprite icon = UIIconManager.instance != null ? UIIconManager.instance.GetPlayerIconSpriteById(message.senderIconId) : null;

        UIThemeTextType textType = GetMessageTextType(message);
        bool useColorOverride = TryGetColorOverride(textType, out Color colorOverride);

        return new MessagePresentation(
            icon,
            displayText,
            backgroundType,
            textType,
            useColorOverride,
            colorOverride,
            icon != null);
    }

    private void ApplyPresentation(ChatMessageRowUI view, MessagePresentation presentation, float textSize)
    {
        if (view == null)
        {
            return;
        }

        view.Setup(
            presentation.icon,
            presentation.text,
            textSize,
            presentation.backgroundType,
            presentation.textType,
            presentation.useColorOverride,
            presentation.colorOverride,
            presentation.showIcon);
    }

    private UIThemeTextType GetMessageTextType(ChatMessageData message)
    {
        if (message != null && message.isPrivate)
        {
            return UIThemeTextType.ChatPrivate;
        }

        return message != null && message.isFromCurrentUser
            ? UIThemeTextType.ChatCurrentUser
            : UIThemeTextType.ChatOtherUser;
    }

    private bool TryGetColorOverride(UIThemeTextType textType, out Color color)
    {
        color = Color.white;

        if (userSettings == null)
        {
            return false;
        }

        switch (textType)
        {
            case UIThemeTextType.ChatCurrentUser:
                color = userSettings.currentUserMessageColor;
                return userSettings.overrideCurrentUserMessageColor;

            case UIThemeTextType.ChatOtherUser:
                color = userSettings.otherUserMessageColor;
                return userSettings.overrideOtherUserMessageColor;

            case UIThemeTextType.ChatPrivate:
                color = userSettings.privateMessageColor;
                return userSettings.overridePrivateMessageColor;

            default:
                return false;
        }
    }

    private List<PlayerProfileData> BuildConversationProfiles()
    {
        List<PlayerProfileData> profiles = new List<PlayerProfileData>();

        if (conversation?.participants == null)
        {
            return profiles;
        }

        for (int i = 0; i < conversation.participants.Count; i++)
        {
            ChatParticipantData participant = conversation.participants[i];

            if (participant != null && participant.IsValid)
            {
                profiles.Add(new PlayerProfileData(participant.userId, participant.playerName, participant.iconId));
            }
        }

        return profiles;
    }

    private string ResolveMessageIdentity(ChatMessageData message, IReadOnlyList<PlayerProfileData> conversationProfiles)
    {
        if (message == null)
        {
            return "Player";
        }

        string playerName = string.IsNullOrWhiteSpace(message.senderPlayerName) ? "Player" : message.senderPlayerName.Trim();

        PlayerProfileData profile = PlayerProfileRegistry.instance?.GetProfile(message.senderUserId) ??
                                    new PlayerProfileData(message.senderUserId, playerName, message.senderIconId);

        return PlayerDisplayIdentityResolver.GetDisplayName(profile, conversationProfiles);
    }

    private void RefreshUserSettings()
    {
        userSettings = ChatSettingsManager.instance != null && ChatSettingsManager.instance.IsReady
            ? ChatSettingsManager.instance.CurrentSettings
            : new ChatSettingsData();
    }

    private float GetMessageTextSize()
    {
        return ChatSettings.instance != null ? ChatSettings.instance.MessageTextSize : 18f;
    }

    private float GetMessageSpacing()
    {
        return ChatSettings.instance != null ? ChatSettings.instance.MessageSpacing : 2f;
    }

    private float GetContentWidth()
    {
        if (content == null)
        {
            return 1f;
        }

        float width = content.rect.width;

        if (width <= 1f && viewport != null)
        {
            width = viewport.rect.width;
        }

        return Mathf.Max(1f, width);
    }

    private void SetContentHeight(float height)
    {
        if (content == null)
        {
            return;
        }

        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, height));
        ClampContentPosition();
    }

    #endregion

    #region Virtual Message Views

    private void RefreshVisibleMessageViews()
    {
        if (!initialized)
        {
            return;
        }

        if (messages.Count == 0)
        {
            ReleaseAllMessageViews();
            return;
        }

        float padding = viewport.rect.height * ViewportPaddingMultiplier;
        float viewTop = Mathf.Max(0f, content.anchoredPosition.y - padding);
        float viewBottom = content.anchoredPosition.y + viewport.rect.height + padding;

        int firstIndex = FindFirstVisibleIndex(viewTop);
        int lastIndex = FindLastVisibleIndex(viewBottom);
        int requiredCount = Mathf.Max(0, lastIndex - firstIndex + 1);

        EnsureMessageViewPoolCount(requiredCount);

        for (int poolIndex = 0; poolIndex < messageViewPool.Count; poolIndex++)
        {
            int messageIndex = poolIndex < requiredCount ? firstIndex + poolIndex : -1;
            BindMessageView(poolIndex, messageIndex);
        }
    }

    private int FindFirstVisibleIndex(float viewTop)
    {
        int low = 0;
        int high = messages.Count - 1;
        int result = high;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            float bottom = messageOffsets[mid] + messageHeights[mid];

            if (bottom >= viewTop)
            {
                result = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return Mathf.Clamp(result, 0, messages.Count - 1);
    }

    private int FindLastVisibleIndex(float viewBottom)
    {
        int low = 0;
        int high = messages.Count - 1;
        int result = 0;

        while (low <= high)
        {
            int mid = (low + high) / 2;

            if (messageOffsets[mid] <= viewBottom)
            {
                result = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return Mathf.Clamp(result, 0, messages.Count - 1);
    }

    private void EnsureMessageViewPoolCount(int desiredCount)
    {
        if (messagePrefab == null || content == null)
        {
            return;
        }

        while (messageViewPool.Count < desiredCount)
        {
            ChatMessageRowUI view = Instantiate(messagePrefab, content);
            view.gameObject.SetActive(false);
            view.Clear();

            messageViewPool.Add(view);
            boundMessageIndices.Add(-1);
        }
    }

    private void BindMessageView(int poolIndex, int messageIndex)
    {
        if (poolIndex < 0 || poolIndex >= messageViewPool.Count)
        {
            return;
        }

        ChatMessageRowUI view = messageViewPool[poolIndex];

        if (view == null)
        {
            return;
        }

        if (messageIndex < 0 || messageIndex >= messages.Count)
        {
            ReleaseMessageView(poolIndex);
            return;
        }

        RectTransform viewRect = view.transform as RectTransform;

        if (viewRect != null)
        {
            viewRect.anchorMin = new Vector2(0f, 1f);
            viewRect.anchorMax = new Vector2(1f, 1f);
            viewRect.pivot = new Vector2(0.5f, 1f);
            viewRect.anchoredPosition = new Vector2(0f, -messageOffsets[messageIndex]);
            viewRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, messageHeights[messageIndex]);
        }

        boundMessageIndices[poolIndex] = messageIndex;

        ApplyPresentation(view, presentations[messageIndex], GetMessageTextSize());
        view.gameObject.SetActive(true);
    }

    private void ReleaseMessageView(int poolIndex)
    {
        if (poolIndex < 0 || poolIndex >= messageViewPool.Count)
        {
            return;
        }

        boundMessageIndices[poolIndex] = -1;

        ChatMessageRowUI view = messageViewPool[poolIndex];

        if (view == null)
        {
            return;
        }

        view.Clear();
        view.gameObject.SetActive(false);
    }

    private void ReleaseAllMessageViews()
    {
        for (int i = 0; i < messageViewPool.Count; i++)
        {
            ReleaseMessageView(i);
        }
    }

    private void ClearMessageViewPool()
    {
        for (int i = messageViewPool.Count - 1; i >= 0; i--)
        {
            ChatMessageRowUI view = messageViewPool[i];

            if (view != null)
            {
                Destroy(view.gameObject);
            }
        }

        messageViewPool.Clear();
        boundMessageIndices.Clear();
    }

    private void CreateMeasurementView()
    {
        if (measurementView != null || messagePrefab == null || content == null)
        {
            return;
        }

        measurementView = Instantiate(messagePrefab, content);
        measurementView.name = "ChatMessageMeasurementView";
        measurementView.Clear();
        measurementView.gameObject.SetActive(false);
    }

    private ChatMessageRowUI GetMeasurementView()
    {
        if (measurementView == null)
        {
            CreateMeasurementView();
        }

        return measurementView;
    }

    private void DestroyMeasurementView()
    {
        if (measurementView == null)
        {
            return;
        }

        Destroy(measurementView.gameObject);
        measurementView = null;
    }

    #endregion

    #region Scroll

    private void OnScrollValueChanged(Vector2 _)
    {
        if (suppressScrollRefresh)
        {
            return;
        }

        if (IsAtBottom())
        {
            followingBottom = true;
            SetHasNewMessagesBelow(false);
        }
        else
        {
            followingBottom = false;
        }

        RefreshVisibleMessageViews();
    }

    public void ScrollToBottom()
    {
        if (!initialized || content == null || viewport == null)
        {
            return;
        }

        float maxY = Mathf.Max(0f, content.rect.height - viewport.rect.height);

        suppressScrollRefresh = true;
        SetContentY(maxY);

        if (scrollRect != null)
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        suppressScrollRefresh = false;

        followingBottom = true;
        SetHasNewMessagesBelow(false);
        RefreshVisibleMessageViews();
    }

    private bool IsAtBottom()
    {
        if (!initialized || content == null || viewport == null)
        {
            return true;
        }

        float maxY = Mathf.Max(0f, content.rect.height - viewport.rect.height);

        if (maxY <= BottomPixelTolerance)
        {
            return true;
        }

        return maxY - content.anchoredPosition.y <= BottomPixelTolerance;
    }

    private void SetContentY(float y)
    {
        if (content == null)
        {
            return;
        }

        float maxY = viewport != null
            ? Mathf.Max(0f, content.rect.height - viewport.rect.height)
            : Mathf.Max(0f, y);

        y = Mathf.Clamp(y, 0f, maxY);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, y);
    }

    private void ClampContentPosition()
    {
        if (content == null || viewport == null)
        {
            return;
        }

        SetContentY(content.anchoredPosition.y);
    }

    private ViewAnchor CaptureViewAnchor()
    {
        if (messages.Count == 0 || content == null)
        {
            return default;
        }

        float viewTop = Mathf.Max(0f, content.anchoredPosition.y);
        int firstIndex = FindFirstVisibleIndex(viewTop);

        if (firstIndex < 0 || firstIndex >= messages.Count)
        {
            return default;
        }

        ChatMessageData message = messages[firstIndex];

        return new ViewAnchor(
            message?.messageId ?? string.Empty,
            viewTop - messageOffsets[firstIndex],
            viewTop);
    }

    private void RestoreViewAnchor(ViewAnchor anchor)
    {
        if (content == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(anchor.messageId))
        {
            for (int i = 0; i < messages.Count; i++)
            {
                ChatMessageData message = messages[i];

                if (message != null && string.Equals(message.messageId, anchor.messageId, StringComparison.Ordinal))
                {
                    SetContentY(messageOffsets[i] + anchor.offsetInsideMessage);
                    RefreshVisibleMessageViews();
                    return;
                }
            }
        }

        SetContentY(anchor.fallbackContentY);
        RefreshVisibleMessageViews();
    }

    #endregion

    #region State UI

    private void UpdateEmptyState()
    {
        bool showEmptyState = conversation != null && messages.Count == 0;

        if (emptyChatText != null)
        {
            emptyChatText.text = showEmptyState ? "No messages yet." : string.Empty;
            emptyChatText.gameObject.SetActive(showEmptyState);
        }
    }

    private void SetHasNewMessagesBelow(bool hasNewMessages)
    {
        hasNewMessagesBelow = hasNewMessages;
        RefreshNewMessagesButton();
    }

    private void RefreshNewMessagesButton()
    {
        if (newMessagesButton != null && isActiveAndEnabled)
        {
            newMessagesButton.gameObject.SetActive(hasNewMessagesBelow);
        }
    }

    private void OnNewMessagesClicked()
    {
        ScrollToBottom();
    }

    private bool HasConversationChangedSinceObserved()
    {
        if (!hasObservedNewestMessage)
        {
            return false;
        }

        string currentNewestMessageId = GetConversationNewestMessageId();
        return !string.Equals(observedNewestMessageId, currentNewestMessageId, StringComparison.Ordinal);
    }

    private void RecordObservedNewestMessage()
    {
        observedNewestMessageId = GetNewestMessageId();
        hasObservedNewestMessage = true;
    }

    private bool HasCurrentUserSentNewestMessage(bool hasNewNewestMessage)
    {
        return hasNewNewestMessage &&
               messages.Count > 0 &&
               messages[messages.Count - 1] != null &&
               messages[messages.Count - 1].isFromCurrentUser;
    }

    private string GetConversationNewestMessageId()
    {
        if (conversation?.messages == null || conversation.messages.Count == 0)
        {
            return string.Empty;
        }

        ChatMessageData message = conversation.messages[conversation.messages.Count - 1];
        return message?.messageId ?? string.Empty;
    }

    private void OnUserChatSettingsChanged(ChatSettingsData settings)
    {
        userSettings = settings?.Clone() ?? new ChatSettingsData();

        if (conversation == null)
        {
            return;
        }

        bool keepBottomPinned = followingBottom || IsAtBottom();
        ViewAnchor anchor = keepBottomPinned ? default : CaptureViewAnchor();

        RebuildMessageLayout();

        if (keepBottomPinned)
        {
            ScrollToBottom();
        }
        else
        {
            RestoreViewAnchor(anchor);
        }
    }

    private void OnProfileChanged(PlayerProfileData _)
    {
        if (conversation == null)
        {
            return;
        }

        bool keepBottomPinned = followingBottom || IsAtBottom();
        ViewAnchor anchor = keepBottomPinned ? default : CaptureViewAnchor();

        RebuildMessageLayout();

        if (keepBottomPinned)
        {
            ScrollToBottom();
        }
        else
        {
            RestoreViewAnchor(anchor);
        }
    }

    private string GetNewestMessageId()
    {
        if (messages.Count == 0)
        {
            return string.Empty;
        }

        ChatMessageData message = messages[messages.Count - 1];
        return message?.messageId ?? string.Empty;
    }

    #endregion

    #region Data

    private readonly struct MessagePresentation
    {
        public readonly Sprite icon;
        public readonly string text;
        public readonly UIThemeBackgroundType backgroundType;
        public readonly UIThemeTextType textType;
        public readonly bool useColorOverride;
        public readonly Color colorOverride;
        public readonly bool showIcon;

        public MessagePresentation(
            Sprite icon,
            string text,
            UIThemeBackgroundType backgroundType,
            UIThemeTextType textType,
            bool useColorOverride,
            Color colorOverride,
            bool showIcon)
        {
            this.icon = icon;
            this.text = text ?? string.Empty;
            this.backgroundType = backgroundType;
            this.textType = textType;
            this.useColorOverride = useColorOverride;
            this.colorOverride = colorOverride;
            this.showIcon = showIcon;
        }
    }

    private readonly struct ViewAnchor
    {
        public readonly string messageId;
        public readonly float offsetInsideMessage;
        public readonly float fallbackContentY;

        public ViewAnchor(string messageId, float offsetInsideMessage, float fallbackContentY)
        {
            this.messageId = messageId ?? string.Empty;
            this.offsetInsideMessage = offsetInsideMessage;
            this.fallbackContentY = fallbackContentY;
        }
    }

    #endregion
}
