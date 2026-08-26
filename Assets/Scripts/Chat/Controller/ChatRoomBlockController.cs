using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChatRoomBlockController : MonoBehaviour
{
    #region Fields

    [Header("Search")]
    [SerializeField] private TMP_InputField blockSearchInput;
    [SerializeField] private Button addPlayerButton;
    [SerializeField] private ChatSuggestionController suggestionController;
    [SerializeField] private TMP_Text errorText;

    [Header("Blocked Users")]
    [SerializeField] private ScrollRect blockedUsersScrollRect;
    [SerializeField] private RectTransform blockedUsersContent;
    [SerializeField] private ChatBlockedUserRowUI blockedUserRowPrefab;
    [SerializeField, Min(1f)] private float blockedUserRowHeight = 56f;
    [SerializeField, Min(0)] private int extraVisibleRows = 1;
    [SerializeField] private TMP_Text emptyBlockedText;

    private readonly Dictionary<string, ChatParticipantData> participantSnapshot = new Dictionary<string, ChatParticipantData>(StringComparer.Ordinal);
    private readonly HashSet<string> initialBlockedUserIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> workingBlockedUserIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> addedByPopup = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> removedByPopup = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<ChatParticipantData> blockedUsers = new List<ChatParticipantData>();

    private VirtualizedScrollList virtualizedList;
    private ChatParticipantData localParticipantSnapshot;
    private bool hasSnapshot;
    private bool isLoadingUi;

    public bool HasSnapshot => hasSnapshot;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        ResolveReferences();
        ConfigureSearchInput();
        InitializeVirtualizedList();
        ClearUi();
    }

    private void OnEnable()
    {
        RegisterListeners();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    private void OnDestroy()
    {
        UnsubscribeFromVirtualizedList();
    }

    private void Update()
    {
        HandleSearchKeyboardInput();
    }

    #endregion

    #region Setup

    private void ResolveReferences()
    {
        if (blockedUsersContent == null && blockedUsersScrollRect != null)
        {
            blockedUsersContent = blockedUsersScrollRect.content;
        }
    }

    private void ConfigureSearchInput()
    {
        if (blockSearchInput != null)
        {
            blockSearchInput.lineType = TMP_InputField.LineType.SingleLine;
        }
    }

    private void InitializeVirtualizedList()
    {
        if (blockedUsersScrollRect == null || blockedUsersContent == null || blockedUserRowPrefab == null)
        {
            return;
        }

        virtualizedList = GetComponent<VirtualizedScrollList>();

        if (virtualizedList == null)
        {
            virtualizedList = gameObject.AddComponent<VirtualizedScrollList>();
        }

        UnsubscribeFromVirtualizedList();

        if (!virtualizedList.Initialize(
                blockedUsersScrollRect,
                blockedUsersContent,
                blockedUserRowPrefab.gameObject,
                null,
                null,
                blockedUserRowHeight,
                extraVisibleRows))
        {
            virtualizedList = null;
            return;
        }

        virtualizedList.ItemBound += OnBlockedItemBound;
        virtualizedList.ItemReleased += OnBlockedItemReleased;
    }

    private void UnsubscribeFromVirtualizedList()
    {
        if (virtualizedList == null)
        {
            return;
        }

        virtualizedList.ItemBound -= OnBlockedItemBound;
        virtualizedList.ItemReleased -= OnBlockedItemReleased;
    }

    #endregion

    #region Snapshot

    public void BeginSnapshot()
    {
        ClearWorkingState();
        ClearUi();

        ChatManager chatManager = ChatManager.instance;
        ChatConversationData session = chatManager?.SessionConversation;

        if (session?.participants == null)
        {
            hasSnapshot = true;
            RefreshBlockedList();
            return;
        }

        string localUserId = UserManager.instance != null && UserManager.instance.HasUser ? UserManager.instance.UserId : string.Empty;

        for (int i = 0; i < session.participants.Count; i++)
        {
            ChatParticipantData participant = session.participants[i];

            if (participant == null || !participant.IsValid)
            {
                continue;
            }

            if (string.Equals(participant.userId, localUserId, StringComparison.Ordinal))
            {
                localParticipantSnapshot = participant.Clone();
                continue;
            }

            participantSnapshot[participant.userId] = participant.Clone();

            if (chatManager.IsUserBlocked(participant.userId))
            {
                initialBlockedUserIds.Add(participant.userId);
                workingBlockedUserIds.Add(participant.userId);
            }
        }

        hasSnapshot = true;
        RefreshBlockedList();
    }

    public void DiscardSnapshot()
    {
        ClearWorkingState();
        ClearUi();
    }

    public void ApplyWorkingChanges()
    {
        if (hasSnapshot && ChatManager.instance != null)
        {
            ChatManager.instance.ApplyBlockChanges(addedByPopup, removedByPopup);
        }
    }

    private void ClearWorkingState()
    {
        participantSnapshot.Clear();
        initialBlockedUserIds.Clear();
        workingBlockedUserIds.Clear();
        addedByPopup.Clear();
        removedByPopup.Clear();
        blockedUsers.Clear();
        localParticipantSnapshot = null;
        hasSnapshot = false;
    }

    #endregion

    #region Search

    private void RegisterListeners()
    {
        if (blockSearchInput != null)
        {
            blockSearchInput.onValueChanged.RemoveListener(OnSearchChanged);
            blockSearchInput.onValueChanged.AddListener(OnSearchChanged);

            blockSearchInput.onSubmit.RemoveListener(OnSearchSubmitted);
            blockSearchInput.onSubmit.AddListener(OnSearchSubmitted);
        }

        if (addPlayerButton != null)
        {
            addPlayerButton.onClick.RemoveListener(OnAddPlayerClicked);
            addPlayerButton.onClick.AddListener(OnAddPlayerClicked);
        }
    }

    private void UnregisterListeners()
    {
        if (blockSearchInput != null)
        {
            blockSearchInput.onValueChanged.RemoveListener(OnSearchChanged);
            blockSearchInput.onSubmit.RemoveListener(OnSearchSubmitted);
        }

        if (addPlayerButton != null)
        {
            addPlayerButton.onClick.RemoveListener(OnAddPlayerClicked);
        }
    }

    private void OnSearchChanged(string value)
    {
        if (isLoadingUi || !hasSnapshot)
        {
            return;
        }

        ClearError();
        BuildSuggestions(value);
    }

    private void BuildSuggestions(string query)
    {
        suggestionController?.ClearSuggestions();

        string normalizedQuery = NormalizeQuery(query);

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return;
        }

        List<ChatSuggestionData> suggestions = new List<ChatSuggestionData>();

        foreach (KeyValuePair<string, ChatParticipantData> pair in participantSnapshot)
        {
            ChatParticipantData participant = pair.Value;

            if (participant == null || workingBlockedUserIds.Contains(participant.userId))
            {
                continue;
            }

            string displayName = GetSnapshotDisplayName(participant);

            if (!MatchesQuery(participant, displayName, normalizedQuery))
            {
                continue;
            }

            suggestions.Add(new ChatSuggestionData(participant, displayName));
        }

        suggestions.Sort(CompareSuggestions);

        suggestionController?.SetSuggestions(
            suggestions,
            OnSuggestionAccepted,
            suggestion => suggestion.displayName);
    }

    private string GetSnapshotDisplayName(ChatParticipantData participant)
    {
        if (participant == null)
        {
            return string.Empty;
        }

        return PlayerDisplayIdentityResolver.GetDisplayName(
            new PlayerProfileData(participant.userId, participant.playerName, participant.iconId),
            BuildSnapshotProfiles());
    }

    private List<PlayerProfileData> BuildSnapshotProfiles()
    {
        List<PlayerProfileData> profiles = new List<PlayerProfileData>(participantSnapshot.Count + 1);

        if (localParticipantSnapshot != null && localParticipantSnapshot.IsValid)
        {
            profiles.Add(new PlayerProfileData(
                localParticipantSnapshot.userId,
                localParticipantSnapshot.playerName,
                localParticipantSnapshot.iconId));
        }

        foreach (ChatParticipantData participant in participantSnapshot.Values)
        {
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

    private bool MatchesQuery(ChatParticipantData participant, string displayName, string query)
    {
        if (participant == null)
        {
            return false;
        }

        return (participant.playerName ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
               (participant.userId ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
               (displayName ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private int CompareSuggestions(ChatSuggestionData left, ChatSuggestionData right)
    {
        string rememberedUserId = ChatManager.instance?.LastSuggestedUserId ?? string.Empty;
        bool leftRemembered = left != null && string.Equals(left.userId, rememberedUserId, StringComparison.Ordinal);
        bool rightRemembered = right != null && string.Equals(right.userId, rememberedUserId, StringComparison.Ordinal);

        if (leftRemembered != rightRemembered)
        {
            return leftRemembered ? -1 : 1;
        }

        return string.Compare(left?.playerName, right?.playerName, StringComparison.OrdinalIgnoreCase);
    }

    private void OnSuggestionAccepted(ChatSuggestionData suggestion)
    {
        if (suggestion == null || !suggestion.IsValid)
        {
            ShowError(ChatBlockErrorType.PlayerNotFound);
            return;
        }

        string displayName = string.IsNullOrWhiteSpace(suggestion.displayName) ? suggestion.playerName : suggestion.displayName;

        SetSearchInputWithoutNotify(displayName);
        ChatManager.instance?.RememberSuggestedUser(suggestion.userId);
        suggestionController?.ClearSuggestions();
        ClearError();
        FocusSearchInput();
    }

    private void OnSearchSubmitted(string value)
    {
        ChatSuggestionData selectedSuggestion = suggestionController?.SelectedSuggestion?.Clone();
        _ = SubmitAfterInputEventAsync(value, selectedSuggestion);
    }

    private async Task SubmitAfterInputEventAsync(string input, ChatSuggestionData selectedSuggestion)
    {
        await Task.Yield();
        SubmitSearch(input, selectedSuggestion);
    }

    private void OnAddPlayerClicked()
    {
        string input = blockSearchInput != null ? blockSearchInput.text : string.Empty;
        ChatSuggestionData selectedSuggestion = suggestionController?.SelectedSuggestion?.Clone();
        SubmitSearch(input, selectedSuggestion);
    }

    private void SubmitSearch(string input, ChatSuggestionData selectedSuggestion)
    {
        SetSearchInputWithoutNotify(string.Empty);
        suggestionController?.ClearSuggestions();

        if (!hasSnapshot)
        {
            ShowError(ChatBlockErrorType.PlayerNotFound);
            FocusSearchInput();
            return;
        }

        if (!TryResolveSubmissionTarget(input, selectedSuggestion, out ChatParticipantData participant))
        {
            ShowError(GetBlockSearchError(input));
            FocusSearchInput();
            return;
        }

        ChatBlockErrorType errorType = GetBlockError(participant.userId);

        if (errorType != ChatBlockErrorType.None)
        {
            ShowError(errorType);
            FocusSearchInput();
            return;
        }

        if (!AddWorkingBlock(participant.userId))
        {
            ShowError(GetBlockError(participant.userId));
            FocusSearchInput();
            return;
        }

        ChatManager.instance?.RememberSuggestedUser(participant.userId);
        ClearError();
        RefreshBlockedList();
        FocusSearchInput();
    }

    private bool TryResolveSubmissionTarget(
        string input,
        ChatSuggestionData selectedSuggestion,
        out ChatParticipantData participant)
    {
        participant = null;

        if (selectedSuggestion != null && selectedSuggestion.IsValid &&
            participantSnapshot.TryGetValue(selectedSuggestion.userId, out ChatParticipantData selectedParticipant) &&
            selectedParticipant != null)
        {
            participant = selectedParticipant;
            return true;
        }

        string normalizedQuery = NormalizeQuery(input);

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return false;
        }

        ChatParticipantData singleMatch = null;

        foreach (ChatParticipantData candidate in participantSnapshot.Values)
        {
            if (candidate == null || !MatchesExactParticipant(candidate, normalizedQuery))
            {
                continue;
            }

            if (singleMatch != null)
            {
                return false;
            }

            singleMatch = candidate;
        }

        participant = singleMatch;
        return participant != null;
    }

    private void HandleSearchKeyboardInput()
    {
        if (blockSearchInput == null || !blockSearchInput.isFocused || Keyboard.current == null || suggestionController == null)
        {
            return;
        }

        if (suggestionController.HasSuggestions && Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            suggestionController.MoveSelection(-1);
            KeepSearchFocused();
            return;
        }

        if (suggestionController.HasSuggestions && Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            suggestionController.MoveSelection(1);
            KeepSearchFocused();
            return;
        }

        if (suggestionController.HasSuggestions && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            suggestionController.AcceptSelected();
            KeepSearchFocused();
        }
    }

    private ChatBlockErrorType GetBlockSearchError(string query)
    {
        string normalizedQuery = NormalizeQuery(query);

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return ChatBlockErrorType.PlayerNotFound;
        }

        if (MatchesExactParticipant(localParticipantSnapshot, normalizedQuery))
        {
            return ChatBlockErrorType.CannotBlockSelf;
        }

        foreach (KeyValuePair<string, ChatParticipantData> pair in participantSnapshot)
        {
            ChatParticipantData participant = pair.Value;

            if (participant == null || !MatchesExactParticipant(participant, normalizedQuery))
            {
                continue;
            }

            return workingBlockedUserIds.Contains(participant.userId)
                ? ChatBlockErrorType.PlayerAlreadyBlocked
                : ChatBlockErrorType.PlayerNotFound;
        }

        return ChatBlockErrorType.PlayerNotFound;
    }

    private ChatBlockErrorType GetBlockError(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ChatBlockErrorType.PlayerNotFound;
        }

        if (localParticipantSnapshot != null && string.Equals(localParticipantSnapshot.userId, userId, StringComparison.Ordinal))
        {
            return ChatBlockErrorType.CannotBlockSelf;
        }

        if (workingBlockedUserIds.Contains(userId))
        {
            return ChatBlockErrorType.PlayerAlreadyBlocked;
        }

        return participantSnapshot.ContainsKey(userId) ? ChatBlockErrorType.None : ChatBlockErrorType.PlayerNotFound;
    }

    private bool MatchesExactParticipant(ChatParticipantData participant, string query)
    {
        if (participant == null || string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        string displayName = GetSnapshotDisplayName(participant);

        return string.Equals(participant.playerName, query, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(participant.userId, query, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(displayName, query, StringComparison.OrdinalIgnoreCase);
    }

    private void KeepSearchFocused()
    {
        if (blockSearchInput != null)
        {
            EventSystem.current?.SetSelectedGameObject(blockSearchInput.gameObject);
        }
    }

    private void FocusSearchInput()
    {
        if (blockSearchInput == null || !blockSearchInput.interactable)
        {
            return;
        }

        blockSearchInput.ActivateInputField();
        EventSystem.current?.SetSelectedGameObject(blockSearchInput.gameObject);
    }

    private void SetSearchInputWithoutNotify(string text)
    {
        if (blockSearchInput == null)
        {
            return;
        }

        string value = text ?? string.Empty;
        int endPosition = value.Length;

        blockSearchInput.SetTextWithoutNotify(value);

        blockSearchInput.stringPosition = endPosition;
        blockSearchInput.selectionStringAnchorPosition = endPosition;
        blockSearchInput.selectionStringFocusPosition = endPosition;

        blockSearchInput.caretPosition = endPosition;
        blockSearchInput.selectionAnchorPosition = endPosition;
        blockSearchInput.selectionFocusPosition = endPosition;

        blockSearchInput.ForceLabelUpdate();
    }

    #endregion

    #region Working Block List

    private bool AddWorkingBlock(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || !participantSnapshot.ContainsKey(userId) || !workingBlockedUserIds.Add(userId))
        {
            return false;
        }

        removedByPopup.Remove(userId);

        if (!initialBlockedUserIds.Contains(userId))
        {
            addedByPopup.Add(userId);
        }

        return true;
    }

    private void RemoveWorkingBlock(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || !workingBlockedUserIds.Remove(userId))
        {
            return;
        }

        addedByPopup.Remove(userId);

        if (initialBlockedUserIds.Contains(userId))
        {
            removedByPopup.Add(userId);
        }

        ClearError();
        RefreshBlockedList();
    }

    private void RefreshBlockedList()
    {
        blockedUsers.Clear();

        foreach (string userId in workingBlockedUserIds)
        {
            if (participantSnapshot.TryGetValue(userId, out ChatParticipantData participant) && participant != null)
            {
                blockedUsers.Add(participant.Clone());
            }
        }

        blockedUsers.Sort((left, right) => string.Compare(left?.playerName, right?.playerName, StringComparison.OrdinalIgnoreCase));

        if (virtualizedList == null)
        {
            InitializeVirtualizedList();
        }

        virtualizedList?.SetItemCount(blockedUsers.Count);

        if (emptyBlockedText != null)
        {
            emptyBlockedText.text = blockedUsers.Count == 0 ? "No blocked users in this room." : string.Empty;
            emptyBlockedText.gameObject.SetActive(blockedUsers.Count == 0);
        }
    }

    private void OnBlockedItemBound(GameObject itemObject, int index)
    {
        if (itemObject == null || index < 0 || index >= blockedUsers.Count)
        {
            return;
        }

        itemObject.GetComponent<ChatBlockedUserRowUI>()?.Setup(blockedUsers[index], RemoveWorkingBlock);
    }

    private void OnBlockedItemReleased(GameObject itemObject, int _)
    {
        if (itemObject != null)
        {
            itemObject.GetComponent<ChatBlockedUserRowUI>()?.Clear();
        }
    }

    #endregion

    #region Error

    private void ShowError(ChatBlockErrorType errorType)
    {
        if (errorText == null)
        {
            return;
        }

        errorText.text = ChatBlockError.GetMessage(errorType);
        errorText.gameObject.SetActive(true);
    }

    private void ClearError()
    {
        if (errorText == null)
        {
            return;
        }

        errorText.text = string.Empty;
        errorText.gameObject.SetActive(false);
    }

    #endregion

    #region UI Reset

    private void ClearUi()
    {
        isLoadingUi = true;

        SetSearchInputWithoutNotify(string.Empty);

        if (emptyBlockedText != null)
        {
            emptyBlockedText.text = string.Empty;
            emptyBlockedText.gameObject.SetActive(false);
        }

        ClearError();
        suggestionController?.ClearSuggestions();
        virtualizedList?.SetItemCount(0);

        if (blockedUsersScrollRect != null)
        {
            blockedUsersScrollRect.StopMovement();
            blockedUsersScrollRect.verticalNormalizedPosition = 1f;
        }

        isLoadingUi = false;
    }

    #endregion


}
