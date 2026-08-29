using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LobbyPlayerListController : MonoBehaviour
{
    #region Fields

    private const float ArrowKeyInitialRepeatDelay = 0.35f;
    private const float ArrowKeyRepeatRate = 0.08f;

    [Header("Header")]
    [SerializeField] private TMP_Text playerCountText;

    [Header("Rows")]
    [SerializeField] private Transform rowParent;
    [SerializeField] private LobbyPlayerRowUI rowPrefab;

    [Header("Layout")]
    [SerializeField] private VerticalLayoutGroup verticalLayoutGroup;
    [SerializeField] private ContentSizeFitter contentSizeFitter;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Virtualization")]
    [SerializeField, Min(0f)] private float rowHeightOverride;
    [SerializeField, Min(0)] private int extraVisibleRows = 2;

    private readonly List<PlayerListPlayerData> players = new List<PlayerListPlayerData>();
    private readonly Dictionary<string, int> playerIndexByUserId = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, LobbyPlayerRowUI> visibleRowsByUserId = new Dictionary<string, LobbyPlayerRowUI>(StringComparer.Ordinal);

    private VirtualizedScrollList virtualizedList;
    private string selectedUserId = string.Empty;

    private int heldArrowDirection;
    private float nextArrowMoveTime;

    public event Action<string> KickRequested;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        ResolveReferences();
        InitializeVirtualizedList();
    }

    private void OnEnable()
    {
        SubscribeToPlayerProfiles();
    }

    private void Start()
    {
        SubscribeToPlayerProfiles();
    }

    private void OnDisable()
    {
        UnsubscribeFromPlayerProfiles();
    }

    private void OnDestroy()
    {
        UnsubscribeFromVirtualizedList();
        UnsubscribeFromPlayerProfiles();
    }

    private void Update()
    {
        HandleArrowKeySelection();
    }

    #endregion

    #region Setup

    private void ResolveReferences()
    {
        if (rowParent == null)
        {
            rowParent = transform;
        }

        if (verticalLayoutGroup == null)
        {
            verticalLayoutGroup = rowParent.GetComponent<VerticalLayoutGroup>();
        }

        if (contentSizeFitter == null)
        {
            contentSizeFitter = rowParent.GetComponent<ContentSizeFitter>();
        }

        if (scrollRect == null)
        {
            scrollRect = GetComponentInChildren<ScrollRect>();
        }
    }

    private void InitializeVirtualizedList()
    {
        if (rowParent is not RectTransform contentRect || rowPrefab == null || scrollRect == null)
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
                scrollRect,
                contentRect,
                rowPrefab.gameObject,
                verticalLayoutGroup,
                contentSizeFitter,
                rowHeightOverride,
                extraVisibleRows))
        {
            virtualizedList = null;
            return;
        }

        virtualizedList.ItemBound += OnVirtualItemBound;
        virtualizedList.ItemReleased += OnVirtualItemReleased;
    }

    private void UnsubscribeFromVirtualizedList()
    {
        if (virtualizedList == null)
        {
            return;
        }

        virtualizedList.ItemBound -= OnVirtualItemBound;
        virtualizedList.ItemReleased -= OnVirtualItemReleased;
    }

    #endregion

    #region Display

    public void DisplayLobbyInfo(LobbyViewData lobbyViewData, string localUserId)
    {
        if (lobbyViewData == null)
        {
            return;
        }

        BuildLobbyPlayerListData(lobbyViewData, localUserId);
        DisplayCurrentPlayers(lobbyViewData.playerCount, lobbyViewData.maxPlayer, lobbyViewData.maxPlayers);
    }

    public void DisplayPlayers(IReadOnlyList<PlayerListPlayerData> playerData, int playerCount)
    {
        DisplayPlayers(playerData, playerCount, 0, true);
    }

    public void DisplayPlayers(IReadOnlyList<PlayerListPlayerData> playerData, int playerCount, int maxPlayer, bool maxPlayers)
    {
        players.Clear();
        playerIndexByUserId.Clear();

        if (playerData != null)
        {
            for (int i = 0; i < playerData.Count; i++)
            {
                PlayerListPlayerData data = playerData[i];

                if (data == null || string.IsNullOrWhiteSpace(data.userId))
                {
                    continue;
                }

                playerIndexByUserId[data.userId] = players.Count;
                players.Add(data);
            }
        }

        ApplyCurrentProfiles();
        RefreshResolvedDisplayNames();
        DisplayCurrentPlayers(playerCount, maxPlayer, maxPlayers);
    }

    private void DisplayCurrentPlayers(int playerCount, int maxPlayer, bool maxPlayers)
    {
        UpdatePlayerCount(playerCount, maxPlayer, maxPlayers);

        if (!string.IsNullOrWhiteSpace(selectedUserId) && !playerIndexByUserId.ContainsKey(selectedUserId))
        {
            selectedUserId = string.Empty;
        }

        if (virtualizedList == null)
        {
            InitializeVirtualizedList();
        }

        virtualizedList?.SetItemCount(players.Count);
        RefreshVisibleSelection();
    }

    private void BuildLobbyPlayerListData(LobbyViewData lobbyViewData, string localUserId)
    {
        players.Clear();
        playerIndexByUserId.Clear();

        if (lobbyViewData?.players == null)
        {
            return;
        }

        bool localPlayerIsHost = IsLocalPlayerHost(lobbyViewData.players, localUserId);
        bool modeAllowsKick = lobbyViewData.playMode == MainMenuPlayMode.Solo || lobbyViewData.playMode == MainMenuPlayMode.Custom;

        for (int i = 0; i < lobbyViewData.players.Count; i++)
        {
            LobbyPlayerViewData lobbyPlayerData = lobbyViewData.players[i];

            if (lobbyPlayerData == null || string.IsNullOrWhiteSpace(lobbyPlayerData.userId))
            {
                continue;
            }

            PlayerListPlayerData playerData = BuildPlayerData(lobbyPlayerData);
            playerData.canKick = localPlayerIsHost && modeAllowsKick && playerData.userId != localUserId && !playerData.isHost;
            playerData.showBotIcon = localPlayerIsHost && playerData.userTag == UserTag.Bot;
            playerData.showReadyIcon = true;

            playerIndexByUserId[playerData.userId] = players.Count;
            players.Add(playerData);
        }

        ApplyCurrentProfiles();
        RefreshResolvedDisplayNames();
    }

    private PlayerListPlayerData BuildPlayerData(LobbyPlayerViewData lobbyPlayerData)
    {
        PlayerListPlayerData playerData = new PlayerListPlayerData(lobbyPlayerData);

        if (PlayerProfileRegistry.instance != null && PlayerProfileRegistry.instance.TryGetProfile(playerData.userId, out PlayerProfileData profile))
        {
            playerData.ApplyProfile(profile);
        }

        LobbyBoardData latestBoard = LobbyManager.instance != null ? LobbyManager.instance.GetPlayerBoard(playerData.userId) : null;

        if (latestBoard != null)
        {
            playerData.boardData = new LobbyBoardData(latestBoard);
        }

        return playerData;
    }

    #endregion

    #region Virtualized Rows

    private void OnVirtualItemBound(GameObject itemObject, int playerIndex)
    {
        if (itemObject == null || playerIndex < 0 || playerIndex >= players.Count)
        {
            return;
        }

        LobbyPlayerRowUI row = itemObject.GetComponent<LobbyPlayerRowUI>();
        PlayerListPlayerData playerData = players[playerIndex];

        if (row == null || playerData == null || string.IsNullOrWhiteSpace(playerData.userId))
        {
            return;
        }

        RemoveVisibleRowMapping(row);

        LobbyBoardData latestBoard = LobbyManager.instance != null ? LobbyManager.instance.GetPlayerBoard(playerData.userId) : null;

        if (latestBoard != null)
        {
            playerData.boardData = latestBoard;
        }

        bool highlighted = !string.IsNullOrWhiteSpace(selectedUserId) && selectedUserId == playerData.userId;
        row.Setup(playerData, OnRowClicked, OnKickRequested, highlighted);
        visibleRowsByUserId[playerData.userId] = row;
    }

    private void OnVirtualItemReleased(GameObject itemObject, int _)
    {
        if (itemObject == null)
        {
            return;
        }

        LobbyPlayerRowUI row = itemObject.GetComponent<LobbyPlayerRowUI>();
        RemoveVisibleRowMapping(row);
    }

    private void RemoveVisibleRowMapping(LobbyPlayerRowUI row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.UserId))
        {
            return;
        }

        if (visibleRowsByUserId.TryGetValue(row.UserId, out LobbyPlayerRowUI mappedRow) && mappedRow == row)
        {
            visibleRowsByUserId.Remove(row.UserId);
        }
    }

    public void ClearRows()
    {
        players.Clear();
        playerIndexByUserId.Clear();
        visibleRowsByUserId.Clear();
        selectedUserId = string.Empty;
        virtualizedList?.SetItemCount(0);
    }

    #endregion

    #region Player Profiles

    private void SubscribeToPlayerProfiles()
    {
        if (PlayerProfileRegistry.instance != null)
        {
            PlayerProfileRegistry.instance.ProfileChanged -= OnPlayerProfileChanged;
            PlayerProfileRegistry.instance.ProfileChanged += OnPlayerProfileChanged;
        }
    }

    private void UnsubscribeFromPlayerProfiles()
    {
        if (PlayerProfileRegistry.instance != null)
        {
            PlayerProfileRegistry.instance.ProfileChanged -= OnPlayerProfileChanged;
        }
    }

    private void OnPlayerProfileChanged(PlayerProfileData profile)
    {
        if (profile == null || !playerIndexByUserId.TryGetValue(profile.userId, out int playerIndex) ||
            playerIndex < 0 || playerIndex >= players.Count)
        {
            return;
        }

        players[playerIndex]?.ApplyProfile(profile);
        RefreshResolvedDisplayNames();
        RefreshVisibleProfiles();
    }

    private void ApplyCurrentProfiles()
    {
        if (PlayerProfileRegistry.instance == null)
        {
            return;
        }

        for (int i = 0; i < players.Count; i++)
        {
            PlayerListPlayerData playerData = players[i];

            if (playerData != null && PlayerProfileRegistry.instance.TryGetProfile(playerData.userId, out PlayerProfileData profile))
            {
                playerData.ApplyProfile(profile);
            }
        }
    }

    private void RefreshResolvedDisplayNames()
    {
        List<PlayerProfileData> profiles = new List<PlayerProfileData>(players.Count);

        for (int i = 0; i < players.Count; i++)
        {
            PlayerListPlayerData playerData = players[i];

            if (playerData != null && !string.IsNullOrWhiteSpace(playerData.userId))
            {
                profiles.Add(playerData.BuildProfile());
            }
        }

        for (int i = 0; i < players.Count; i++)
        {
            PlayerListPlayerData playerData = players[i];

            if (playerData == null)
            {
                continue;
            }

            playerData.displayName = PlayerDisplayIdentityResolver.GetDisplayName(playerData.BuildProfile(), profiles);
            playerData.displayUserId = GetDisplayUserId(playerData);
        }
    }

    private string GetDisplayUserId(PlayerListPlayerData playerData)
    {
        if (playerData == null || string.IsNullOrWhiteSpace(playerData.displayName))
        {
            return string.Empty;
        }

        int separatorIndex = playerData.displayName.LastIndexOf(" #", StringComparison.Ordinal);

        if (separatorIndex < 0 || separatorIndex + 2 >= playerData.displayName.Length)
        {
            return string.Empty;
        }

        return playerData.displayName.Substring(separatorIndex + 2);
    }

    private void RefreshVisibleProfiles()
    {
        foreach (KeyValuePair<string, LobbyPlayerRowUI> pair in visibleRowsByUserId)
        {
            if (pair.Value == null || !playerIndexByUserId.TryGetValue(pair.Key, out int playerIndex) ||
                playerIndex < 0 || playerIndex >= players.Count)
            {
                continue;
            }

            pair.Value.UpdateProfile(players[playerIndex]);
        }
    }

    #endregion

    #region Board Updates

    public void UpdatePlayerBoard(string userId, LobbyBoardData boardData)
    {
        if (string.IsNullOrWhiteSpace(userId) || boardData == null)
        {
            return;
        }

        if (visibleRowsByUserId.TryGetValue(userId, out LobbyPlayerRowUI row) && row != null)
        {
            row.UpdateBoard(boardData);
        }
    }

    public void UpdatePlayerBoard(string userId, LobbyBoardData boardData, IReadOnlyList<int> markedCellIndices)
    {
        if (string.IsNullOrWhiteSpace(userId) || boardData == null)
        {
            return;
        }

        if (visibleRowsByUserId.TryGetValue(userId, out LobbyPlayerRowUI row) && row != null)
        {
            row.UpdateBoard(boardData, markedCellIndices);
        }
    }

    public void UpdatePlayerMarkedCells(string userId, IReadOnlyList<int> markedCellIndices)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        if (visibleRowsByUserId.TryGetValue(userId, out LobbyPlayerRowUI row) && row != null)
        {
            row.UpdateMarkedCells(markedCellIndices);
        }
    }

    public void SetPlayerMarkedCell(string userId, int cellIndex, bool isMarked)
    {
        if (string.IsNullOrWhiteSpace(userId) || cellIndex < 0)
        {
            return;
        }

        if (TryGetPlayerData(userId, out PlayerListPlayerData playerData))
        {
            playerData.markedCellIndices ??= new List<int>();

            bool isFreeCell =
                playerData.boardData?.usesFreeCell == true &&
                cellIndex == 12;

            if (isMarked || isFreeCell)
            {
                if (!playerData.markedCellIndices.Contains(cellIndex))
                {
                    playerData.markedCellIndices.Add(cellIndex);
                    playerData.markedCellIndices.Sort();
                }
            }
            else
            {
                playerData.markedCellIndices.Remove(cellIndex);
            }
        }

        if (visibleRowsByUserId.TryGetValue(userId, out LobbyPlayerRowUI row) && row != null)
        {
            row.SetMarkedCell(cellIndex, isMarked);
        }
    }

    private bool TryGetPlayerData(
        string userId,
        out PlayerListPlayerData playerData)
    {
        playerData = null;

        if (!playerIndexByUserId.TryGetValue(userId, out int playerIndex) ||
            playerIndex < 0 ||
            playerIndex >= players.Count)
        {
            return false;
        }

        playerData = players[playerIndex];
        return playerData != null;
    }

    #endregion

    #region Selection

    private void OnRowClicked(string userId)
    {
        SetSelectedUser(selectedUserId == userId ? string.Empty : userId);
    }

    private void OnKickRequested(string userId)
    {
        KickRequested?.Invoke(userId);
    }

    private void SetSelectedUser(string userId)
    {
        selectedUserId = userId ?? string.Empty;
        RefreshVisibleSelection();
    }

    private void RefreshVisibleSelection()
    {
        foreach (KeyValuePair<string, LobbyPlayerRowUI> pair in visibleRowsByUserId)
        {
            LobbyPlayerRowUI row = pair.Value;

            if (row != null)
            {
                row.SetHighlighted(!string.IsNullOrWhiteSpace(selectedUserId) && row.UserId == selectedUserId);
            }
        }
    }

    private void HandleArrowKeySelection()
    {
        if (string.IsNullOrWhiteSpace(selectedUserId) || Keyboard.current == null)
        {
            ResetArrowHold();
            return;
        }

        int direction = 0;
        bool pressedThisFrame = false;

        if (Keyboard.current.upArrowKey.isPressed)
        {
            direction = -1;
            pressedThisFrame = Keyboard.current.upArrowKey.wasPressedThisFrame;
        }
        else if (Keyboard.current.downArrowKey.isPressed)
        {
            direction = 1;
            pressedThisFrame = Keyboard.current.downArrowKey.wasPressedThisFrame;
        }

        if (direction == 0)
        {
            ResetArrowHold();
            return;
        }

        if (pressedThisFrame || heldArrowDirection != direction)
        {
            heldArrowDirection = direction;
            nextArrowMoveTime = Time.unscaledTime + ArrowKeyInitialRepeatDelay;
            MoveSelectedRow(direction);
            return;
        }

        if (Time.unscaledTime < nextArrowMoveTime)
        {
            return;
        }

        nextArrowMoveTime = Time.unscaledTime + ArrowKeyRepeatRate;
        MoveSelectedRow(direction);
    }

    private void ResetArrowHold()
    {
        heldArrowDirection = 0;
        nextArrowMoveTime = 0f;
    }

    private void MoveSelectedRow(int direction)
    {
        if (!playerIndexByUserId.TryGetValue(selectedUserId, out int selectedIndex))
        {
            return;
        }

        int nextIndex = Mathf.Clamp(selectedIndex + direction, 0, players.Count - 1);

        if (nextIndex == selectedIndex || nextIndex < 0 || nextIndex >= players.Count)
        {
            return;
        }

        PlayerListPlayerData nextPlayer = players[nextIndex];

        if (nextPlayer == null || string.IsNullOrWhiteSpace(nextPlayer.userId))
        {
            return;
        }

        selectedUserId = nextPlayer.userId;
        virtualizedList?.ScrollToIndex(nextIndex);
        RefreshVisibleSelection();
    }

    #endregion

    #region Helpers

    private void UpdatePlayerCount(int playerCount, int maxPlayer, bool maxPlayers)
    {
        if (playerCountText == null)
        {
            return;
        }

        playerCountText.text = maxPlayers ? $"Players {playerCount}" : $"Players {playerCount} / {maxPlayer}";
    }

    private bool IsLocalPlayerHost(IReadOnlyList<LobbyPlayerViewData> lobbyPlayers, string localUserId)
    {
        if (lobbyPlayers == null || string.IsNullOrWhiteSpace(localUserId))
        {
            return false;
        }

        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            LobbyPlayerViewData playerData = lobbyPlayers[i];

            if (playerData != null && playerData.userId == localUserId)
            {
                return playerData.isHost;
            }
        }

        return false;
    }

    #endregion
}
