using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbySceneController : MonoBehaviour, ILobbyView
{
    [Header("Sections")]
    [SerializeField] private LobbyHeaderController headerController;
    [SerializeField] private LobbyCustomPanelController customPanelController;
    [SerializeField] private LobbyBoardSectionController boardSectionController;
    [SerializeField] private LobbyPlayerListController playerListController;
    [SerializeField] private LobbyGameInfoController gameInfoController;

    private LobbyController lobbyController;
    private Coroutine bindRoutine;
    private LobbyViewData displayedLobby;
    private LobbyBoardData displayedBoard;
    private string displayedUserId;
    private bool displayedHostSettings;
    private bool displayedStart;

    private void OnEnable()
    {
        displayedLobby = null;
        displayedBoard = null;
        SubscribeToHeader();
        SubscribeToBoardSection();
        SubscribeToPlayerList();
        bindRoutine = StartCoroutine(BindWhenLobbyIsReady());
    }

    private void OnDisable()
    {
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        UnsubscribeFromHeader();
        UnsubscribeFromLobbyManager();
        UnsubscribeFromBoardSection();
        UnsubscribeFromPlayerList();
        UnbindLobbyController();
    }

    public void DisplayLobbyInfo(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData == null)
        {
            return;
        }

        LobbyManager lobbyManager = LobbyManager.instance;

        string userId = lobbyManager != null ? lobbyManager.CurrentUserId : string.Empty;
        bool initialDisplay = displayedLobby == null || displayedLobby.lobbyId != lobbyViewData.lobbyId || displayedUserId != userId;
        bool canOpenSettings = CanOpenHostSettings(lobbyViewData);
        bool canStart = CanStartLobby(lobbyViewData);

        if (initialDisplay || !SamePlayers(displayedLobby, lobbyViewData))
        {
            playerListController?.DisplayLobbyInfo(lobbyViewData, userId);
        }

        if (initialDisplay || displayedHostSettings != canOpenSettings || displayedStart != canStart ||
            displayedLobby.playMode != lobbyViewData.playMode || displayedLobby.gameModeType != lobbyViewData.gameModeType ||
            displayedLobby.gameModeName != lobbyViewData.gameModeName || displayedLobby.lobbyState != lobbyViewData.lobbyState ||
            displayedLobby.isTimerActive != lobbyViewData.isTimerActive || displayedLobby.timerEndTime != lobbyViewData.timerEndTime)
        {
            headerController?.DisplayLobbyInfo(lobbyViewData, canOpenSettings, canStart);
        }

        if (initialDisplay || displayedLobby.gameModeType != lobbyViewData.gameModeType ||
            displayedLobby.gameModeName != lobbyViewData.gameModeName || displayedLobby.ballCountType != lobbyViewData.ballCountType ||
            displayedLobby.hasRule != lobbyViewData.hasRule ||
            (lobbyViewData.hasRule && displayedLobby.ruleType != lobbyViewData.ruleType) ||
            !SameValues(displayedLobby.patternTypes, lobbyViewData.patternTypes))
        {
            gameInfoController?.DisplayLobbyInfo(lobbyViewData);
        }

        LobbyBoardData board = GetCurrentPlayerBoard(lobbyViewData);
        if (initialDisplay || !SameBoard(displayedBoard, board))
        {
            boardSectionController?.DisplayBoard(board);
            displayedBoard = board != null ? new LobbyBoardData(board) : null;
        }

        if (initialDisplay || displayedLobby.lobbyState != lobbyViewData.lobbyState)
        {
            bool controlsInteractable = lobbyViewData.lobbyState == LobbyState.Open;
            boardSectionController?.SetBoardInteractable(false);
            boardSectionController?.SetRerollInteractable(controlsInteractable);
            boardSectionController?.SetReadyInteractable(controlsInteractable);
        }

        if (initialDisplay || displayedLobby.playMode != lobbyViewData.playMode ||
            displayedLobby.lobbyName != lobbyViewData.lobbyName || displayedLobby.roomCode != lobbyViewData.roomCode ||
            displayedLobby.hasPassword != lobbyViewData.hasPassword || displayedLobby.lobbyPassword != lobbyViewData.lobbyPassword)
        {
            customPanelController?.DisplayLobbyInfo(lobbyViewData);
        }

        CaptureLobbyDisplay(lobbyViewData);
        displayedUserId = userId;
        displayedHostSettings = canOpenSettings;
        displayedStart = canStart;

        TryLoadGameScene(lobbyViewData);
    }

    private static bool SameValues<T>(IReadOnlyList<T> _first, IReadOnlyList<T> _second)
    {
        int count = _first?.Count ?? 0;
        if (count != (_second?.Count ?? 0)) return false;
        for (int i = 0; i < count; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(_first[i], _second[i])) return false;
        }
        return true;
    }

    private static bool SameBoard(LobbyBoardData _first, LobbyBoardData _second)
    {
        if (_first == null || _second == null) return _first == _second;
        return _first.ballCountType == _second.ballCountType && _first.usesFreeCell == _second.usesFreeCell &&
               SameValues(_first.cellNumbers, _second.cellNumbers);
    }


    private static bool SamePlayers(LobbyViewData _first, LobbyViewData _second)
    {
        if (_first.playMode != _second.playMode || _first.playerCount != _second.playerCount ||
            _first.maxPlayer != _second.maxPlayer || _first.maxPlayers != _second.maxPlayers ||
            (_first.players?.Count ?? 0) != (_second.players?.Count ?? 0)) return false;
        for (int i = 0; i < (_first.players?.Count ?? 0); i++)
        {
            LobbyPlayerViewData first = _first.players[i];
            LobbyPlayerViewData second = _second.players[i];
            if (first == null || second == null)
            {
                if (first != second) return false;
                continue;
            }
            if (first.userId != second.userId || first.userTag != second.userTag || first.playerName != second.playerName ||
                first.iconId != second.iconId || first.isHost != second.isHost || first.isReady != second.isReady) return false;
        }
        return true;
    }

    private void CaptureLobbyDisplay(LobbyViewData _data)
    {
        if (displayedLobby == null) displayedLobby = new LobbyViewData();
        displayedLobby.lobbyId = _data.lobbyId;
        displayedLobby.playMode = _data.playMode;
        displayedLobby.lobbyState = _data.lobbyState;
        displayedLobby.isTimerActive = _data.isTimerActive;
        displayedLobby.timerEndTime = _data.timerEndTime;
        displayedLobby.lobbyName = _data.lobbyName;
        displayedLobby.roomCode = _data.roomCode;
        displayedLobby.hasPassword = _data.hasPassword;
        displayedLobby.lobbyPassword = _data.lobbyPassword;
        displayedLobby.gameModeType = _data.gameModeType;
        displayedLobby.gameModeName = _data.gameModeName;
        displayedLobby.hasRule = _data.hasRule;
        displayedLobby.ruleType = _data.ruleType;
        displayedLobby.ballCountType = _data.ballCountType;
        displayedLobby.playerCount = _data.playerCount;
        displayedLobby.maxPlayer = _data.maxPlayer;
        displayedLobby.maxPlayers = _data.maxPlayers;
        displayedLobby.patternTypes.Clear();
        if (_data.patternTypes != null) displayedLobby.patternTypes.AddRange(_data.patternTypes);
        int count = _data.players?.Count ?? 0;
        while (displayedLobby.players.Count > count) displayedLobby.players.RemoveAt(displayedLobby.players.Count - 1);
        for (int i = 0; i < count; i++)
        {
            if (i == displayedLobby.players.Count) displayedLobby.players.Add(null);
            LobbyPlayerViewData player = _data.players[i];
            if (player == null)
            {
                displayedLobby.players[i] = null;
                continue;
            }
            LobbyPlayerViewData copy = displayedLobby.players[i];
            if (copy == null) displayedLobby.players[i] = copy = new LobbyPlayerViewData();
            copy.userId = player.userId;
            copy.userTag = player.userTag;
            copy.playerName = player.playerName;
            copy.iconId = player.iconId;
            copy.isHost = player.isHost;
            copy.isReady = player.isReady;
        }
    }

    private void LeaveLobby()
    {
        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null)
        {
            Debug.LogWarning("[LobbySceneController] Could not leave the Lobby because LobbyManager was not found.");
            return;
        }

        lobbyManager.LeaveCurrentLobby();
    }

    private void OpenHostSettings()
    {
        PopupManager popupManager = PopupManager.instance;

        if (popupManager == null)
        {
            Debug.LogWarning("[LobbySceneController] Could not open Host Settings because PopupManager was not found.");
            return;
        }

        popupManager.OpenPopup(PopupId.HostSettings);
    }

    private bool CanOpenHostSettings(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData.playMode == MainMenuPlayMode.Solo)
        {
            return true;
        }

        if (lobbyViewData.playMode != MainMenuPlayMode.Custom)
        {
            return false;
        }

        return IsCurrentPlayerHost(lobbyViewData);
    }

    private bool CanStartLobby(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData == null || lobbyViewData.lobbyState != LobbyState.Open)
        {
            return false;
        }

        if (lobbyViewData.playMode == MainMenuPlayMode.Solo)
        {
            return true;
        }

        if (lobbyViewData.playMode != MainMenuPlayMode.Custom)
        {
            return false;
        }

        return IsCurrentPlayerHost(lobbyViewData);
    }

    private bool IsCurrentPlayerHost(LobbyViewData lobbyViewData)
    {
        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null || lobbyViewData?.players == null || string.IsNullOrWhiteSpace(lobbyManager.CurrentUserId))
        {
            return false;
        }

        for (int i = 0; i < lobbyViewData.players.Count; i++)
        {
            LobbyPlayerViewData playerData = lobbyViewData.players[i];

            if (playerData != null && playerData.userId == lobbyManager.CurrentUserId)
            {
                return playerData.isHost;
            }
        }

        return false;
    }

    private IEnumerator BindWhenLobbyIsReady()
    {
        while (LobbyManager.instance == null ||
               !LobbyManager.instance.HasEnteredLobby ||
               LobbyManager.instance.CurrentLobbyViewData == null ||
               (LobbyManager.instance.RuntimeType == SessionRuntimeType.Local && LobbyManager.instance.CurrentLobby?.Controller == null))
        {
            yield return null;
        }

        LobbyManager lobbyManager = LobbyManager.instance;

        lobbyManager.LobbyViewUpdated -= OnLobbyViewUpdated;
        lobbyManager.LobbyViewUpdated += OnLobbyViewUpdated;

        lobbyManager.LobbyPlayerBoardUpdated -= OnLobbyPlayerBoardUpdated;
        lobbyManager.LobbyPlayerBoardUpdated += OnLobbyPlayerBoardUpdated;

        if (lobbyManager.RuntimeType == SessionRuntimeType.Network)
        {
            UnbindLobbyController();

            DisplayLobbyInfo(lobbyManager.CurrentLobbyViewData);
        }
        else
        {
            BindLobbyController(
                lobbyManager.CurrentLobby.Controller);
        }

        bindRoutine = null;
    }

    private void OnLobbyViewUpdated(LobbyViewData lobbyViewData)
    {
        DisplayLobbyInfo(lobbyViewData);
    }

    private void OnLobbyPlayerBoardUpdated(
    LobbyPlayerBoardUpdateData updateData)
    {
        if (updateData == null)
        {
            return;
        }

        ApplyPlayerBoardUpdate(
            updateData.userId,
            updateData.boardData);
    }

    private void ApplyPlayerBoardUpdate(
    string userId,
    LobbyBoardData boardData)
    {
        if (string.IsNullOrWhiteSpace(userId) || boardData == null)
        {
            return;
        }

        playerListController?.UpdatePlayerBoard(
            userId,
            boardData);

        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null ||
            userId != lobbyManager.CurrentUserId)
        {
            return;
        }

        if (!SameBoard(displayedBoard, boardData))
        {
            boardSectionController?.DisplayBoard(boardData);
            displayedBoard = new LobbyBoardData(boardData);
        }
    }

    private void SubscribeToHeader()
    {
        if (headerController == null)
        {
            return;
        }

        headerController.LeaveRequested -= LeaveLobby;
        headerController.LeaveRequested += LeaveLobby;

        headerController.StartRequested -= StartLobby;
        headerController.StartRequested += StartLobby;

        headerController.HostSettingsRequested -= OpenHostSettings;
        headerController.HostSettingsRequested += OpenHostSettings;
    }

    private void UnsubscribeFromHeader()
    {
        if (headerController == null)
        {
            return;
        }

        headerController.LeaveRequested -= LeaveLobby;
        headerController.StartRequested -= StartLobby;
        headerController.HostSettingsRequested -= OpenHostSettings;
    }

    private void UnsubscribeFromLobbyManager()
    {
        if (LobbyManager.instance == null)
        {
            return;
        }

        LobbyManager.instance.LobbyViewUpdated -= OnLobbyViewUpdated;
        LobbyManager.instance.LobbyPlayerBoardUpdated -= OnLobbyPlayerBoardUpdated;
    }

    private void SubscribeToPlayerList()
    {
        if (playerListController == null)
        {
            return;
        }

        playerListController.KickRequested -= KickPlayer;
        playerListController.KickRequested += KickPlayer;
    }

    private void UnsubscribeFromPlayerList()
    {
        if (playerListController != null)
        {
            playerListController.KickRequested -= KickPlayer;
        }
    }

    private void BindLobbyController(LobbyController controller)
    {
        if (lobbyController == controller)
        {
            if (lobbyController != null)
            {
                lobbyController.FinalCountdownStarted -= OnLocalFinalCountdownStarted;
                lobbyController.FinalCountdownStarted += OnLocalFinalCountdownStarted;

                lobbyController.RefreshViews();
            }

            return;
        }

        UnbindLobbyController();

        lobbyController = controller;

        if (lobbyController == null)
        {
            return;
        }

        lobbyController.FinalCountdownStarted -= OnLocalFinalCountdownStarted;
        lobbyController.FinalCountdownStarted += OnLocalFinalCountdownStarted;

        lobbyController.BindView(this);
    }

    private void UnbindLobbyController()
    {
        if (lobbyController == null)
        {
            return;
        }

        lobbyController.FinalCountdownStarted -= OnLocalFinalCountdownStarted;
        lobbyController.UnbindView(this);

        lobbyController = null;
    }

    private void OnLocalFinalCountdownStarted(LobbyController controller)
    {
        NotificationService.instance?.SendLocal(UIMessageType.GameAboutToStart);
    }

    private void SaveCurrentHostLobbySettings(LobbyManager lobbyManager)
    {
        if (lobbyManager == null || !lobbyManager.HasEnteredLobby)
        {
            return;
        }

        LobbyViewData lobbyViewData = lobbyManager.RuntimeType == SessionRuntimeType.Local
            ? lobbyManager.CurrentLobby?.Controller?.BuildViewData()
            : lobbyManager.CurrentLobbyViewData;

        if (lobbyViewData == null)
        {
            return;
        }

        if (lobbyViewData.playMode == MainMenuPlayMode.Solo)
        {
            LobbySaveDataService.SaveLobbyViewData(lobbyViewData);
            return;
        }

        if (lobbyViewData.playMode == MainMenuPlayMode.Custom && IsCurrentPlayerHost(lobbyViewData))
        {
            LobbySaveDataService.SaveLobbyViewData(lobbyViewData);
        }
    }

    private void StartLobby()
    {
        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null || !lobbyManager.HasEnteredLobby)
        {
            Debug.LogWarning("[LobbySceneController] Could not start the Lobby because the current Lobby was not found.");
            return;
        }

        if (lobbyManager.RuntimeType == SessionRuntimeType.Local)
        {
            LobbyController controller = lobbyManager.CurrentLobby?.Controller;

            if (controller == null)
            {
                return;
            }
            LobbySettings lobbySettings = LobbySettings.instance;

            if (lobbySettings != null && controller.PlayerCount < lobbySettings.MinimumPlayers)
            {
                string message = $"At least {lobbySettings.MinimumPlayers} players are required to start the game.";
                NotificationService.instance?.SendLocal(UIMessageType.NotEnoughPlayers, message);
                return;
            }

            SaveCurrentHostLobbySettings(lobbyManager);

            if (!controller.BeginFinalCountdown(lobbyManager.CurrentUserId))
            {
                return;
            }

            return;
        }

        NetworkLobbyService lobbyService = NetworkLobbyService.instance;

        if (lobbyService == null || !lobbyService.IsReady)
        {
            Debug.LogWarning("[LobbySceneController] Could not start the Custom Lobby because the network Lobby service was not available.");
            return;
        }

        SaveCurrentHostLobbySettings(lobbyManager);
        lobbyService.StartLobby();
    }

    private void TryLoadGameScene(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData == null || lobbyViewData.lobbyState != LobbyState.InGame)
        {
            return;
        }

        GameSceneManager gameSceneManager = GameSceneManager.instance;

        if (gameSceneManager == null || gameSceneManager.IsLoadingScene)
        {
            return;
        }

        gameSceneManager.LoadGameScene();
    }

    private LobbyBoardData GetCurrentPlayerBoard(LobbyViewData lobbyViewData)
    {
        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null || string.IsNullOrWhiteSpace(lobbyManager.CurrentUserId))
        {
            return null;
        }

        return lobbyManager.GetPlayerBoard(lobbyManager.CurrentUserId);
    }

    private void SubscribeToBoardSection()
    {
        if (boardSectionController == null)
        {
            return;
        }

        boardSectionController.RerollRequested -= RerollBoard;
        boardSectionController.RerollRequested += RerollBoard;

        boardSectionController.ReadyRequested -= ReadyPlayer;
        boardSectionController.ReadyRequested += ReadyPlayer;
    }

    private void UnsubscribeFromBoardSection()
    {
        if (boardSectionController == null)
        {
            return;
        }

        boardSectionController.RerollRequested -= RerollBoard;
        boardSectionController.ReadyRequested -= ReadyPlayer;
    }

    private void ReadyPlayer()
    {
        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null ||
            !lobbyManager.HasEnteredLobby ||
            string.IsNullOrWhiteSpace(lobbyManager.CurrentUserId))
        {
            return;
        }

        bool nextReadyState = !GetCurrentPlayerReadyState(lobbyManager);

        if (lobbyManager.RuntimeType == SessionRuntimeType.Local)
        {
            lobbyManager.CurrentLobby.Controller.SetPlayerReady(
                lobbyManager.CurrentUserId,
                nextReadyState);

            return;
        }

        NetworkLobbyService lobbyService = NetworkLobbyService.instance;

        if (lobbyService == null || !lobbyService.IsReady)
        {
            Debug.LogWarning("[LobbySceneController] The network lobby service was not available for Ready.");
            return;
        }

        lobbyService.SetPlayerReady(nextReadyState);
    }

    private bool GetCurrentPlayerReadyState(LobbyManager lobbyManager)
    {
        if (lobbyManager == null ||
            string.IsNullOrWhiteSpace(lobbyManager.CurrentUserId))
        {
            return false;
        }

        if (lobbyManager.RuntimeType == SessionRuntimeType.Local)
        {
            LobbyPlayerData playerData =
                lobbyManager.CurrentLobby?.Controller?.GetPlayer(
                    lobbyManager.CurrentUserId);

            return playerData != null && playerData.isReady;
        }

        LobbyViewData lobbyViewData =
            lobbyManager.CurrentLobbyViewData;

        if (lobbyViewData?.players == null)
        {
            return false;
        }

        for (int i = 0; i < lobbyViewData.players.Count; i++)
        {
            LobbyPlayerViewData playerData =
                lobbyViewData.players[i];

            if (playerData != null &&
                playerData.userId == lobbyManager.CurrentUserId)
            {
                return playerData.isReady;
            }
        }

        return false;
    }

    private void RerollBoard()
    {
        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null ||
            !lobbyManager.HasEnteredLobby ||
            string.IsNullOrWhiteSpace(lobbyManager.CurrentUserId))
        {
            return;
        }

        if (lobbyManager.RuntimeType == SessionRuntimeType.Local)
        {
            lobbyManager.CurrentLobby?.Controller?.RerollPlayerBoard(lobbyManager.CurrentUserId);
            return;
        }

        NetworkLobbyService lobbyService = NetworkLobbyService.instance;

        if (lobbyService == null || !lobbyService.IsReady)
        {
            Debug.LogWarning("[LobbySceneController] The network lobby service was not available for board reroll.");
            return;
        }

        lobbyService.RerollBoard();
    }

    private async void KickPlayer(string targetUserId)
    {
        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null ||
            !lobbyManager.HasEnteredLobby ||
            string.IsNullOrWhiteSpace(targetUserId))
        {
            return;
        }

        if (lobbyManager.RuntimeType == SessionRuntimeType.Local)
        {
            LobbyExitResult result =
                lobbyManager.CurrentLobby.Controller.KickPlayer(
                    lobbyManager.CurrentUserId,
                    targetUserId);

            if (!result.success)
            {
                Debug.LogWarning(
                    $"[LobbySceneController] Kick failed: {result.failureMessage}");
            }

            return;
        }

        NetworkLobbyService lobbyService = NetworkLobbyService.instance;

        if (lobbyService == null || !lobbyService.IsReady)
        {
            Debug.LogWarning("[LobbySceneController] NetworkLobbyService was not ready for Kick.");
            return;
        }

        LobbyExitResult networkResult = await lobbyService.KickPlayerAsync(targetUserId);

        if (networkResult == null || !networkResult.success)
        {
            Debug.LogWarning(
                $"[LobbySceneController] Kick failed: {networkResult?.failureMessage}");
        }
    }
}
