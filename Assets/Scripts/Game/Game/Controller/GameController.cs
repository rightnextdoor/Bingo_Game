using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GameController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Sections")]
    [SerializeField] private GameHeaderController headerController;
    [SerializeField] private GameBoardSectionController boardSectionController;
    [SerializeField] private LobbyPlayerListController playerListController;
    [SerializeField] private GameInfoController gameInfoController;
    [SerializeField] private LobbyCustomPanelController customPanelController;

    #endregion

    #region Private Fields

    private readonly List<PlayerListPlayerData> visiblePlayers = new List<PlayerListPlayerData>();
    private readonly Dictionary<string, HashSet<int>> markedCellsByUserId =
        new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
    private Coroutine bindRoutine;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        SubscribeToHeader();
        SubscribeToBoardSection();
        bindRoutine = StartCoroutine(BindWhenGameIsReady());
    }

    private void OnDisable()
    {
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        UnsubscribeFromHeader();
        UnsubscribeFromBoardSection();
        UnsubscribeFromGameSessionManager();
    }

    #endregion

    #region Game Display

    public void DisplayGameInfo(GameSessionData gameSessionData)
    {
        if (gameSessionData == null)
        {
            ClearDisplay();
            return;
        }

        GameModeManager gameModeManager = GameModeManager.instance;
        BingoGameModeData gameModeData = gameModeManager != null
            ? gameModeManager.GetGameModeData(gameSessionData.gameModeType)
            : null;

        string gameName = gameModeData != null && !string.IsNullOrWhiteSpace(gameModeData.GameName)
            ? gameModeData.GameName
            : gameSessionData.gameModeType.ToString();

        headerController?.DisplayGameInfo(gameSessionData, gameName);
        DisplayPlayerBoard(gameSessionData);
        DisplayPlayerList(gameSessionData);
        DisplayGameModeInfo(gameSessionData, gameModeData, gameName, gameModeManager);
        DisplayCustomLobbyInfo(gameSessionData);
    }

    public void SetTimerSeconds(float remainingSeconds)
    {
        headerController?.SetTimerSeconds(remainingSeconds);
    }

    public void HideTimer()
    {
        headerController?.HideTimer();
    }

    private void DisplayPlayerList(GameSessionData gameSessionData)
    {
        visiblePlayers.Clear();
        bool localPlayerIsHost = gameSessionData?.GetPlayer(UserManager.instance?.UserId)?.isLobbyHost == true;

        if (gameSessionData?.players != null)
        {
            AddVisibleHost(gameSessionData.players, localPlayerIsHost);

            for (int i = 0; i < gameSessionData.players.Count; i++)
            {
                GamePlayerData playerData = gameSessionData.players[i];

                if (playerData == null || playerData.isLobbyHost)
                {
                    continue;
                }

                AddVisiblePlayer(playerData, localPlayerIsHost);
            }
        }

        playerListController?.DisplayPlayers(visiblePlayers, visiblePlayers.Count);
    }

    private void AddVisibleHost(IReadOnlyList<GamePlayerData> players, bool localPlayerIsHost)
    {
        for (int i = 0; i < players.Count; i++)
        {
            GamePlayerData playerData = players[i];

            if (playerData != null && playerData.isLobbyHost)
            {
                AddVisiblePlayer(playerData, localPlayerIsHost);
                return;
            }
        }
    }

    private void AddVisiblePlayer(GamePlayerData gamePlayerData, bool localPlayerIsHost)
    {
        if (gamePlayerData == null || !gamePlayerData.HasValidPlayer || !gamePlayerData.isGameSceneReady)
        {
            return;
        }

        PlayerListPlayerData playerData = new PlayerListPlayerData
        {
            userId = gamePlayerData.userId ?? string.Empty,
            userTag = gamePlayerData.userTag,
            playerName = gamePlayerData.playerName ?? string.Empty,
            iconId = gamePlayerData.iconId ?? string.Empty,
            isHost = gamePlayerData.isLobbyHost,
            isReady = true,
            boardData = new LobbyBoardData(gamePlayerData.boardData),
            markedCellIndices = GetMarkedCellSnapshot(gamePlayerData.userId),
            canKick = false,
            showBotIcon = localPlayerIsHost && gamePlayerData.userTag == UserTag.Bot,
            showReadyIcon = false
        };

        visiblePlayers.Add(playerData);
    }

    private void DisplayGameModeInfo(
        GameSessionData gameSessionData,
        BingoGameModeData gameModeData,
        string gameName,
        GameModeManager gameModeManager)
    {
        string gameDescription = gameModeData != null && !string.IsNullOrWhiteSpace(gameModeData.Description)
            ? gameModeData.Description
            : "No game information is available for this game mode.";

        string ruleDescription = string.Empty;

        if (gameSessionData.hasRule)
        {
            BingoGameRuleData ruleData = gameModeManager != null
                ? gameModeManager.GetGameRuleData(gameSessionData.ruleType)
                : null;

            ruleDescription = ruleData != null && !string.IsNullOrWhiteSpace(ruleData.Description)
                ? ruleData.Description
                : "No rule description is available for this game mode.";
        }

        gameInfoController?.ShowGameInfo(
            gameName,
            gameDescription,
            gameSessionData.ballCountType,
            gameSessionData.hasRule,
            ruleDescription,
            gameSessionData.patternTypes);
    }

    private void DisplayCustomLobbyInfo(GameSessionData gameSessionData)
    {
        LobbyViewData lobbyViewData = new LobbyViewData
        {
            lobbyId = gameSessionData.lobbyId ?? string.Empty,
            playMode = gameSessionData.playMode,
            lobbyName = gameSessionData.lobbyName ?? string.Empty,
            roomCode = gameSessionData.roomCode ?? string.Empty,
            hasPassword = gameSessionData.hasPassword,
            lobbyPassword = gameSessionData.lobbyPassword ?? string.Empty
        };

        customPanelController?.DisplayLobbyInfo(lobbyViewData);
    }

    private void ClearDisplay()
    {
        visiblePlayers.Clear();
        markedCellsByUserId.Clear();
        headerController?.ClearHeader();
        boardSectionController?.ClearBoard();
        boardSectionController?.SetBoardInteractable(false);
        boardSectionController?.SetBingoInteractable(false);
        playerListController?.DisplayPlayers(visiblePlayers, 0);
        gameInfoController?.ClearInfo();
        customPanelController?.DisplayLobbyInfo(null);
    }

    #endregion

    #region Board

    private void DisplayPlayerBoard(GameSessionData gameSessionData)
    {
        if (boardSectionController == null)
        {
            return;
        }

        string localUserId = UserManager.instance?.UserId;
        GamePlayerData localPlayer = gameSessionData?.GetPlayer(localUserId);

        bool boardDisplayed =
            localPlayer?.boardData != null &&
            boardSectionController.DisplayBoard(localPlayer.boardData);

        if (!boardDisplayed)
        {
            boardSectionController.ClearBoard();
        }

        boardSectionController.SetBoardInteractable(boardDisplayed);
        boardSectionController.SetBingoInteractable(boardDisplayed);
    }

    private void SubscribeToBoardSection()
    {
        if (boardSectionController == null)
        {
            return;
        }

        boardSectionController.BingoRequested -= OnBingoRequested;
        boardSectionController.BingoRequested += OnBingoRequested;

        boardSectionController.MarkedCellChanged -= OnBoardMarkedCellChanged;
        boardSectionController.MarkedCellChanged += OnBoardMarkedCellChanged;
    }

    private void UnsubscribeFromBoardSection()
    {
        if (boardSectionController != null)
        {
            boardSectionController.BingoRequested -= OnBingoRequested;
            boardSectionController.MarkedCellChanged -= OnBoardMarkedCellChanged;
        }
    }

    private void OnBoardMarkedCellChanged(int cellIndex, bool isMarked)
    {
        if (GameSessionManager.instance == null ||
            !GameSessionManager.instance.SetCurrentPlayerMarkedCell(cellIndex, isMarked))
        {
            Debug.LogWarning(
                $"[GameController] Could not update marked cell {cellIndex} for the current player.");
        }
    }

    private void OnBingoRequested(
        LobbyBoardData boardData,
        IReadOnlyList<int> markedCellIndices)
    {
        if (boardData?.cellNumbers == null)
        {
            Debug.LogWarning("[GameController] Bingo was pressed, but the board data was unavailable.");
            return;
        }

        List<int> sortedCellIndices = markedCellIndices != null
            ? new List<int>(markedCellIndices)
            : new List<int>();

        sortedCellIndices.Sort();

        List<string> selectedCells = new List<string>();
        const string columnLetters = "BINGO";

        for (int i = 0; i < sortedCellIndices.Count; i++)
        {
            int cellIndex = sortedCellIndices[i];

            if (cellIndex < 0 || cellIndex >= boardData.cellNumbers.Count)
            {
                continue;
            }

            if (boardData.usesFreeCell && cellIndex == 12)
            {
                selectedCells.Add("FREE");
                continue;
            }

            int columnIndex = cellIndex % columnLetters.Length;
            selectedCells.Add($"{columnLetters[columnIndex]}{boardData.cellNumbers[cellIndex]}");
        }

        string selectedCellText = selectedCells.Count > 0
            ? string.Join(", ", selectedCells)
            : "None";

        Debug.Log($"[GameController] Bingo pressed. Selected cells: {selectedCellText}");
    }

    #endregion

    #region Session Binding

    private IEnumerator BindWhenGameIsReady()
    {
        while (GameSessionManager.instance == null ||
               !GameSessionManager.instance.HasEnteredGame ||
               GameSessionManager.instance.CurrentGameSession == null ||
               GameModeManager.instance == null ||
               !GameModeManager.instance.IsReady)
        {
            yield return null;
        }

        GameSessionManager gameSessionManager = GameSessionManager.instance;

        gameSessionManager.GameSessionUpdated -= OnGameSessionUpdated;
        gameSessionManager.GameSessionUpdated += OnGameSessionUpdated;

        gameSessionManager.GamePlayerMarkedCellChanged -= OnGamePlayerMarkedCellChanged;
        gameSessionManager.GamePlayerMarkedCellChanged += OnGamePlayerMarkedCellChanged;

        DisplayGameInfo(gameSessionManager.CurrentGameSession);
        bindRoutine = null;
    }

    private void OnGameSessionUpdated(GameSessionData gameSessionData)
    {
        DisplayGameInfo(gameSessionData);
    }

    private void OnGamePlayerMarkedCellChanged(
        GamePlayerMarkedCellChangedData updateData)
    {
        if (updateData == null || string.IsNullOrWhiteSpace(updateData.userId))
        {
            return;
        }

        if (!markedCellsByUserId.TryGetValue(
                updateData.userId,
                out HashSet<int> markedCells))
        {
            markedCells = new HashSet<int>();
            markedCellsByUserId.Add(updateData.userId, markedCells);
        }

        if (updateData.isMarked)
        {
            markedCells.Add(updateData.cellIndex);
        }
        else
        {
            markedCells.Remove(updateData.cellIndex);
        }

        playerListController?.SetPlayerMarkedCell(
            updateData.userId,
            updateData.cellIndex,
            updateData.isMarked);
    }

    private List<int> GetMarkedCellSnapshot(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            !markedCellsByUserId.TryGetValue(userId, out HashSet<int> markedCells))
        {
            return new List<int>();
        }

        List<int> snapshot = new List<int>(markedCells);
        snapshot.Sort();
        return snapshot;
    }

    private void UnsubscribeFromGameSessionManager()
    {
        if (GameSessionManager.instance != null)
        {
            GameSessionManager.instance.GameSessionUpdated -= OnGameSessionUpdated;
            GameSessionManager.instance.GamePlayerMarkedCellChanged -= OnGamePlayerMarkedCellChanged;
        }
    }

    #endregion

    #region Header

    private void SubscribeToHeader()
    {
        if (headerController == null)
        {
            return;
        }

        headerController.LeaveRequested -= LeaveGame;
        headerController.LeaveRequested += LeaveGame;
    }

    private void UnsubscribeFromHeader()
    {
        if (headerController != null)
        {
            headerController.LeaveRequested -= LeaveGame;
        }
    }

    private void LeaveGame()
    {
        headerController?.SetLeaveInteractable(false);

        if (GameSessionManager.instance != null)
        {
            GameSessionManager.instance.LeaveCurrentGame();
            return;
        }

        UserManager.instance?.ClearLastGameId();
        GameSceneManager.instance?.LoadMainScene();
    }

    #endregion
}
