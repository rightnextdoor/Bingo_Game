using UnityEngine;

[DisallowMultipleComponent]
public class FriendsChatController : MonoBehaviour
{
    #region Fields

    [SerializeField] private ChatMessageScrollController messageScrollController;

    private ChatConversationData conversation;

    public ChatConversationData Conversation => conversation;
    public ChatMessageScrollController MessageScrollController => messageScrollController;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        ClearConversation();
    }

    #endregion

    #region Conversation

    public void SetConversation(ChatConversationData newConversation)
    {
        conversation = newConversation;

        if (conversation == null)
        {
            ClearConversation();
            return;
        }

        messageScrollController?.SetConversation(conversation);
    }

    public void RefreshConversation()
    {
        if (conversation == null)
        {
            ClearConversation();
            return;
        }

        messageScrollController?.RefreshConversation();
    }

    public void ClearConversation()
    {
        conversation = null;
        messageScrollController?.ClearDisplay();
    }

    #endregion
}
