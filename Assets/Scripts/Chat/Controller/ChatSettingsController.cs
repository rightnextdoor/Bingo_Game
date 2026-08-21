using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatSettingsController : MonoBehaviour
{
    private enum ChatColorTarget
    {
        None,
        CurrentUser,
        OtherUser,
        Private
    }

    [Header("Settings")]
    [SerializeField] private ScrollRect settingsScrollView;
    [SerializeField] private Toggle chatEnabledToggle;

    [Header("Colors")]
    [SerializeField] private Button currentUserColorButton;
    [SerializeField] private Image currentUserColorSwatch;
    [SerializeField] private Button otherUserColorButton;
    [SerializeField] private Image otherUserColorSwatch;
    [SerializeField] private Button privateColorButton;
    [SerializeField] private Image privateColorSwatch;
    [SerializeField] private Button resetColorsButton;
    [SerializeField] private ColorPickerController colorPickerController;

    [Header("Room Block")]
    [SerializeField] private ChatRoomBlockController roomBlockController;

    [Header("Bottom Controls")]
    [SerializeField] private Button closeButton;

    private ChatSettingsData workingData;
    private ChatColorTarget activeColorTarget = ChatColorTarget.None;
    private Coroutine initializeRoutine;
    private PopupManager subscribedPopupManager;
    private bool workingSessionActive;
    private bool loadingUi;

    public ChatSettingsData WorkingData => workingData?.Clone();

    private void Awake()
    {
        ClearRuntimeUi();
        TrySubscribeToPopupManager();
    }

    private void Start()
    {
        TrySubscribeToPopupManager();
    }

    private void OnEnable()
    {
        RegisterUiListeners();
        TrySubscribeToPopupManager();
        StartInitialization();
    }

    private void OnDisable()
    {
        UnregisterUiListeners();

        if (initializeRoutine != null)
        {
            StopCoroutine(initializeRoutine);
            initializeRoutine = null;
        }

        loadingUi = false;
    }

    private void OnDestroy()
    {
        if (subscribedPopupManager != null)
        {
            subscribedPopupManager.PopupClosed -= OnPopupClosed;
            subscribedPopupManager = null;
        }
    }

    private void StartInitialization()
    {
        if (initializeRoutine != null)
        {
            StopCoroutine(initializeRoutine);
        }

        initializeRoutine = StartCoroutine(InitializeWhenReady());
    }

    private IEnumerator InitializeWhenReady()
    {
        workingSessionActive = false;
        workingData = null;
        activeColorTarget = ChatColorTarget.None;
        ClearRuntimeUi();
        roomBlockController?.DiscardSnapshot();
        colorPickerController?.Close();

        while (ChatSettingsManager.instance == null || !ChatSettingsManager.instance.IsReady)
        {
            yield return null;
        }

        workingData = ChatSettingsManager.instance.CurrentSettings;
        LoadWorkingDataIntoUi();
        roomBlockController?.BeginSnapshot();
        workingSessionActive = true;
        initializeRoutine = null;
    }

    private void ClearRuntimeUi()
    {
        loadingUi = true;

        if (chatEnabledToggle != null)
        {
            chatEnabledToggle.SetIsOnWithoutNotify(false);
        }

        ClearSwatch(currentUserColorSwatch);
        ClearSwatch(otherUserColorSwatch);
        ClearSwatch(privateColorSwatch);

        if (settingsScrollView != null)
        {
            settingsScrollView.verticalNormalizedPosition = 1f;
        }

        loadingUi = false;
    }

    private void ClearSwatch(Image swatch)
    {
        if (swatch != null)
        {
            swatch.color = Color.clear;
        }
    }

    private void LoadWorkingDataIntoUi()
    {
        if (workingData == null)
        {
            return;
        }

        loadingUi = true;

        if (chatEnabledToggle != null)
        {
            chatEnabledToggle.SetIsOnWithoutNotify(workingData.chatEnabled);
        }

        RefreshColorSwatches();

        if (settingsScrollView != null)
        {
            Canvas.ForceUpdateCanvases();
            settingsScrollView.verticalNormalizedPosition = 1f;
        }

        loadingUi = false;
    }

    private void RegisterUiListeners()
    {
        if (chatEnabledToggle != null)
        {
            chatEnabledToggle.onValueChanged.RemoveListener(OnChatEnabledChanged);
            chatEnabledToggle.onValueChanged.AddListener(OnChatEnabledChanged);
        }

        RegisterButton(currentUserColorButton, OpenCurrentUserColor);
        RegisterButton(otherUserColorButton, OpenOtherUserColor);
        RegisterButton(privateColorButton, OpenPrivateColor);
        RegisterButton(resetColorsButton, ResetColors);
        RegisterButton(closeButton, ClosePopup);
    }

    private void UnregisterUiListeners()
    {
        if (chatEnabledToggle != null)
        {
            chatEnabledToggle.onValueChanged.RemoveListener(OnChatEnabledChanged);
        }

        UnregisterButton(currentUserColorButton, OpenCurrentUserColor);
        UnregisterButton(otherUserColorButton, OpenOtherUserColor);
        UnregisterButton(privateColorButton, OpenPrivateColor);
        UnregisterButton(resetColorsButton, ResetColors);
        UnregisterButton(closeButton, ClosePopup);
    }

    private void RegisterButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void UnregisterButton(Button button, UnityEngine.Events.UnityAction action)
    {
        button?.onClick.RemoveListener(action);
    }

    private void OnChatEnabledChanged(bool enabled)
    {
        if (!loadingUi && workingData != null)
        {
            workingData.chatEnabled = enabled;
        }
    }

    private void OpenCurrentUserColor()
    {
        OpenColorPicker(ChatColorTarget.CurrentUser);
    }

    private void OpenOtherUserColor()
    {
        OpenColorPicker(ChatColorTarget.OtherUser);
    }

    private void OpenPrivateColor()
    {
        OpenColorPicker(ChatColorTarget.Private);
    }

    private void OpenColorPicker(ChatColorTarget target)
    {
        if (workingData == null || colorPickerController == null)
        {
            return;
        }

        activeColorTarget = target;
        Color startColor = GetEffectiveWorkingColor(target);
        colorPickerController.Open(startColor, OnPickerColorChanged);
    }

    private void OnPickerColorChanged(Color color)
    {
        if (workingData == null || activeColorTarget == ChatColorTarget.None)
        {
            return;
        }

        color.a = 1f;

        switch (activeColorTarget)
        {
            case ChatColorTarget.CurrentUser:
                workingData.overrideCurrentUserMessageColor = true;
                workingData.currentUserMessageColor = color;
                break;

            case ChatColorTarget.OtherUser:
                workingData.overrideOtherUserMessageColor = true;
                workingData.otherUserMessageColor = color;
                break;

            case ChatColorTarget.Private:
                workingData.overridePrivateMessageColor = true;
                workingData.privateMessageColor = color;
                break;
        }

        RefreshColorSwatches();
    }

    private void ResetColors()
    {
        if (workingData == null)
        {
            return;
        }

        workingData.overrideCurrentUserMessageColor = false;
        workingData.overrideOtherUserMessageColor = false;
        workingData.overridePrivateMessageColor = false;
        activeColorTarget = ChatColorTarget.None;
        colorPickerController?.Close();
        RefreshColorSwatches();
    }

    private void RefreshColorSwatches()
    {
        if (workingData == null)
        {
            return;
        }

        SetSwatch(currentUserColorSwatch, GetEffectiveWorkingColor(ChatColorTarget.CurrentUser));
        SetSwatch(otherUserColorSwatch, GetEffectiveWorkingColor(ChatColorTarget.OtherUser));
        SetSwatch(privateColorSwatch, GetEffectiveWorkingColor(ChatColorTarget.Private));
    }

    private void SetSwatch(Image swatch, Color color)
    {
        if (swatch == null)
        {
            return;
        }

        color.a = 1f;
        swatch.color = color;
    }

    private Color GetEffectiveWorkingColor(ChatColorTarget target)
    {
        if (workingData == null)
        {
            return Color.white;
        }

        switch (target)
        {
            case ChatColorTarget.CurrentUser:
                return workingData.overrideCurrentUserMessageColor
                    ? Opaque(workingData.currentUserMessageColor)
                    : GetThemeColor(UIThemeTextType.ChatCurrentUser, workingData.currentUserMessageColor);

            case ChatColorTarget.OtherUser:
                return workingData.overrideOtherUserMessageColor
                    ? Opaque(workingData.otherUserMessageColor)
                    : GetThemeColor(UIThemeTextType.ChatOtherUser, workingData.otherUserMessageColor);

            case ChatColorTarget.Private:
                return workingData.overridePrivateMessageColor
                    ? Opaque(workingData.privateMessageColor)
                    : GetThemeColor(UIThemeTextType.ChatPrivate, workingData.privateMessageColor);

            default:
                return Color.white;
        }
    }

    private Color GetThemeColor(UIThemeTextType textType, Color fallback)
    {
        UIThemeStyle style = UIThemeManager.instance?.GetTextStyle(textType);
        return style != null ? Opaque(style.VertexColor) : Opaque(fallback);
    }

    private Color Opaque(Color color)
    {
        color.a = 1f;
        return color;
    }

    private void TrySubscribeToPopupManager()
    {
        PopupManager popupManager = PopupManager.instance;

        if (popupManager == null || subscribedPopupManager == popupManager)
        {
            return;
        }

        if (subscribedPopupManager != null)
        {
            subscribedPopupManager.PopupClosed -= OnPopupClosed;
        }

        subscribedPopupManager = popupManager;
        subscribedPopupManager.PopupClosed += OnPopupClosed;
    }

    private void OnPopupClosed(PopupId popupId)
    {
        if (popupId != PopupId.ChatSettings || !workingSessionActive)
        {
            return;
        }

        ApplyAndClearWorkingSession();
    }

    private void ApplyAndClearWorkingSession()
    {
        workingSessionActive = false;
        activeColorTarget = ChatColorTarget.None;
        colorPickerController?.Close();
        roomBlockController?.ApplyWorkingChanges();

        if (workingData != null && ChatSettingsManager.instance != null && ChatSettingsManager.instance.IsReady)
        {
            ChatSettingsData latest = ChatSettingsManager.instance.CurrentSettings;
            workingData.lastSelectedChatTab = latest.lastSelectedChatTab;
            ChatSettingsManager.instance.UpdateChatSettings(workingData);
        }

        roomBlockController?.DiscardSnapshot();
        workingData = null;
        ClearRuntimeUi();
    }

    private void ClosePopup()
    {
        if (PopupManager.instance != null)
        {
            PopupManager.instance.CloseActivePopup();
            return;
        }

        if (workingSessionActive)
        {
            ApplyAndClearWorkingSession();
        }

        gameObject.SetActive(false);
    }
}
