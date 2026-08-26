using System;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbyChatController : MonoBehaviour
{
    #region Fields

    [SerializeField] private ChatMessageScrollController messageScrollController;

    private bool listenersRegistered;

    public ChatMessageScrollController MessageScrollController => messageScrollController;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        messageScrollController?.ClearDisplay();
    }

    private void OnEnable()
    {
        RegisterListeners();
        RefreshFromCurrentSession();
    }

    private void Start()
    {
        RegisterListeners();
        RefreshFromCurrentSession();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    #endregion

    #region Setup

    public void RefreshFromCurrentSession()
    {
        ChatConversationData sessionConversation = ChatManager.instance?.SessionConversation;

        if (sessionConversation == null)
        {
            messageScrollController?.ClearDisplay();
            return;
        }

        messageScrollController?.SetConversation(sessionConversation);
    }

    #endregion

    #region Listeners

    private void RegisterListeners()
    {
        if (listenersRegistered || ChatManager.instance == null)
        {
            return;
        }

        ChatManager.instance.ConversationJoined += OnConversationJoined;
        ChatManager.instance.ConversationLeft += OnConversationLeft;
        ChatManager.instance.SessionParticipantsChanged += OnSessionParticipantsChanged;
        listenersRegistered = true;
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered)
        {
            return;
        }

        if (ChatManager.instance != null)
        {
            ChatManager.instance.ConversationJoined -= OnConversationJoined;
            ChatManager.instance.ConversationLeft -= OnConversationLeft;
            ChatManager.instance.SessionParticipantsChanged -= OnSessionParticipantsChanged;
        }

        listenersRegistered = false;
    }

    private void OnConversationJoined(ChatConversationData conversation)
    {
        if (conversation == null || conversation.conversationType != ChatConversationType.Session)
        {
            return;
        }

        messageScrollController?.SetConversation(conversation);
    }

    private void OnConversationLeft(ChatConversationReference conversation)
    {
        if (conversation == null || conversation.conversationType != ChatConversationType.Session)
        {
            return;
        }

        ChatConversationData displayedConversation = messageScrollController?.Conversation;

        if (displayedConversation != null && string.Equals(displayedConversation.Key, conversation.Key, StringComparison.Ordinal))
        {
            messageScrollController.ClearDisplay();
        }
    }

    private void OnSessionParticipantsChanged(ChatConversationData sessionConversation)
    {
        if (sessionConversation == null || messageScrollController?.Conversation == null ||
            !string.Equals(sessionConversation.Key, messageScrollController.Conversation.Key, StringComparison.Ordinal))
        {
            return;
        }

        messageScrollController.RefreshConversation();
    }

    #endregion
}
