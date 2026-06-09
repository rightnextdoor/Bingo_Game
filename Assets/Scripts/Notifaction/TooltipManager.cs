using UnityEngine;

[DisallowMultipleComponent]
public class ToolTipManager : MonoBehaviour
{
    public static ToolTipManager instance;

    [Header("Tooltip UI")]
    [SerializeField] private UI_ToolTip toolTipUI;

    private UIMessageData currentMessageData;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("Duplicate ToolTipManager found. Disabling this duplicate.");
            enabled = false;
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

    public void ShowToolTip(UIMessageData messageData, Vector2 pointerScreenPosition)
    {
        if (messageData == null || toolTipUI == null)
        {
            return;
        }

        currentMessageData = messageData;

        string message = messageData.BuildMessage();

        toolTipUI.Show(messageData, message, pointerScreenPosition);
    }

    public void MoveToolTip(Vector2 pointerScreenPosition)
    {
        if (toolTipUI == null || currentMessageData == null)
        {
            return;
        }

        toolTipUI.MoveToPointer(pointerScreenPosition, currentMessageData.TooltipOffset);
    }

    public void HideToolTip()
    {
        currentMessageData = null;

        if (toolTipUI != null)
        {
            toolTipUI.Hide();
        }
    }
}