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
        tabController?.SelectLobby(false);
        lobbyChatController?.RefreshFromCurrentSession();

        RefreshAvailability();
        RefreshNewMessagesButton();
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
            tabController.SelectionChanged += OnTabSelectionChanged;
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
            tabController.SelectionChanged -= OnTabSelectionChanged;
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

    private void OnTabSelectionChanged()
    {
        RefreshAvailability();
        RefreshNewMessagesButton();
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
        bool lobbySelected = tabController == null || tabController.IsLobbySelected;

        return lobbySelected
            ? lobbyChatController?.MessageScrollController
            : friendsChatController?.MessageScrollController;
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
        bool lobbySelected = tabController == null || tabController.IsLobbySelected;
        bool canSend = lobbySelected && chatManager != null && chatManager.IsChatEnabled &&
                       chatManager.IsChatAvailable && chatManager.HasSessionConversation;

        inputController?.SetInteractable(canSend);
    }

    private void OnChatAvailabilityChanged(bool _)
    {
        RefreshAvailability();
    }

    #endregion
}
