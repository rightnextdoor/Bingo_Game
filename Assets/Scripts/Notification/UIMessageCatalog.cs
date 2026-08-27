using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class UIMessageCatalog : MonoBehaviour
{
    public static UIMessageCatalog instance;

    #region Fields

    [SerializeField] private List<UIMessageData> messages = new List<UIMessageData>();

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
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Messages

    public UIMessageData GetMessage(UIMessageType messageType)
    {
        if (messageType == UIMessageType.None)
        {
            return null;
        }

        for (int i = 0; i < messages.Count; i++)
        {
            UIMessageData messageData = messages[i];

            if (messageData != null && messageData.MessageType == messageType)
            {
                return messageData;
            }
        }

        Debug.LogWarning($"UIMessageCatalog could not find UIMessageData for {messageType}.");
        return null;
    }

    #endregion
}