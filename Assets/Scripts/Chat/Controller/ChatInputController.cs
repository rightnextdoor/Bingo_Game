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

        RememberCompletedCommand(input);

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

        if (suggestionController == null || chatManager == null)
        {
            return;
        }

        commandCatalog ??= chatManager.GetComponent<ChatCommandCatalog>();

        if (commandCatalog == null || !commandCatalog.IsReady)
        {
            return;
        }

        if (TryRefreshCommandSuggestion(input, chatManager))
        {
            return;
        }

        RefreshUserSuggestion(input, chatManager);
    }

    private bool TryRefreshCommandSuggestion(string input, ChatManager chatManager)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string trimmedStart = input.TrimStart();

        if (!trimmedStart.StartsWith("/", StringComparison.Ordinal) || trimmedStart.IndexOf(' ') >= 0)
        {
            return false;
        }

        string leadingWhitespace = input.Substring(0, input.Length - trimmedStart.Length);
        string commandQuery = trimmedStart.Substring(1);
        ChatCommandDefinition command = FindCommandSuggestion(commandQuery, chatManager);

        if (command == null)
        {
            return true;
        }

        string completedText = leadingWhitespace + "/" + command.name;

        if (string.Equals(input, completedText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        suggestionController.SetTextSuggestion(
            completedText,
            () => ApplyCommandSuggestion(leadingWhitespace, command));

        return true;
    }

    private ChatCommandDefinition FindCommandSuggestion(string query, ChatManager chatManager)
    {
        string preferredCommandName = chatManager?.LastSuggestedCommandName ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(preferredCommandName))
        {
            ChatCommandDefinition preferredCommand = commandCatalog.FindCommand(preferredCommandName);

            if (preferredCommand != null && IsCommandAvailable(preferredCommand, chatManager) &&
                (string.IsNullOrEmpty(query) || preferredCommand.name.StartsWith(query, StringComparison.OrdinalIgnoreCase)))
            {
                return preferredCommand;
            }
        }

        if (string.IsNullOrEmpty(query))
        {
            return null;
        }

        IReadOnlyList<ChatCommandDefinition> commands = commandCatalog.Commands;

        for (int i = 0; i < commands.Count; i++)
        {
            ChatCommandDefinition command = commands[i];

            if (command == null || !command.enabled || !IsCommandAvailable(command, chatManager))
            {
                continue;
            }

            if (command.name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return command;
            }
        }

        return null;
    }

    private bool IsCommandAvailable(ChatCommandDefinition command, ChatManager chatManager)
    {
        if (command == null)
        {
            return false;
        }

        switch (command.availability)
        {
            case ChatCommandAvailability.All:
                return true;

            case ChatCommandAvailability.SessionOnly:
                return chatManager != null && chatManager.HasSessionConversation;

            case ChatCommandAvailability.FriendsOnly:
                return false;

            default:
                return false;
        }
    }

    private void ApplyCommandSuggestion(string leadingWhitespace, ChatCommandDefinition command)
    {
        if (chatInputField == null || command == null)
        {
            return;
        }

        string replacement = leadingWhitespace + "/" + command.name;

        if (command.targetsSessionUser)
        {
            replacement += " ";
        }

        SetInputWithoutNotify(replacement);
        ChatManager.instance?.RememberSuggestedCommand(command.name);
        suggestionController?.ClearSuggestions();

        if (command.targetsSessionUser)
        {
            RefreshSuggestions(replacement);
        }

        FocusInput();
    }

    private void RefreshUserSuggestion(string input, ChatManager chatManager)
    {
        if (!TryGetUserSuggestionContext(input, out ChatCommandDefinition command, out string commandPrefix, out string query))
        {
            return;
        }

        ChatManager.instance?.RememberSuggestedCommand(command.name);

        if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(chatManager.LastSuggestedUserId))
        {
            return;
        }

        IReadOnlyList<ChatUserSuggestion> managerSuggestions = chatManager.GetUserSuggestions(input, 1);

        if (managerSuggestions == null || managerSuggestions.Count == 0)
        {
            return;
        }

        ChatUserSuggestion source = managerSuggestions[0];

        if (source == null || string.IsNullOrWhiteSpace(source.userId) || string.IsNullOrWhiteSpace(source.playerName))
        {
            return;
        }

        List<ChatSuggestionData> suggestions = new List<ChatSuggestionData>(1)
        {
            new ChatSuggestionData
            {
                userId = source.userId,
                playerName = source.playerName,
                iconId = source.iconId,
                displayName = source.displayName
            }
        };

        suggestionController.SetSuggestions(
            suggestions,
            suggestion => ApplyUserSuggestion(commandPrefix, suggestion),
            suggestion => commandPrefix + suggestion.displayName);
    }

    private bool TryGetUserSuggestionContext(
        string input,
        out ChatCommandDefinition command,
        out string commandPrefix,
        out string query)
    {
        command = null;
        commandPrefix = string.Empty;
        query = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
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
        command = commandCatalog.FindCommand(commandToken);

        if (command == null || !command.targetsSessionUser || !IsCommandAvailable(command, ChatManager.instance))
        {
            return false;
        }

        string leadingWhitespace = input.Substring(0, input.Length - trimmedStart.Length);
        commandPrefix = leadingWhitespace + trimmedStart.Substring(0, commandSpaceIndex + 1);
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
            suggestionController.AcceptSelected();
            KeepInputFocused();
        }
    }

    private void ApplyUserSuggestion(string commandPrefix, ChatSuggestionData suggestion)
    {
        if (chatInputField == null || suggestion == null)
        {
            return;
        }

        string replacement = commandPrefix + suggestion.displayName;

        SetInputWithoutNotify(replacement);
        ChatManager.instance?.RememberSuggestedUser(suggestion.userId);
        suggestionController?.ClearSuggestions();
        FocusInput();
    }

    private void SetInputWithoutNotify(string text)
    {
        if (chatInputField == null)
        {
            return;
        }

        string value = text ?? string.Empty;

        suppressValueChanged = true;
        chatInputField.SetTextWithoutNotify(value);
        chatInputField.caretPosition = value.Length;
        chatInputField.selectionAnchorPosition = value.Length;
        chatInputField.selectionFocusPosition = value.Length;
        suppressValueChanged = false;
    }

    private void RememberCompletedCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        commandCatalog ??= ChatManager.instance != null ? ChatManager.instance.GetComponent<ChatCommandCatalog>() : null;

        if (commandCatalog == null || !commandCatalog.IsReady)
        {
            return;
        }

        string trimmed = input.TrimStart();

        if (!trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            return;
        }

        int commandEndIndex = trimmed.IndexOf(' ');
        string commandToken = commandEndIndex < 0 ? trimmed.Substring(1) : trimmed.Substring(1, commandEndIndex - 1);
        ChatCommandDefinition command = commandCatalog.FindCommand(commandToken);

        if (command != null)
        {
            ChatManager.instance?.RememberSuggestedCommand(command.name);
        }
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
