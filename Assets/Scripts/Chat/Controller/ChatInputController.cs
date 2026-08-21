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
    [SerializeField] private TMP_Text errorText;

    [Header("Suggestions")]
    [SerializeField] private ChatSuggestionController suggestionController;
    [SerializeField, Min(1)] private int maximumSuggestions = 8;

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
        ClearError();
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
        if (chatInputField == null)
        {
            return;
        }

        chatInputField.lineType = TMP_InputField.LineType.SingleLine;
    }

    private void ClearRuntimeUi()
    {
        if (chatInputField != null)
        {
            chatInputField.SetTextWithoutNotify(string.Empty);
        }

        ClearError();
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
        ClearError();
    }

    private void OnInputValueChanged(string value)
    {
        if (suppressValueChanged)
        {
            return;
        }

        ClearError();
        RefreshSuggestions(value);
    }

    private void OnInputSubmitted(string value)
    {
        if (suggestionController != null && suggestionController.HasSuggestions)
        {
            AcceptSelectedSuggestion(false);
            return;
        }

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

        ClearError();

        if (string.IsNullOrWhiteSpace(input))
        {
            ShowError("The chat message is empty.");
            return;
        }

        ChatManager chatManager = ChatManager.instance;

        if (chatManager == null)
        {
            ShowError("Chat is not available.");
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

        ShowError(result?.failureMessage ?? "The chat message could not be sent.");
        FocusInput();
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

        if (suggestionController == null || ChatManager.instance?.SessionConversation?.participants == null ||
            !TryGetSuggestionContext(input, out string commandPrefix, out string query))
        {
            return;
        }

        string localUserId = UserManager.instance != null && UserManager.instance.HasUser ? UserManager.instance.UserId : string.Empty;
        ChatConversationData session = ChatManager.instance.SessionConversation;
        List<PlayerProfileData> profiles = BuildParticipantProfiles(session.participants);
        List<ChatSuggestionData> suggestions = new List<ChatSuggestionData>();
        string normalizedQuery = NormalizeQuery(query);

        for (int i = 0; i < session.participants.Count; i++)
        {
            ChatParticipantData participant = session.participants[i];

            if (participant == null || !participant.IsValid || string.Equals(participant.userId, localUserId, StringComparison.Ordinal))
            {
                continue;
            }

            string displayName = PlayerDisplayIdentityResolver.GetDisplayName(
                new PlayerProfileData(participant.userId, participant.playerName, participant.iconId), profiles);

            if (!MatchesSuggestion(participant, displayName, normalizedQuery))
            {
                continue;
            }

            suggestions.Add(new ChatSuggestionData(participant, displayName));
        }

        suggestions.Sort((left, right) => CompareSuggestion(left, right, normalizedQuery));

        if (suggestions.Count > maximumSuggestions)
        {
            suggestions.RemoveRange(maximumSuggestions, suggestions.Count - maximumSuggestions);
        }

        suggestionController.SetSuggestions(suggestions, suggestion => ApplySuggestion(commandPrefix, suggestion, false));
        UpdateGhostSuggestion(commandPrefix, query);
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

        ChatConversationData session = ChatManager.instance.SessionConversation;
        List<PlayerProfileData> profiles = BuildParticipantProfiles(session.participants);
        string trimmed = arguments.TrimStart();

        for (int i = 0; i < session.participants.Count; i++)
        {
            ChatParticipantData participant = session.participants[i];

            if (participant == null || !participant.IsValid)
            {
                continue;
            }

            string displayName = PlayerDisplayIdentityResolver.GetDisplayName(
                new PlayerProfileData(participant.userId, participant.playerName, participant.iconId), profiles);

            if (trimmed.Length > displayName.Length && trimmed.StartsWith(displayName, StringComparison.OrdinalIgnoreCase) &&
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

    private string NormalizeQuery(string query)
    {
        string normalized = query?.Trim() ?? string.Empty;
        return normalized.StartsWith("#", StringComparison.Ordinal) ? normalized.Substring(1) : normalized;
    }

    private bool MatchesSuggestion(ChatParticipantData participant, string displayName, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return (participant.playerName ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
               (participant.userId ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
               (displayName ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private int CompareSuggestion(ChatSuggestionData left, ChatSuggestionData right, string query)
    {
        int leftRank = GetSuggestionRank(left, query);
        int rightRank = GetSuggestionRank(right, query);

        if (leftRank != rightRank)
        {
            return leftRank.CompareTo(rightRank);
        }

        return string.Compare(left?.playerName, right?.playerName, StringComparison.OrdinalIgnoreCase);
    }

    private int GetSuggestionRank(ChatSuggestionData suggestion, string query)
    {
        if (suggestion == null)
        {
            return int.MaxValue;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        if (string.Equals(suggestion.playerName, query, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(suggestion.userId, query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if ((suggestion.playerName ?? string.Empty).StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if ((suggestion.userId ?? string.Empty).StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 3;
    }

    private void UpdateGhostSuggestion(string commandPrefix, string query)
    {
        ChatSuggestionData selected = suggestionController?.SelectedSuggestion;

        if (selected == null)
        {
            suggestionController?.SetGhostText(string.Empty);
            return;
        }

        string typed = commandPrefix + (query ?? string.Empty);
        string completed = commandPrefix + selected.displayName;

        if (completed.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
        {
            suggestionController.SetGhostText(completed);
        }
        else
        {
            suggestionController.SetGhostText(selected.displayName);
        }
    }

    private void HandleSuggestionKeyboardInput()
    {
        if (chatInputField == null || !chatInputField.isFocused || Keyboard.current == null ||
            suggestionController == null || !suggestionController.HasSuggestions)
        {
            return;
        }

        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            suggestionController.MoveSelection(-1);
            RefreshGhostFromCurrentInput();
            KeepInputFocused();
            return;
        }

        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            suggestionController.MoveSelection(1);
            RefreshGhostFromCurrentInput();
            KeepInputFocused();
            return;
        }

        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            AcceptSelectedSuggestion(false);
            KeepInputFocused();
            return;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame && ShouldSpaceAcceptSuggestion())
        {
            AcceptSelectedSuggestion(true);
            KeepInputFocused();
        }
    }

    private bool ShouldSpaceAcceptSuggestion()
    {
        if (chatInputField == null || !TryGetSuggestionContext(chatInputField.text, out _, out string query))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(query) && suggestionController?.SelectedSuggestion != null;
    }

    private void AcceptSelectedSuggestion(bool appendSpace)
    {
        if (chatInputField == null || suggestionController?.SelectedSuggestion == null ||
            !TryGetSuggestionContext(chatInputField.text, out string commandPrefix, out _))
        {
            return;
        }

        ChatSuggestionData selected = suggestionController.SelectedSuggestion.Clone();
        ApplySuggestion(commandPrefix, selected, appendSpace);
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

        suggestionController?.ClearSuggestions();
        FocusInput();
    }

    private void RefreshGhostFromCurrentInput()
    {
        if (chatInputField != null && TryGetSuggestionContext(chatInputField.text, out string commandPrefix, out string query))
        {
            UpdateGhostSuggestion(commandPrefix, query);
        }
    }

    private void KeepInputFocused()
    {
        if (chatInputField == null)
        {
            return;
        }

        EventSystem.current?.SetSelectedGameObject(chatInputField.gameObject);
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

    private void ShowError(string message)
    {
        if (errorText != null)
        {
            errorText.text = message ?? string.Empty;
            errorText.gameObject.SetActive(!string.IsNullOrWhiteSpace(errorText.text));
        }
    }

    private void ClearError()
    {
        if (errorText != null)
        {
            errorText.text = string.Empty;
            errorText.gameObject.SetActive(false);
        }
    }

    #endregion
}
