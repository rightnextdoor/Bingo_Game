using System;
using System.Collections.Generic;
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
    [SerializeField] private ChatSuggestionController suggestionController;
    [SerializeField, Min(1)] private int maximumSuggestions = 8;

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
    private bool hasSnapshot;
    private bool isLoadingUi;

    public bool HasSnapshot => hasSnapshot;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        ResolveReferences();
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
        if (blockedUsersScrollRect == null)
        {
            blockedUsersScrollRect = GetComponentInChildren<ScrollRect>(true);
        }

        if (blockedUsersContent == null && blockedUsersScrollRect != null)
        {
            blockedUsersContent = blockedUsersScrollRect.content;
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

            if (participant == null || !participant.IsValid ||
                string.Equals(participant.userId, localUserId, StringComparison.Ordinal))
            {
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
        if (!hasSnapshot || ChatManager.instance == null)
        {
            return;
        }

        ChatManager.instance.ApplyBlockChanges(addedByPopup, removedByPopup);
    }

    private void ClearWorkingState()
    {
        participantSnapshot.Clear();
        initialBlockedUserIds.Clear();
        workingBlockedUserIds.Clear();
        addedByPopup.Clear();
        removedByPopup.Clear();
        blockedUsers.Clear();
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
        }
    }

    private void UnregisterListeners()
    {
        if (blockSearchInput != null)
        {
            blockSearchInput.onValueChanged.RemoveListener(OnSearchChanged);
        }
    }

    private void OnSearchChanged(string value)
    {
        if (isLoadingUi || !hasSnapshot)
        {
            return;
        }

        BuildSuggestions(value);
    }

    private void BuildSuggestions(string query)
    {
        suggestionController?.ClearSuggestions();

        string normalizedQuery = query?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return;
        }

        if (normalizedQuery.StartsWith("#", StringComparison.Ordinal))
        {
            normalizedQuery = normalizedQuery.Substring(1);
        }

        List<ChatSuggestionData> suggestions = new List<ChatSuggestionData>();

        foreach (KeyValuePair<string, ChatParticipantData> pair in participantSnapshot)
        {
            ChatParticipantData participant = pair.Value;

            if (participant == null || workingBlockedUserIds.Contains(participant.userId) ||
                !MatchesQuery(participant, normalizedQuery))
            {
                continue;
            }

            string displayName = GetSnapshotDisplayName(participant);
            suggestions.Add(new ChatSuggestionData(participant, displayName));

            if (suggestions.Count >= maximumSuggestions)
            {
                break;
            }
        }

        suggestions.Sort(CompareSuggestions);
        suggestionController?.SetSuggestions(suggestions, OnSuggestionAccepted);

        if (suggestionController?.SelectedSuggestion != null)
        {
            suggestionController.SetGhostText(suggestionController.SelectedSuggestion.displayName);
        }
    }


    private string GetSnapshotDisplayName(ChatParticipantData participant)
    {
        if (participant == null)
        {
            return string.Empty;
        }

        List<PlayerProfileData> profiles = new List<PlayerProfileData>(participantSnapshot.Count);

        foreach (ChatParticipantData snapshotParticipant in participantSnapshot.Values)
        {
            if (snapshotParticipant != null && snapshotParticipant.IsValid)
            {
                profiles.Add(new PlayerProfileData(snapshotParticipant.userId, snapshotParticipant.playerName, snapshotParticipant.iconId));
            }
        }

        return PlayerDisplayIdentityResolver.GetDisplayName(
            new PlayerProfileData(participant.userId, participant.playerName, participant.iconId),
            profiles);
    }

    private bool MatchesQuery(ChatParticipantData participant, string query)
    {
        if (participant == null)
        {
            return false;
        }

        return (participant.playerName ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
               (participant.userId ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private int CompareSuggestions(ChatSuggestionData left, ChatSuggestionData right)
    {
        return string.Compare(left?.playerName, right?.playerName, StringComparison.OrdinalIgnoreCase);
    }

    private void OnSuggestionAccepted(ChatSuggestionData suggestion)
    {
        if (suggestion == null || !participantSnapshot.ContainsKey(suggestion.userId))
        {
            return;
        }

        AddWorkingBlock(suggestion.userId);

        isLoadingUi = true;

        if (blockSearchInput != null)
        {
            blockSearchInput.SetTextWithoutNotify(string.Empty);
        }

        isLoadingUi = false;

        suggestionController?.ClearSuggestions();
        RefreshBlockedList();
    }

    private void HandleSearchKeyboardInput()
    {
        if (blockSearchInput == null || !blockSearchInput.isFocused || Keyboard.current == null ||
            suggestionController == null || !suggestionController.HasSuggestions)
        {
            return;
        }

        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            suggestionController.MoveSelection(-1);
            RefreshSuggestionGhost();
            KeepSearchFocused();
            return;
        }

        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            suggestionController.MoveSelection(1);
            RefreshSuggestionGhost();
            KeepSearchFocused();
            return;
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
            Keyboard.current.tabKey.wasPressedThisFrame ||
            (Keyboard.current.spaceKey.wasPressedThisFrame && !string.IsNullOrWhiteSpace(blockSearchInput.text)))
        {
            suggestionController.AcceptSelected();
            KeepSearchFocused();
        }
    }

    private void RefreshSuggestionGhost()
    {
        ChatSuggestionData selected = suggestionController?.SelectedSuggestion;
        suggestionController?.SetGhostText(selected?.displayName ?? string.Empty);
    }

    private void KeepSearchFocused()
    {
        if (blockSearchInput != null)
        {
            EventSystem.current?.SetSelectedGameObject(blockSearchInput.gameObject);
        }
    }

    #endregion

    #region Working Block List

    private void AddWorkingBlock(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || !participantSnapshot.ContainsKey(userId) || !workingBlockedUserIds.Add(userId))
        {
            return;
        }

        removedByPopup.Remove(userId);

        if (!initialBlockedUserIds.Contains(userId))
        {
            addedByPopup.Add(userId);
        }
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

    #endregion

    #region Virtualized Rows

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

    #region UI Reset

    private void ClearUi()
    {
        isLoadingUi = true;

        if (blockSearchInput != null)
        {
            blockSearchInput.SetTextWithoutNotify(string.Empty);
        }

        if (emptyBlockedText != null)
        {
            emptyBlockedText.text = string.Empty;
            emptyBlockedText.gameObject.SetActive(false);
        }

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
