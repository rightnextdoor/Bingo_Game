using UnityEngine;

[DisallowMultipleComponent]
public class ChatConfigSettings : MonoBehaviour
{
    #region Singleton / Settings

    public static ChatConfigSettings instance;

    [Header("Message Display")]
    [SerializeField, Min(1f)] private float messageTextSize = 24f;
    [SerializeField, Min(0f)] private float messageSpacing = 2f;

    [Header("Message History")]
    [SerializeField, Min(1)] private int maximumRetainedMessages = 200;

    public float MessageTextSize => Mathf.Max(1f, messageTextSize);
    public float MessageSpacing => Mathf.Max(0f, messageSpacing);
    public int MaximumRetainedMessages => Mathf.Max(1, maximumRetainedMessages);

    #endregion

    #region Unity Methods

    private void Awake()
    {
        if (instance != null && instance != this)
        {
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
}
