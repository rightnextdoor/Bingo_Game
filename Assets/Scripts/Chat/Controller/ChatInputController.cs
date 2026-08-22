using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatInputController : MonoBehaviour
{
    #region Fields

    [Header("Input")]
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Button sendButton;

    [Header("Suggestions")]
    [SerializeField] private ChatSuggestionController suggestionController;

    private ChatCommandCatalog commandCatalog;
    private bool submitting;
    private bool suppressValueChanged;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        ResolveReferences();
        ConfigureInput();
        ClearRuntimeUi();
    }

    private void OnEnable()
    {
        RegisterListeners();
        RefreshInteractableState();
    }

    private void OnDisable()
    {
        UnregisterListeners();
        suggestionController?.ClearSuggestions();
    }

    private void Update()
    {
        HandleSuggestionKeyboardInput();
    }

    #endregion

    #region Setup

    private void ResolveReferences()
    {
        if (chatInputField == null)
        {
            chatInputField = GetComponentInChildren<TMP_InputField>(true);
        }

        if (commandCatalog == null && ChatManager.instance != null)
        {
            commandCatalog = ChatManager.instance.GetComponent<ChatCommandCatalog>();
        }
    }

    private void ConfigureInput()
    {
        if (chatInputField != null)
        {
            chatInputField.lineType = TMP_InputField.LineType.SingleLine;
        }
    }

    private void ClearRuntimeUi()
    {
        if (chatInputField != null)
        {
            chatInputField.SetTextWithoutNotify(string.Empty);
        }

        suggestionController?.ClearSuggestions();
    }

    private void RegisterListeners()
    {
        if (chatInputField != null)
        {
            chatInputField.onValueChanged.RemoveListener(OnInputValueChanged);
            chatInputField.onValueChanged.AddListener(OnInputValueChanged);

            chatInputField.onSubmit.RemoveListener(OnInputSubmitted);
            chatInputField.onSubmit.AddListener(OnInputSubmitted);
        }

        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(OnSendClicked);
            sendButton.onClick.AddListener(OnSendClicked);
        }

        if (ChatManager.instance != null)
        {
            ChatManager.instance.ChatAvailabilityChanged -= OnChatAvailabilityChanged;
            ChatManager.instance.ChatAvailabilityChanged += OnChatAvailabilityChanged;

            ChatManager.instance.SessionParticipantsChanged -= OnParticipantsChanged;
            ChatManager.instance.SessionParticipantsChanged += OnParticipantsChanged;
        }
    }

    private void UnregisterListeners()
    {
        if (chatInputField != null)
        {
            chatInputField.onValueChanged.RemoveListener(OnInputValueChanged);
            chatInputField.onSubmit.RemoveListener(OnInputSubmitted);
        }

        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(OnSendClicked);
        }

        if (ChatManager.instance != null)
        {
            ChatManager.instance.ChatAvailabilityChanged -= OnChatAvailabilityChanged;
            ChatManager.instance.SessionParticipantsChanged -= OnParticipantsChanged;
        }
    }

    #endregion

    #region Input

    public void SetInteractable(bool interactable)
    {
        bool finalInteractable = interactable && !submitting;

        if (chatInputField != null)
        {
            chatInputField.interactable = finalInteractable;
        }

        if (sendButton != null)
        {
            sendButton.interactable = finalInteractable;
        }

        if (!finalInteractable)
        {
            suggestionController?.ClearSuggestions();
        }
    }

    public void ClearInput()
    {
        suppressValueChanged = true;

        if (chatInputField != null)
        {
            chatInputField.SetTextWithoutNotify(string.Empty);
        }

        suppressValueChanged = false;
        suggestionController?.ClearSuggestions();
    }

    private void OnInputValueChanged(string value)
    {
        if (!suppressValueChanged)
        {
            RefreshSuggestions(value);
        }
    }

    private void OnInputSubmitted(string value)
    {
        _ = SubmitAsync(value);
    }

    private void OnSendClicked()
    {
        _ = SubmitAsync(chatInputField != null ? chatInputField.text : string.Empty);
    }

    private async Task SubmitAsync(string input)
    {
        if (submitting)
        {
            return;
        }

        ChatManager chatManager = ChatManager.instance;
        ChatErrorType inputError = ChatError.CheckInput(input, chatManager);

        if (inputError != ChatErrorType.None)
        {
            HandleChatError(inputError, ChatError.GetMessage(inputError));
            return;
        }

        submitting = true;
        RefreshInteractableState();
        suggestionController?.ClearSuggestions();

        ChatSendResult result = await chatManager.SubmitMessageAsync(input);

        submitting = false;
        RefreshInteractableState();

        if (result != null && result.success)
        {
            ClearInput();
            FocusInput();
            return;
        }

        HandleChatError(
            ChatErrorType.SendFailed,
            string.IsNullOrWhiteSpace(result?.failureMessage) ? ChatError.GetMessage(ChatErrorType.SendFailed) : result.failureMessage);

        FocusInput();
    }

    public void HandleChatError(ChatErrorType errorType, string message)
    {
        if (errorType == ChatErrorType.None)
        {
            return;
        }

        // Future Chat error presentation routes through this method.
    }

    private void FocusInput()
    {
        if (chatInputField == null || !chatInputField.interactable)
        {
            return;
        }

        chatInputField.ActivateInputField();
        EventSystem.current?.SetSelectedGameObject(chatInputField.gameObject);
    }

    #endregion

    #region Suggestions

    private void RefreshSuggestions(string input)
    {
        suggestionController?.ClearSuggestions();

        ChatManager chatManager = ChatManager.instance;

        if (suggestionController == null || chatManager == null ||
            !TryGetSuggestionContext(input, out string commandPrefix, out string query) ||
            string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        IReadOnlyList<ChatUserSuggestion> managerSuggestions = chatManager.GetUserSuggestions(input, 1);

        if (managerSuggestions == null || managerSuggestions.Count == 0)
        {
            return;
        }

        List<ChatSuggestionData> suggestions = new List<ChatSuggestionData>(1);
        ChatUserSuggestion source = managerSuggestions[0];

        if (source != null && !string.IsNullOrWhiteSpace(source.userId) && !string.IsNullOrWhiteSpace(source.playerName))
        {
            suggestions.Add(new ChatSuggestionData
            {
                userId = source.userId,
                playerName = source.playerName,
                iconId = source.iconId,
                displayName = source.displayName
            });
        }

        suggestionController.SetSuggestions(
            suggestions,
            suggestion => ApplySuggestion(commandPrefix, suggestion, false),
            suggestion => commandPrefix + suggestion.displayName);
    }

    private bool TryGetSuggestionContext(string input, out string commandPrefix, out string query)
    {
        commandPrefix = string.Empty;
        query = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        commandCatalog ??= ChatManager.instance != null ? ChatManager.instance.GetComponent<ChatCommandCatalog>() : null;

        if (commandCatalog == null || !commandCatalog.IsReady)
        {
            return false;
        }

        string trimmedStart = input.TrimStart();

        if (!trimmedStart.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        int commandSpaceIndex = trimmedStart.IndexOf(' ');

        if (commandSpaceIndex < 0)
        {
            return false;
        }

        string commandToken = trimmedStart.Substring(1, commandSpaceIndex - 1);
        ChatCommandDefinition command = commandCatalog.FindCommand(commandToken);

        if (command == null || !command.targetsSessionUser)
        {
            return false;
        }

        commandPrefix = trimmedStart.Substring(0, commandSpaceIndex + 1);
        string arguments = trimmedStart.Substring(commandSpaceIndex + 1);

        if (HasResolvedTargetWithRemainder(arguments))
        {
            return false;
        }

        query = arguments.TrimStart();
        return true;
    }

    private bool HasResolvedTargetWithRemainder(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments) || ChatManager.instance?.SessionConversation?.participants == null)
        {
            return false;
        }

        string trimmed = arguments.TrimStart();
        ChatConversationData session = ChatManager.instance.SessionConversation;
        List<PlayerProfileData> profiles = BuildParticipantProfiles(session.participants);

        for (int i = 0; i < session.participants.Count; i++)
        {
            ChatParticipantData participant = session.participants[i];

            if (participant == null || !participant.IsValid)
            {
                continue;
            }

            string displayName = PlayerDisplayIdentityResolver.GetDisplayName(
                new PlayerProfileData(participant.userId, participant.playerName, participant.iconId), profiles);

            if (trimmed.Length > displayName.Length &&
                trimmed.StartsWith(displayName, StringComparison.OrdinalIgnoreCase) &&
                char.IsWhiteSpace(trimmed[displayName.Length]))
            {
                return true;
            }
        }

        return false;
    }

    private List<PlayerProfileData> BuildParticipantProfiles(IReadOnlyList<ChatParticipantData> participants)
    {
        List<PlayerProfileData> profiles = new List<PlayerProfileData>();

        if (participants == null)
        {
            return profiles;
        }

        for (int i = 0; i < participants.Count; i++)
        {
            ChatParticipantData participant = participants[i];

            if (participant != null && participant.IsValid)
            {
                profiles.Add(new PlayerProfileData(participant.userId, participant.playerName, participant.iconId));
            }
        }

        return profiles;
    }

    private void HandleSuggestionKeyboardInput()
    {
        if (chatInputField == null || !chatInputField.isFocused || Keyboard.current == null ||
            suggestionController == null || !suggestionController.HasSuggestions)
        {
            return;
        }

        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            AcceptSelectedSuggestion(false);
            KeepInputFocused();
            return;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            AcceptSelectedSuggestion(true);
            KeepInputFocused();
        }
    }

    private void AcceptSelectedSuggestion(bool appendSpace)
    {
        if (chatInputField == null || suggestionController?.SelectedSuggestion == null ||
            !TryGetSuggestionContext(chatInputField.text, out string commandPrefix, out _))
        {
            return;
        }

        ApplySuggestion(commandPrefix, suggestionController.SelectedSuggestion.Clone(), appendSpace);
    }

    private void ApplySuggestion(string commandPrefix, ChatSuggestionData suggestion, bool appendSpace)
    {
        if (chatInputField == null || suggestion == null)
        {
            return;
        }

        string replacement = commandPrefix + suggestion.displayName + (appendSpace ? " " : string.Empty);

        suppressValueChanged = true;
        chatInputField.SetTextWithoutNotify(replacement);
        chatInputField.caretPosition = replacement.Length;
        chatInputField.selectionAnchorPosition = replacement.Length;
        chatInputField.selectionFocusPosition = replacement.Length;
        suppressValueChanged = false;

        ChatManager.instance?.RememberSuggestedUser(suggestion.userId);
        suggestionController?.ClearSuggestions();
        FocusInput();
    }

    private void KeepInputFocused()
    {
        if (chatInputField != null)
        {
            EventSystem.current?.SetSelectedGameObject(chatInputField.gameObject);
        }
    }

    #endregion

    #region State

    private void RefreshInteractableState()
    {
        ChatManager chatManager = ChatManager.instance;
        bool available = chatManager != null && chatManager.IsChatEnabled && chatManager.IsChatAvailable && chatManager.HasSessionConversation;
        SetInteractable(available);
    }

    private void OnChatAvailabilityChanged(bool _)
    {
        RefreshInteractableState();
    }

    private void OnParticipantsChanged(ChatConversationData _)
    {
        if (chatInputField != null && chatInputField.isFocused)
        {
            RefreshSuggestions(chatInputField.text);
        }

        RefreshInteractableState();
    }

    #endregion
}
