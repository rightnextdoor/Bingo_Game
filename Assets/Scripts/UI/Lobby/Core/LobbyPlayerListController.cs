using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LobbyPlayerListController : MonoBehaviour
{
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

    private readonly List<LobbyPlayerRowUI> spawnedRows = new List<LobbyPlayerRowUI>();
    private readonly Dictionary<string, LobbyPlayerRowUI> rowsByUserId = new Dictionary<string, LobbyPlayerRowUI>();

    private string selectedUserId = string.Empty;

    private int heldArrowDirection;
    private float nextArrowMoveTime;

    public event Action<string> KickRequested;

    private void Awake()
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

        ClearRows();
    }

    private void Update()
    {
        HandleArrowKeySelection();
    }

    public void DisplayLobbyInfo(LobbyViewData lobbyViewData, string localUserId)
    {
        if (lobbyViewData == null)
        {
            return;
        }

        List<PlayerListPlayerData> players = BuildLobbyPlayerListData(lobbyViewData, localUserId);

        DisplayPlayers(players, lobbyViewData.playerCount, lobbyViewData.maxPlayers, lobbyViewData.unlimitedPlayers);
    }

    public void DisplayPlayers(IReadOnlyList<PlayerListPlayerData> players, int playerCount)
    {
        DisplayPlayers(players, playerCount, 0, true);
    }

    public void DisplayPlayers(
        IReadOnlyList<PlayerListPlayerData> players,
        int playerCount,
        int maxPlayers,
        bool unlimitedPlayers)
    {
        UpdatePlayerCount(playerCount, maxPlayers, unlimitedPlayers);

        string previousSelectedUserId = selectedUserId;

        if (rowParent == null || rowPrefab == null || players == null)
        {
            ClearRows();
            selectedUserId = string.Empty;
            return;
        }

        HashSet<string> desiredUserIds = new HashSet<string>();

        for (int i = 0; i < players.Count; i++)
        {
            string userId = players[i]?.userId;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                desiredUserIds.Add(userId);
            }
        }

        List<string> removedUserIds = new List<string>();

        foreach (KeyValuePair<string, LobbyPlayerRowUI> rowEntry in rowsByUserId)
        {
            if (!desiredUserIds.Contains(rowEntry.Key))
            {
                removedUserIds.Add(rowEntry.Key);
            }
        }

        for (int i = 0; i < removedUserIds.Count; i++)
        {
            string userId = removedUserIds[i];

            if (!rowsByUserId.TryGetValue(userId, out LobbyPlayerRowUI removedRow))
            {
                continue;
            }

            rowsByUserId.Remove(userId);
            spawnedRows.Remove(removedRow);

            if (removedRow != null)
            {
                DestroyChildObject(removedRow.gameObject);
            }
        }

        List<LobbyPlayerRowUI> orderedRows = new List<LobbyPlayerRowUI>();

        for (int i = 0; i < players.Count; i++)
        {
            PlayerListPlayerData playerData = players[i];

            if (playerData == null || string.IsNullOrWhiteSpace(playerData.userId))
            {
                continue;
            }

            bool isNewRow = !rowsByUserId.TryGetValue(playerData.userId, out LobbyPlayerRowUI row) || row == null;

            if (isNewRow)
            {
                row = Instantiate(rowPrefab, rowParent);
                row.gameObject.SetActive(true);
                rowsByUserId[playerData.userId] = row;
            }

            bool highlighted = !string.IsNullOrWhiteSpace(previousSelectedUserId) && previousSelectedUserId == playerData.userId;
            row.Setup(playerData, OnRowClicked, OnKickRequested, highlighted, isNewRow);
            row.transform.SetAsLastSibling();
            orderedRows.Add(row);
        }

        spawnedRows.Clear();
        spawnedRows.AddRange(orderedRows);

        selectedUserId = rowsByUserId.ContainsKey(previousSelectedUserId) ? previousSelectedUserId : string.Empty;
        SetSelectedUser(selectedUserId);
    }

    private List<PlayerListPlayerData> BuildLobbyPlayerListData(LobbyViewData lobbyViewData, string localUserId)
    {
        List<PlayerListPlayerData> players = new List<PlayerListPlayerData>();

        if (lobbyViewData?.players == null)
        {
            return players;
        }

        bool localPlayerIsHost =
            IsLocalPlayerHost(
                lobbyViewData.players,
                localUserId);

        bool modeAllowsKick =
            lobbyViewData.playMode ==
                MainMenuPlayMode.Solo ||
            lobbyViewData.playMode ==
                MainMenuPlayMode.Custom;

        for (int i = 0; i < lobbyViewData.players.Count; i++)
        {
            LobbyPlayerViewData lobbyPlayerData =
                lobbyViewData.players[i];

            if (lobbyPlayerData == null ||
                string.IsNullOrWhiteSpace(
                    lobbyPlayerData.userId))
            {
                continue;
            }

            PlayerListPlayerData playerData =
                new PlayerListPlayerData(
                    lobbyPlayerData);

            LobbyBoardData boardData = LobbyManager.instance != null ? LobbyManager.instance.GetPlayerBoard(playerData.userId) : null;

            if (boardData != null)
            {
                playerData.boardData = new LobbyBoardData(boardData);
            }

            playerData.canKick =
                localPlayerIsHost &&
                modeAllowsKick &&
                playerData.userId != localUserId &&
                !playerData.isHost;

            playerData.showBotIcon =
                localPlayerIsHost &&
                playerData.userTag == UserTag.Bot;

            playerData.showReadyIcon = true;

            players.Add(playerData);
        }

        return players;
    }

    public void UpdatePlayerBoard(string userId, LobbyBoardData boardData)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        if (!rowsByUserId.TryGetValue(userId, out LobbyPlayerRowUI row) || row == null)
        {
            return;
        }

        row.UpdateBoard(boardData);
    }

    public void UpdatePlayerBoard(string userId, LobbyBoardData boardData, IReadOnlyList<int> markedCellIndices)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        if (!rowsByUserId.TryGetValue(userId, out LobbyPlayerRowUI row) || row == null)
        {
            return;
        }

        row.UpdateBoard(boardData, markedCellIndices);
    }

    public void UpdatePlayerMarkedCells(string userId, IReadOnlyList<int> markedCellIndices)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        if (!rowsByUserId.TryGetValue(userId, out LobbyPlayerRowUI row) || row == null)
        {
            return;
        }

        row.UpdateMarkedCells(markedCellIndices);
    }

    public void SetPlayerMarkedCell(string userId, int cellIndex, bool isMarked)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        if (!rowsByUserId.TryGetValue(userId, out LobbyPlayerRowUI row) || row == null)
        {
            return;
        }

        row.SetMarkedCell(cellIndex, isMarked);
    }

    public void ClearRows()
    {
        if (rowParent != null)
        {
            for (int i = rowParent.childCount - 1; i >= 0; i--)
            {
                Transform child = rowParent.GetChild(i);

                if (rowPrefab != null && child == rowPrefab.transform)
                {
                    child.gameObject.SetActive(false);
                    continue;
                }

                DestroyChildObject(child.gameObject);
            }
        }

        spawnedRows.Clear();
        rowsByUserId.Clear();
    }

    private void OnRowClicked(string userId)
    {
        if (selectedUserId == userId)
        {
            SetSelectedUser(string.Empty);
            return;
        }

        SetSelectedUser(userId);
    }

    private void OnKickRequested(string userId)
    {
        KickRequested?.Invoke(userId);
    }

    private void SetSelectedUser(string userId)
    {
        selectedUserId = userId ?? string.Empty;

        for (int i = 0; i < spawnedRows.Count; i++)
        {
            LobbyPlayerRowUI row = spawnedRows[i];

            if (row == null)
            {
                continue;
            }

            row.SetHighlighted(
                !string.IsNullOrWhiteSpace(selectedUserId) &&
                row.UserId == selectedUserId);
        }
    }

    private void UpdatePlayerCount(int playerCount, int maxPlayers, bool unlimitedPlayers)
    {
        if (playerCountText == null)
        {
            return;
        }

        playerCountText.text = unlimitedPlayers
            ? $"Players {playerCount}"
            : $"Players {playerCount} / {maxPlayers}";
    }

    private bool IsLocalPlayerHost(IReadOnlyList<LobbyPlayerViewData> players, string localUserId)
    {
        if (players == null || string.IsNullOrWhiteSpace(localUserId))
        {
            return false;
        }

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerViewData playerData = players[i];

            if (playerData != null &&
                playerData.userId == localUserId)
            {
                return playerData.isHost;
            }
        }

        return false;
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
        int selectedIndex = GetSelectedRowIndex();

        if (selectedIndex < 0)
        {
            return;
        }

        int nextIndex = Mathf.Clamp(selectedIndex + direction, 0, spawnedRows.Count - 1);

        if (nextIndex == selectedIndex)
        {
            return;
        }

        LobbyPlayerRowUI nextRow = spawnedRows[nextIndex];

        if (nextRow == null)
        {
            return;
        }

        SetSelectedUser(nextRow.UserId);
        ScrollRowIntoView(nextRow);
    }

    private int GetSelectedRowIndex()
    {
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            LobbyPlayerRowUI row = spawnedRows[i];

            if (row != null && row.UserId == selectedUserId)
            {
                return i;
            }
        }

        return -1;
    }

    private void ScrollRowIntoView(LobbyPlayerRowUI row)
    {
        if (row == null || scrollRect == null || scrollRect.content == null)
        {
            return;
        }

        RectTransform contentRect = scrollRect.content;
        RectTransform viewportRect = scrollRect.viewport;

        if (viewportRect == null)
        {
            viewportRect = scrollRect.GetComponent<RectTransform>();
        }

        RectTransform rowRect = row.GetComponent<RectTransform>();

        if (rowRect == null || viewportRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        float contentHeight = contentRect.rect.height;
        float viewportHeight = viewportRect.rect.height;

        if (contentHeight <= viewportHeight)
        {
            return;
        }

        float rowTop = GetRowTopFromContentTop(rowRect);
        float rowBottom = rowTop + rowRect.rect.height;

        float viewTop = contentRect.anchoredPosition.y;
        float viewBottom = viewTop + viewportHeight;

        float targetY = contentRect.anchoredPosition.y;

        if (rowTop < viewTop)
        {
            targetY = rowTop;
        }
        else if (rowBottom > viewBottom)
        {
            targetY = rowBottom - viewportHeight;
        }

        float maxY = Mathf.Max(0f, contentHeight - viewportHeight);
        targetY = Mathf.Clamp(targetY, 0f, maxY);

        int rowIndex = spawnedRows.IndexOf(row);

        if (rowIndex == 0)
        {
            targetY = 0f;
        }
        else if (rowIndex == spawnedRows.Count - 1)
        {
            targetY = maxY;
        }

        contentRect.anchoredPosition = new Vector2(contentRect.anchoredPosition.x, targetY);
        scrollRect.StopMovement();

        if (rowIndex == 0)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }
        else if (rowIndex == spawnedRows.Count - 1)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private float GetRowTopFromContentTop(RectTransform rowRect)
    {
        return -rowRect.anchoredPosition.y - ((1f - rowRect.pivot.y) * rowRect.rect.height);
    }

    private void DestroyChildObject(GameObject childObject)
    {
        if (childObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(childObject);
        }
        else
        {
            DestroyImmediate(childObject);
        }
    }
}