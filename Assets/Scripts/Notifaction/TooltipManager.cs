using UnityEngine;

[DisallowMultipleComponent]
public class ToolTipManager : MonoBehaviour
{
    public static ToolTipManager instance;

    [Header("Tooltip UI")]
    [SerializeField] private UI_ToolTip toolTipUI;

    private UIMessageData currentMessageData;
    private TooltipVisualStyle currentVisualStyle;
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

    public bool IsShowing(UIMessageType messageType)
    {
        return currentMessageData != null && currentMessageData.MessageType == messageType;
    }

    public void ShowToolTip(UIMessageData messageData, RectTransform targetRect, string messageOverride = null)
    {
        if (messageData == null || targetRect == null || toolTipUI == null)
        {
            return;
        }

        currentMessageData = messageData;
        currentVisualStyle = null;
        currentTargetRect = targetRect;

        string message = string.IsNullOrWhiteSpace(messageOverride) ? messageData.BuildMessage() : messageOverride;
        toolTipUI.ShowNearTarget(messageData, message, targetRect);
    }

    public void ShowFixedToolTip(UIMessageData messageData, string messageOverride = null)
    {
        if (messageData == null || toolTipUI == null)
        {
            return;
        }

        currentMessageData = messageData;
        currentVisualStyle = null;
        currentTargetRect = null;

        string message = string.IsNullOrWhiteSpace(messageOverride) ? messageData.BuildMessage() : messageOverride;
        toolTipUI.ShowFixed(messageData, message);
    }

    public void ShowToolTip(TooltipVisualStyle visualStyle, RectTransform targetRect)
    {
        if (visualStyle == null || targetRect == null || toolTipUI == null)
        {
            return;
        }

        currentMessageData = null;
        currentVisualStyle = visualStyle;
        currentTargetRect = targetRect;

        toolTipUI.ShowNearTarget(visualStyle, targetRect);
    }

    public void HideToolTip()
    {
        currentMessageData = null;
        currentVisualStyle = null;
        currentTargetRect = null;

        if (toolTipUI != null)
        {
            toolTipUI.Hide();
        }
    }
}
