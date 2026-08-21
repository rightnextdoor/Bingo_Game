using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatHeaderController : MonoBehaviour
{
    [Header("Help")]
    [SerializeField] private Button helpButton;
    [SerializeField] private Image helpIconImage;
    [SerializeField] private RectTransform helpTargetRect;
    [SerializeField] private UIIconType helpIconType = UIIconType.None;
    [SerializeField, Min(0f)] private float helpHoverDelay = 0.45f;

    [Header("Settings")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private Image settingsIconImage;
    [SerializeField] private UIIconType settingsIconType = UIIconType.None;

    private ChatCommandCatalog commandCatalog;
    private Coroutine hoverRoutine;
    private bool helpPointerInside;
    private bool hoverTooltipShowing;

    private void Awake()
    {
        ClearRuntimeUi();
        ResolveReferences();
    }

    private void OnEnable()
    {
        RegisterListeners();
        RefreshIcons();
    }

    private void OnDisable()
    {
        UnregisterListeners();
        StopHoverRoutine();
        CloseTransientHelp();
        helpPointerInside = false;
    }

    private void ResolveReferences()
    {
        if (helpTargetRect == null && helpButton != null)
        {
            helpTargetRect = helpButton.transform as RectTransform;
        }

        if (commandCatalog == null && ChatManager.instance != null)
        {
            commandCatalog = ChatManager.instance.GetComponent<ChatCommandCatalog>();
        }
    }

    private void ClearRuntimeUi()
    {
        if (helpIconImage != null)
        {
            helpIconImage.sprite = null;
            helpIconImage.enabled = false;
        }

        if (settingsIconImage != null)
        {
            settingsIconImage.sprite = null;
            settingsIconImage.enabled = false;
        }
    }

    private void RegisterListeners()
    {
        if (helpButton != null)
        {
            helpButton.onClick.RemoveListener(OnHelpClicked);
            helpButton.onClick.AddListener(OnHelpClicked);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }
    }

    private void UnregisterListeners()
    {
        if (helpButton != null)
        {
            helpButton.onClick.RemoveListener(OnHelpClicked);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
        }
    }

    public void RefreshIcons()
    {
        UIIconManager iconManager = UIIconManager.instance;

        SetIcon(helpIconImage, iconManager != null ? iconManager.GetNonPlayerIconSprite(helpIconType) : null);
        SetIcon(settingsIconImage, iconManager != null ? iconManager.GetNonPlayerIconSprite(settingsIconType) : null);
    }

    private void SetIcon(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = true;
    }

    private void OnSettingsClicked()
    {
        PopupManager.instance?.OpenPopup(PopupId.ChatSettings);
    }

    private void OnHelpClicked()
    {
        StopHoverRoutine();

        if (hoverTooltipShowing && ToolTipManager.instance != null)
        {
            ToolTipManager.instance.HideToolTip();
            hoverTooltipShowing = false;
        }

        commandCatalog ??= ChatManager.instance != null ? ChatManager.instance.GetComponent<ChatCommandCatalog>() : null;

        if (ChatManager.instance == null || commandCatalog == null || !commandCatalog.IsReady)
        {
            return;
        }

        ChatManager.instance.ToggleHelp(commandCatalog.BuildHelpMessage());
    }

    public void NotifyHelpPointerEnter()
    {
        helpPointerInside = true;
        StopHoverRoutine();

        if (ToolTipManager.instance != null && ToolTipManager.instance.IsShowing(UIMessageType.ChatHelp))
        {
            return;
        }

        hoverRoutine = StartCoroutine(ShowHoverHelpAfterDelay());
    }

    public void NotifyHelpPointerExit()
    {
        helpPointerInside = false;
        StopHoverRoutine();
        CloseTransientHelp();
    }

    private IEnumerator ShowHoverHelpAfterDelay()
    {
        if (helpHoverDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(helpHoverDelay);
        }

        hoverRoutine = null;

        if (!helpPointerInside || helpTargetRect == null || ToolTipManager.instance == null || UIMessageCatalog.instance == null)
        {
            yield break;
        }

        if (ToolTipManager.instance.IsShowing(UIMessageType.ChatHelp))
        {
            yield break;
        }

        commandCatalog ??= ChatManager.instance != null ? ChatManager.instance.GetComponent<ChatCommandCatalog>() : null;
        UIMessageData messageData = UIMessageCatalog.instance.GetMessage(UIMessageType.ChatHelp);

        if (messageData == null)
        {
            yield break;
        }

        string message = commandCatalog != null && commandCatalog.IsReady ? commandCatalog.BuildHelpMessage() : null;
        ToolTipManager.instance.ShowToolTip(messageData, helpTargetRect, message);
        hoverTooltipShowing = true;
    }

    private void CloseTransientHelp()
    {
        if (!hoverTooltipShowing)
        {
            return;
        }

        if (ToolTipManager.instance != null && ToolTipManager.instance.IsShowing(UIMessageType.ChatHelp))
        {
            ToolTipManager.instance.HideToolTip();
        }

        hoverTooltipShowing = false;
    }

    private void StopHoverRoutine()
    {
        if (hoverRoutine == null)
        {
            return;
        }

        StopCoroutine(hoverRoutine);
        hoverRoutine = null;
    }
}
