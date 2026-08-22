using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatSectionController : MonoBehaviour
{
    #region Fields

    [Header("Controllers")]
    [SerializeField] private ChatTabController tabController;
    [SerializeField] private LobbyChatController lobbyChatController;
    [SerializeField] private FriendsChatController friendsChatController;
    [SerializeField] private ChatInputController inputController;

    [Header("Shared State")]
    [SerializeField] private Button newMessagesButton;

    private bool listenersRegistered;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        SetNewMessagesButtonVisible(false);
    }

    private void OnEnable()
    {
        RegisterListeners();
        InitializeView();
    }

    private void Start()
    {
        RegisterListeners();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    #endregion

    #region Setup

    private void InitializeView()
    {
        ChatTabType initialTab = ChatTabType.Session;

        if (ChatSettingsManager.instance != null && ChatSettingsManager.instance.IsReady)
        {
            initialTab = ChatSettingsManager.instance.CurrentSettings.lastSelectedChatTab;
        }

        tabController?.SetSelectedTab(initialTab, false);
        lobbyChatController?.RefreshFromCurrentSession();
        ApplyTabState(initialTab, false);
    }

    #endregion

    #region Listeners

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

        ChatMessageScrollController lobbyMessageScroll = lobbyChatController?.MessageScrollController;

        if (lobbyMessageScroll != null)
        {
            lobbyMessageScroll.NewMessagesStateChanged += OnNewMessagesStateChanged;
        }

        ChatMessageScrollController friendsMessageScroll = friendsChatController?.MessageScrollController;

        if (friendsMessageScroll != null)
        {
            friendsMessageScroll.NewMessagesStateChanged += OnNewMessagesStateChanged;
        }

        if (newMessagesButton != null)
        {
            newMessagesButton.onClick.RemoveListener(OnNewMessagesClicked);
            newMessagesButton.onClick.AddListener(OnNewMessagesClicked);
        }

        if (ChatManager.instance != null)
        {
            ChatManager.instance.ChatAvailabilityChanged += OnChatAvailabilityChanged;
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

        ChatMessageScrollController lobbyMessageScroll = lobbyChatController?.MessageScrollController;

        if (lobbyMessageScroll != null)
        {
            lobbyMessageScroll.NewMessagesStateChanged -= OnNewMessagesStateChanged;
        }

        ChatMessageScrollController friendsMessageScroll = friendsChatController?.MessageScrollController;

        if (friendsMessageScroll != null)
        {
            friendsMessageScroll.NewMessagesStateChanged -= OnNewMessagesStateChanged;
        }

        if (newMessagesButton != null)
        {
            newMessagesButton.onClick.RemoveListener(OnNewMessagesClicked);
        }

        if (ChatManager.instance != null)
        {
            ChatManager.instance.ChatAvailabilityChanged -= OnChatAvailabilityChanged;
        }

        listenersRegistered = false;
    }

    #endregion

    #region Tabs

    private void OnTabChanged(ChatTabType tab)
    {
        ApplyTabState(tab, true);
    }

    private void ApplyTabState(ChatTabType tab, bool saveSelection)
    {
        RefreshAvailability();
        RefreshNewMessagesButton();

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

    #endregion

    #region Shared New Messages

    private void OnNewMessagesStateChanged(bool _)
    {
        RefreshNewMessagesButton();
    }

    private void OnNewMessagesClicked()
    {
        GetActiveMessageScroll()?.ScrollToBottom();
        RefreshNewMessagesButton();
    }

    private void RefreshNewMessagesButton()
    {
        ChatMessageScrollController activeMessageScroll = GetActiveMessageScroll();
        SetNewMessagesButtonVisible(activeMessageScroll != null && activeMessageScroll.HasNewMessagesBelow);
    }

    private ChatMessageScrollController GetActiveMessageScroll()
    {
        ChatTabType selectedTab = tabController != null ? tabController.SelectedTab : ChatTabType.Session;

        return selectedTab == ChatTabType.Friends
            ? friendsChatController?.MessageScrollController
            : lobbyChatController?.MessageScrollController;
    }

    private void SetNewMessagesButtonVisible(bool visible)
    {
        if (newMessagesButton != null)
        {
            newMessagesButton.gameObject.SetActive(visible);
        }
    }

    #endregion

    #region Availability

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

    #endregion
}
