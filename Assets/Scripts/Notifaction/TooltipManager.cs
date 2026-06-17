using UnityEngine;

[DisallowMultipleComponent]
public class ToolTipManager : MonoBehaviour
{
    public static ToolTipManager instance;

    [Header("Tooltip UI")]
    [SerializeField] private UI_ToolTip toolTipUI;

    private UIMessageData currentMessageData;
    private RectTransform currentTargetRect;

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

        HideToolTip();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void ShowToolTip(UIMessageData messageData, RectTransform targetRect)
    {
        if (messageData == null || targetRect == null || toolTipUI == null)
        {
            return;
        }

        currentMessageData = messageData;
        currentTargetRect = targetRect;

        string message = messageData.BuildMessage();

        toolTipUI.ShowNearTarget(messageData, message, targetRect);
    }

    public void HideToolTip()
    {
        currentMessageData = null;
        currentTargetRect = null;

        if (toolTipUI != null)
        {
            toolTipUI.Hide();
        }
    }
}