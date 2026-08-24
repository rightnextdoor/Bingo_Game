using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ChatIconController : MonoBehaviour
{
    #region Fields

    [Header("Icons")]
    [SerializeField] private UITopBarIconSlot helpIconSlot;
    [SerializeField] private UITopBarIconSlot settingsIconSlot;

    private ChatManager chatManager;
    private ChatCommandCatalog commandCatalog;
    private Coroutine setupRoutine;
    private bool iconsSetup;
    private bool subscribedToChatManager;

    #endregion

    #region Unity Methods

    private void OnEnable()
    {
        iconsSetup = false;
        ResolveReferences();
        TryCompleteSetup();

        if (!iconsSetup || !subscribedToChatManager)
        {
            setupRoutine = StartCoroutine(CompleteSetupWhenReady());
        }
    }

    private void OnDisable()
    {
        if (setupRoutine != null)
        {
            StopCoroutine(setupRoutine);
            setupRoutine = null;
        }

        UnsubscribeFromChatManager();
        helpIconSlot?.CancelTooltipDelay();
        CloseHelpTooltip();
        iconsSetup = false;
    }

    #endregion

    #region Setup

    private IEnumerator CompleteSetupWhenReady()
    {
        while (isActiveAndEnabled && (!iconsSetup || !subscribedToChatManager))
        {
            ResolveReferences();
            TryCompleteSetup();

            if (iconsSetup && subscribedToChatManager)
            {
                break;
            }

            yield return null;
        }

        setupRoutine = null;
    }

    private void TryCompleteSetup()
    {
        if (!iconsSetup && UIIconManager.instance != null && UIMessageCatalog.instance != null)
        {
            SetupIcons();
            iconsSetup = true;
        }

        if (!subscribedToChatManager && chatManager != null)
        {
            SubscribeToChatManager();
        }
    }

    private void SetupIcons()
    {
        UIIconManager iconManager = UIIconManager.instance;
        UIMessageData helpMessageData = UIMessageCatalog.instance.GetMessage(UIMessageType.ChatHelp);

        Sprite helpSprite = iconManager.GetNonPlayerIconSprite(UIIconType.ChatHelp);
        Sprite settingsSprite = iconManager.GetNonPlayerIconSprite(UIIconType.ChatSettings);
        float helpOpenDelay = helpMessageData != null ? helpMessageData.TooltipOpenDelay : 0f;

        helpIconSlot?.Setup(helpSprite, HandleHelpAction, UIMessageType.ChatHelp, helpOpenDelay, BuildHelpMessage);
        settingsIconSlot?.Setup(settingsSprite, HandleSettingsAction, UIMessageType.None);
    }

    private void ResolveReferences()
    {
        if (chatManager == null)
        {
            chatManager = ChatManager.instance;
        }

        if (commandCatalog == null && chatManager != null)
        {
            commandCatalog = chatManager.GetComponent<ChatCommandCatalog>();
        }
    }

    #endregion

    #region Actions

    private void HandleSettingsAction()
    {
        PopupManager.instance?.TogglePopup(PopupId.ChatSettings);
    }

    private void HandleHelpAction()
    {
        helpIconSlot?.CancelTooltipDelay();

        ToolTipManager toolTipManager = ToolTipManager.instance;

        if (toolTipManager == null)
        {
            return;
        }

        if (toolTipManager.IsShowing(UIMessageType.ChatHelp))
        {
            toolTipManager.HideToolTip();
            return;
        }

        UIMessageCatalog messageCatalog = UIMessageCatalog.instance;
        RectTransform targetRect = helpIconSlot != null ? helpIconSlot.transform as RectTransform : null;

        if (messageCatalog == null || targetRect == null)
        {
            return;
        }

        UIMessageData messageData = messageCatalog.GetMessage(UIMessageType.ChatHelp);

        if (messageData == null)
        {
            return;
        }

        toolTipManager.ShowToolTip(messageData, targetRect, BuildHelpMessage());
    }

    private string BuildHelpMessage()
    {
        ResolveReferences();
        return commandCatalog != null && commandCatalog.IsReady ? commandCatalog.BuildHelpMessage() : string.Empty;
    }

    private void CloseHelpTooltip()
    {
        ToolTipManager toolTipManager = ToolTipManager.instance;

        if (toolTipManager != null && toolTipManager.IsShowing(UIMessageType.ChatHelp))
        {
            toolTipManager.HideToolTip();
        }
    }

    #endregion

    #region Chat Events

    private void SubscribeToChatManager()
    {
        if (subscribedToChatManager || chatManager == null)
        {
            return;
        }

        chatManager.ChatHelpToggleRequested += HandleHelpAction;
        chatManager.ChatHelpCloseRequested += CloseHelpTooltip;
        subscribedToChatManager = true;
    }

    private void UnsubscribeFromChatManager()
    {
        if (!subscribedToChatManager || chatManager == null)
        {
            subscribedToChatManager = false;
            return;
        }

        chatManager.ChatHelpToggleRequested -= HandleHelpAction;
        chatManager.ChatHelpCloseRequested -= CloseHelpTooltip;
        subscribedToChatManager = false;
    }

    #endregion
}
