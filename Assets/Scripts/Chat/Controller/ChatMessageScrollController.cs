using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatMessageScrollController : MonoBehaviour
{
    #region Fields

    [Header("Scroll View")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform viewport;

    [Header("Rows")]
    [SerializeField] private ChatMessageRowUI rowPrefab;
    [SerializeField, Min(0f)] private float rowSpacing = 4f;
    [SerializeField, Min(0)] private int extraVisibleRows = 2;
    [SerializeField, Min(1f)] private float fallbackRowHeight = 48f;

    [Header("State UI")]
    [SerializeField] private TMP_Text emptyChatText;
    [SerializeField] private Button newMessagesButton;
    [SerializeField] private TMP_Text newMessagesButtonText;
    [SerializeField, Range(0f, 1f)] private float bottomThresholdNormalized = 0.02f;

    private readonly List<ChatMessageData> messages = new List<ChatMessageData>();
    private readonly List<float> rowHeights = new List<float>();
    private readonly List<float> rowOffsets = new List<float>();
    private readonly List<ChatMessageRowUI> rowPool = new List<ChatMessageRowUI>();
    private readonly List<int> boundIndices = new List<int>();

    private ChatConversationData conversation;
    private ChatSettingsData settingsData = new ChatSettingsData();
    private float cachedContentWidth = -1f;
    private bool initialized;
    private bool suppressScrollRefresh;
    private bool wasNearBottom = true;

    public ChatConversationData Conversation => conversation;

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
        RefreshFromCurrentSession();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    private void OnDestroy()
    {
        UnregisterListeners();
        ClearRowPool();
    }

    private void LateUpdate()
    {
        if (!initialized || content == null)
        {
            return;
        }

        float width = GetContentWidth();

        if (Mathf.Abs(width - cachedContentWidth) > 0.5f)
        {
            RebuildMeasurements(false);
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
        initialized = scrollRect != null && content != null && viewport != null && rowPrefab != null;

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
        rowPrefab.gameObject.SetActive(false);
        cachedContentWidth = GetContentWidth();
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

            if (rowPrefab != null && child.gameObject == rowPrefab.gameObject)
            {
                child.gameObject.SetActive(false);
                continue;
            }

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

    #region Listener Setup

    private void RegisterListeners()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
            scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }

        if (newMessagesButton != null)
        {
            newMessagesButton.onClick.RemoveListener(ScrollToBottom);
            newMessagesButton.onClick.AddListener(ScrollToBottom);
        }

        if (ChatManager.instance != null)
        {
            ChatManager.instance.ConversationMessagesChanged -= OnConversationMessagesChanged;
            ChatManager.instance.ConversationMessagesChanged += OnConversationMessagesChanged;

            ChatManager.instance.SessionParticipantsChanged -= OnSessionParticipantsChanged;
            ChatManager.instance.SessionParticipantsChanged += OnSessionParticipantsChanged;

            ChatManager.instance.ConversationJoined -= OnConversationJoined;
            ChatManager.instance.ConversationJoined += OnConversationJoined;

            ChatManager.instance.ConversationLeft -= OnConversationLeft;
            ChatManager.instance.ConversationLeft += OnConversationLeft;
        }

        if (ChatSettingsManager.instance != null)
        {
            ChatSettingsManager.instance.ChatSettingsChanged -= OnChatSettingsChanged;
            ChatSettingsManager.instance.ChatSettingsChanged += OnChatSettingsChanged;
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

        if (newMessagesButton != null)
        {
            newMessagesButton.onClick.RemoveListener(ScrollToBottom);
        }

        if (ChatManager.instance != null)
        {
            ChatManager.instance.ConversationMessagesChanged -= OnConversationMessagesChanged;
            ChatManager.instance.SessionParticipantsChanged -= OnSessionParticipantsChanged;
            ChatManager.instance.ConversationJoined -= OnConversationJoined;
            ChatManager.instance.ConversationLeft -= OnConversationLeft;
        }

        if (ChatSettingsManager.instance != null)
        {
            ChatSettingsManager.instance.ChatSettingsChanged -= OnChatSettingsChanged;
        }

        if (PlayerProfileRegistry.instance != null)
        {
            PlayerProfileRegistry.instance.ProfileChanged -= OnProfileChanged;
        }
    }

    #endregion

    #region Conversation

    public void SetConversation(ChatConversationData newConversation)
    {
        bool keepBottomPinned = IsNearBottom();

        conversation = newConversation;
        RebuildMessageData();
        RebuildMeasurements(keepBottomPinned);
    }

    public void RefreshFromCurrentSession()
    {
        if (ChatSettingsManager.instance != null && ChatSettingsManager.instance.IsReady)
        {
            settingsData = ChatSettingsManager.instance.CurrentSettings;
        }
        else
        {
            settingsData = new ChatSettingsData();
        }

        SetConversation(ChatManager.instance?.SessionConversation);
    }

    public void ClearDisplay()
    {
        conversation = null;
        messages.Clear();
        rowHeights.Clear();
        rowOffsets.Clear();
        ReleaseAllRows();

        if (emptyChatText != null)
        {
            emptyChatText.text = string.Empty;
            emptyChatText.gameObject.SetActive(false);
        }

        if (newMessagesButtonText != null)
        {
            newMessagesButtonText.text = string.Empty;
        }

        if (newMessagesButton != null)
        {
            newMessagesButton.gameObject.SetActive(false);
        }

        if (content != null)
        {
            Vector2 size = content.sizeDelta;
            size.y = 0f;
            content.sizeDelta = size;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        }

        wasNearBottom = true;
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

        bool nearBottomBefore = IsNearBottom();
        int previousMessageCount = messages.Count;
        float previousScrollY = content != null ? content.anchoredPosition.y : 0f;

        RebuildMessageData();
        RebuildMeasurements(false);

        bool receivedNewMessages = messages.Count > previousMessageCount;

        if (nearBottomBefore)
        {
            ScrollToBottom();
        }
        else if (content != null)
        {
            SetContentY(previousScrollY);
            SetNewMessagesButtonVisible(receivedNewMessages || (newMessagesButton != null && newMessagesButton.gameObject.activeSelf));
        }
    }

    private void OnSessionParticipantsChanged(ChatConversationData session)
    {
        if (!IsSameConversation(session, conversation))
        {
            return;
        }

        RefreshVisibleRows();
    }

    private void OnConversationJoined(ChatConversationData joinedConversation)
    {
        if (joinedConversation != null && joinedConversation.conversationType == ChatConversationType.Session)
        {
            SetConversation(joinedConversation);
        }
    }

    private void OnConversationLeft(ChatConversationReference leftConversation)
    {
        if (leftConversation == null || conversation == null || leftConversation.Key != conversation.Key)
        {
            return;
        }

        ClearDisplay();
    }

    private bool IsSameConversation(ChatConversationData first, ChatConversationData second)
    {
        return first != null && second != null && string.Equals(first.Key, second.Key, StringComparison.Ordinal);
    }

    #endregion

    #region Measurement

    private void RebuildMeasurements(bool scrollBottomAfter)
    {
        if (!initialized)
        {
            return;
        }

        rowHeights.Clear();
        rowOffsets.Clear();

        float width = GetContentWidth();
        cachedContentWidth = width;
        float currentOffset = 0f;

        ChatMessageRowUI measureRow = GetMeasureRow();

        for (int i = 0; i < messages.Count; i++)
        {
            ChatMessageData message = messages[i];
            string identity = ResolveMessageIdentity(message);
            float height = measureRow != null ? measureRow.MeasurePreferredHeight(message, identity, width) : fallbackRowHeight;
            height = Mathf.Max(1f, height);

            rowOffsets.Add(currentOffset);
            rowHeights.Add(height);
            currentOffset += height;

            if (i < messages.Count - 1)
            {
                currentOffset += rowSpacing;
            }
        }

        SetContentHeight(currentOffset);
        EnsurePoolCapacity();
        RefreshVisibleRows();

        if (scrollBottomAfter)
        {
            ScrollToBottom();
        }
    }

    private ChatMessageRowUI GetMeasureRow()
    {
        EnsurePoolCount(1);
        return rowPool.Count > 0 ? rowPool[0] : null;
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

        Vector2 size = content.sizeDelta;
        size.y = Mathf.Max(0f, height);
        content.sizeDelta = size;
        ClampContentPosition();
    }

    #endregion

    #region Virtual Rows

    private void EnsurePoolCapacity()
    {
        if (!initialized || viewport == null)
        {
            return;
        }

        float minimumHeight = Mathf.Max(1f, GetMinimumMeasuredHeight());
        int visibleCount = Mathf.CeilToInt(viewport.rect.height / minimumHeight) + (extraVisibleRows * 2) + 1;
        EnsurePoolCount(Mathf.Max(1, visibleCount));
    }

    private void EnsurePoolCount(int desiredCount)
    {
        if (rowPrefab == null || content == null)
        {
            return;
        }

        while (rowPool.Count < desiredCount)
        {
            ChatMessageRowUI row = Instantiate(rowPrefab, content);
            row.gameObject.SetActive(false);
            row.Clear();
            rowPool.Add(row);
            boundIndices.Add(-1);
        }
    }

    private float GetMinimumMeasuredHeight()
    {
        float minimum = fallbackRowHeight;

        for (int i = 0; i < rowHeights.Count; i++)
        {
            minimum = Mathf.Min(minimum, rowHeights[i]);
        }

        return Mathf.Max(1f, minimum);
    }

    private void RefreshVisibleRows()
    {
        if (!initialized)
        {
            return;
        }

        if (messages.Count == 0)
        {
            ReleaseAllRows();
            return;
        }

        EnsurePoolCapacity();

        float viewTop = Mathf.Max(0f, content.anchoredPosition.y);
        float viewBottom = viewTop + viewport.rect.height;
        int firstIndex = FindFirstVisibleIndex(viewTop);
        int lastIndex = FindLastVisibleIndex(viewBottom);

        firstIndex = Mathf.Max(0, firstIndex - extraVisibleRows);
        lastIndex = Mathf.Min(messages.Count - 1, lastIndex + extraVisibleRows);

        int requiredCount = Mathf.Max(0, lastIndex - firstIndex + 1);
        EnsurePoolCount(requiredCount);

        for (int poolIndex = 0; poolIndex < rowPool.Count; poolIndex++)
        {
            int messageIndex = poolIndex < requiredCount ? firstIndex + poolIndex : -1;
            BindRow(poolIndex, messageIndex);
        }
    }

    private int FindFirstVisibleIndex(float viewTop)
    {
        for (int i = 0; i < messages.Count; i++)
        {
            float bottom = rowOffsets[i] + rowHeights[i];

            if (bottom >= viewTop)
            {
                return i;
            }
        }

        return Mathf.Max(0, messages.Count - 1);
    }

    private int FindLastVisibleIndex(float viewBottom)
    {
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            if (rowOffsets[i] <= viewBottom)
            {
                return i;
            }
        }

        return 0;
    }

    private void BindRow(int poolIndex, int messageIndex)
    {
        if (poolIndex < 0 || poolIndex >= rowPool.Count)
        {
            return;
        }

        ChatMessageRowUI row = rowPool[poolIndex];

        if (row == null)
        {
            return;
        }

        if (messageIndex < 0 || messageIndex >= messages.Count)
        {
            boundIndices[poolIndex] = -1;
            row.Clear();
            row.gameObject.SetActive(false);
            return;
        }

        RectTransform rowRect = row.transform as RectTransform;

        if (rowRect != null)
        {
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.offsetMin = new Vector2(0f, rowRect.offsetMin.y);
            rowRect.offsetMax = new Vector2(0f, rowRect.offsetMax.y);
            rowRect.anchoredPosition = new Vector2(0f, -rowOffsets[messageIndex]);
            rowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeights[messageIndex]);
        }

        boundIndices[poolIndex] = messageIndex;
        row.gameObject.SetActive(true);
        row.Setup(messages[messageIndex], ResolveMessageIdentity(messages[messageIndex]), messageIndex, settingsData);
    }

    private void RefreshVisibleRowsThemeOnly()
    {
        for (int i = 0; i < rowPool.Count; i++)
        {
            if (rowPool[i] != null && rowPool[i].gameObject.activeSelf)
            {
                rowPool[i].ReapplyTheme();
            }
        }
    }

    private void ReleaseAllRows()
    {
        for (int i = 0; i < rowPool.Count; i++)
        {
            boundIndices[i] = -1;

            if (rowPool[i] != null)
            {
                rowPool[i].Clear();
                rowPool[i].gameObject.SetActive(false);
            }
        }
    }

    private void ClearRowPool()
    {
        for (int i = rowPool.Count - 1; i >= 0; i--)
        {
            if (rowPool[i] != null)
            {
                Destroy(rowPool[i].gameObject);
            }
        }

        rowPool.Clear();
        boundIndices.Clear();
    }

    #endregion

    #region Identity

    private string ResolveMessageIdentity(ChatMessageData message)
    {
        if (message == null)
        {
            return "Player";
        }

        ChatConversationData session = ChatManager.instance?.SessionConversation;
        List<PlayerProfileData> profiles = new List<PlayerProfileData>();

        if (session?.participants != null)
        {
            for (int i = 0; i < session.participants.Count; i++)
            {
                ChatParticipantData participant = session.participants[i];

                if (participant != null && participant.IsValid)
                {
                    profiles.Add(new PlayerProfileData(participant.userId, participant.playerName, participant.iconId));
                }
            }
        }

        string playerName = string.IsNullOrWhiteSpace(message.senderPlayerName) ? "Player" : message.senderPlayerName.Trim();
        PlayerProfileData profile = PlayerProfileRegistry.instance?.GetProfile(message.senderUserId) ??
                                    new PlayerProfileData(message.senderUserId, playerName, message.senderIconId);

        return PlayerDisplayIdentityResolver.GetDisplayName(profile, profiles);
    }

    #endregion

    #region Scroll

    private void OnScrollValueChanged(Vector2 _)
    {
        if (suppressScrollRefresh)
        {
            return;
        }

        wasNearBottom = IsNearBottom();

        if (wasNearBottom)
        {
            SetNewMessagesButtonVisible(false);
        }

        RefreshVisibleRows();
    }

    public void ScrollToBottom()
    {
        if (!initialized || content == null || viewport == null)
        {
            return;
        }

        float maxY = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        SetContentY(maxY);

        if (scrollRect != null)
        {
            scrollRect.StopMovement();
            suppressScrollRefresh = true;
            scrollRect.verticalNormalizedPosition = 0f;
            suppressScrollRefresh = false;
        }

        wasNearBottom = true;
        SetNewMessagesButtonVisible(false);
        RefreshVisibleRows();
    }

    private void SetContentY(float y)
    {
        if (content == null)
        {
            return;
        }

        float maxY = viewport != null ? Mathf.Max(0f, content.rect.height - viewport.rect.height) : Mathf.Max(0f, y);
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

    private bool IsNearBottom()
    {
        if (!initialized || content == null || viewport == null)
        {
            return true;
        }

        float maxY = Mathf.Max(0f, content.rect.height - viewport.rect.height);

        if (maxY <= 0.5f)
        {
            return true;
        }

        float remaining = maxY - content.anchoredPosition.y;
        float threshold = Mathf.Max(2f, viewport.rect.height * bottomThresholdNormalized);
        return remaining <= threshold;
    }

    #endregion

    #region UI State

    private void UpdateEmptyState()
    {
        bool isEmpty = messages.Count == 0;

        if (emptyChatText != null)
        {
            emptyChatText.text = isEmpty ? "No messages yet." : string.Empty;
            emptyChatText.gameObject.SetActive(isEmpty);
        }

        if (isEmpty)
        {
            SetNewMessagesButtonVisible(false);
        }
    }

    private void SetNewMessagesButtonVisible(bool visible)
    {
        if (newMessagesButtonText != null)
        {
            newMessagesButtonText.text = visible ? "New Messages" : string.Empty;
        }

        if (newMessagesButton != null)
        {
            newMessagesButton.gameObject.SetActive(visible);
        }
    }

    private void OnChatSettingsChanged(ChatSettingsData settings)
    {
        settingsData = settings?.Clone() ?? new ChatSettingsData();
        RefreshVisibleRows();
    }

    private void OnProfileChanged(PlayerProfileData _)
    {
        RefreshVisibleRows();
    }

    #endregion
}
