using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class ChatSectionController : MonoBehaviour
{
    [Header("Controllers")]
    [SerializeField] private ChatTabController tabController;
    [SerializeField] private ChatMessageScrollController sessionMessageController;
    [SerializeField] private ChatInputController inputController;

    [Header("Friends Shell")]
    [SerializeField] private TMP_Text friendsEmptyStateText;

    private bool listenersRegistered;

    private void Awake()
    {
        ClearRuntimeUi();
    }

    private void OnEnable()
    {
        RegisterListeners();
        InitializeView();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    private void ClearRuntimeUi()
    {
        if (friendsEmptyStateText != null)
        {
            friendsEmptyStateText.text = string.Empty;
        }
    }

    private void InitializeView()
    {
        ChatTabType initialTab = ChatTabType.Session;

        if (ChatSettingsManager.instance != null && ChatSettingsManager.instance.IsReady)
        {
            initialTab = ChatSettingsManager.instance.CurrentSettings.lastSelectedChatTab;
        }

        tabController?.SetSelectedTab(initialTab, false);
        sessionMessageController?.RefreshFromCurrentSession();
        ApplyTabState(initialTab, false);
        RefreshAvailability();
    }

    private void RegisterListeners()
    {
        if (listenersRegistered)
        {
            return;
        }

        if (tabController != null)
        {
            tabController.TabChanged += OnTabChanged;
        }

        if (ChatManager.instance != null)
        {
            ChatManager.instance.ChatAvailabilityChanged += OnChatAvailabilityChanged;
            ChatManager.instance.ConversationJoined += OnConversationJoined;
            ChatManager.instance.ConversationLeft += OnConversationLeft;
        }

        listenersRegistered = true;
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered)
        {
            return;
        }

        if (tabController != null)
        {
            tabController.TabChanged -= OnTabChanged;
        }

        if (ChatManager.instance != null)
        {
            ChatManager.instance.ChatAvailabilityChanged -= OnChatAvailabilityChanged;
            ChatManager.instance.ConversationJoined -= OnConversationJoined;
            ChatManager.instance.ConversationLeft -= OnConversationLeft;
        }

        listenersRegistered = false;
    }

    private void OnTabChanged(ChatTabType tab)
    {
        ApplyTabState(tab, true);
    }

    private void ApplyTabState(ChatTabType tab, bool saveSelection)
    {
        bool sessionSelected = tab == ChatTabType.Session;

        if (friendsEmptyStateText != null)
        {
            friendsEmptyStateText.text = sessionSelected ? string.Empty : "Friends chat is not available yet.";
        }

        if (sessionSelected)
        {
            sessionMessageController?.RefreshFromCurrentSession();
        }

        RefreshAvailability();

        if (saveSelection)
        {
            SaveSelectedTab(tab);
        }
    }

    private void SaveSelectedTab(ChatTabType tab)
    {
        ChatSettingsManager settingsManager = ChatSettingsManager.instance;

        if (settingsManager == null || !settingsManager.IsReady)
        {
            return;
        }

        ChatSettingsData settings = settingsManager.CurrentSettings;

        if (settings.lastSelectedChatTab == tab)
        {
            return;
        }

        settings.lastSelectedChatTab = tab;
        settingsManager.UpdateChatSettings(settings);
    }

    private void RefreshAvailability()
    {
        ChatManager chatManager = ChatManager.instance;
        bool sessionSelected = tabController == null || tabController.SelectedTab == ChatTabType.Session;
        bool canSend = sessionSelected && chatManager != null && chatManager.IsChatEnabled &&
                       chatManager.IsChatAvailable && chatManager.HasSessionConversation;

        inputController?.SetInteractable(canSend);
    }

    private void OnChatAvailabilityChanged(bool _)
    {
        RefreshAvailability();
    }

    private void OnConversationJoined(ChatConversationData conversation)
    {
        if (conversation != null && conversation.conversationType == ChatConversationType.Session)
        {
            sessionMessageController?.SetConversation(conversation);
        }

        RefreshAvailability();
    }

    private void OnConversationLeft(ChatConversationReference conversation)
    {
        if (conversation != null && conversation.conversationType == ChatConversationType.Session)
        {
            sessionMessageController?.ClearDisplay();
        }

        RefreshAvailability();
    }
}
