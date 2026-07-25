using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbySceneController : MonoBehaviour, ILobbyView
{
    [Header("Sections")]
    [SerializeField] private LobbyHeaderController headerController;
    [SerializeField] private LobbyCustomPanelController customPanelController;
    [SerializeField] private LobbyBoardSectionController boardSectionController;
    [SerializeField] private LobbyPlayerListController playerListController;

    private LobbyController lobbyController;
    private Coroutine bindRoutine;
    private Coroutine localStartRoutine;

    private void OnEnable()
    {
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

        if (localStartRoutine != null)
        {
            StopCoroutine(localStartRoutine);
            localStartRoutine = null;
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

        playerListController?.DisplayLobbyInfo(
            lobbyViewData,
            lobbyManager != null
                ? lobbyManager.CurrentUserId
                : string.Empty);

        headerController?.DisplayLobbyInfo(lobbyViewData, CanOpenHostSettings(lobbyViewData), CanStartLobby(lobbyViewData));

        if (boardSectionController != null)
        {
            bool controlsInteractable = lobbyViewData.lobbyState == LobbyState.Open;

            boardSectionController.DisplayBoard(GetCurrentPlayerBoard(lobbyViewData));
            boardSectionController.SetBoardInteractable(false);
            boardSectionController.SetRerollInteractable(controlsInteractable);
            boardSectionController.SetReadyInteractable(controlsInteractable);
        }

        customPanelController?.DisplayLobbyInfo(lobbyViewData);

        TryLoadGameScene(lobbyViewData);
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

        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null || lobbyManager.CurrentLobby?.Controller == null)
        {
            return false;
        }

        LobbyPlayerData currentPlayer = lobbyManager.CurrentLobby.Controller.GetPlayer(lobbyManager.CurrentUserId);
        return currentPlayer != null && currentPlayer.isHost;
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

        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null || lobbyManager.CurrentLobby?.Controller == null)
        {
            return false;
        }

        LobbyPlayerData currentPlayer =
            lobbyManager.CurrentLobby.Controller.GetPlayer(
                lobbyManager.CurrentUserId);

        return currentPlayer != null && currentPlayer.isHost;
    }

    private IEnumerator BindWhenLobbyIsReady()
    {
        while (LobbyManager.instance == null ||
               !LobbyManager.instance.HasEnteredLobby ||
               LobbyManager.instance.CurrentLobby == null ||
               LobbyManager.instance.CurrentLobby.Controller == null)
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

            LobbyViewData lobbyViewData =
                lobbyManager.CurrentLobbyViewData ??
                lobbyManager.CurrentLobby.Controller.BuildViewData();

            DisplayLobbyInfo(lobbyViewData);
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

        boardSectionController?.DisplayBoard(boardData);
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
                lobbyController.PlayerBoardChanged -= OnLocalPlayerBoardChanged;
                lobbyController.PlayerBoardChanged += OnLocalPlayerBoardChanged;

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

        lobbyController.PlayerBoardChanged -= OnLocalPlayerBoardChanged;
        lobbyController.PlayerBoardChanged += OnLocalPlayerBoardChanged;

        lobbyController.BindView(this);
    }

    private void UnbindLobbyController()
    {
        if (lobbyController == null)
        {
            return;
        }

        lobbyController.PlayerBoardChanged -= OnLocalPlayerBoardChanged;
        lobbyController.UnbindView(this);

        lobbyController = null;
    }

    private void OnLocalPlayerBoardChanged(
    LobbyController controller,
    LobbyPlayerBoardViewData playerBoard)
    {
        if (playerBoard == null)
        {
            return;
        }

        ApplyPlayerBoardUpdate(
            playerBoard.userId,
            playerBoard.boardData);
    }

    private void StartLobby()
    {
        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null ||
            lobbyManager.CurrentLobby?.Controller == null)
        {
            Debug.LogWarning(
                "[LobbySceneController] Could not start the Lobby because the current Lobby was not found.");

            return;
        }

        if (lobbyManager.RuntimeType == SessionRuntimeType.Local)
        {
            LobbyController controller =
                lobbyManager.CurrentLobby.Controller;

            if (!controller.BeginFinalCountdown(lobbyManager.CurrentUserId))
            {
                return;
            }

            if (localStartRoutine != null)
            {
                StopCoroutine(localStartRoutine);
            }

            localStartRoutine =
                StartCoroutine(
                    WaitForLocalFinalCountdown(controller));

            return;
        }

        NetworkLobbyConnection lobbyConnection =
            NetworkLobbyConnection.GetLocalConnection();

        if (lobbyConnection == null)
        {
            Debug.LogWarning(
                "[LobbySceneController] Could not start the Custom Lobby because the network Lobby connection was not available.");

            return;
        }

        lobbyConnection.RequestStartLobby();
    }

    private IEnumerator WaitForLocalFinalCountdown(LobbyController controller)
    {
        while (controller != null &&
               controller.TimerEndTime > LobbyTimer.GetCurrentTime())
        {
            yield return null;
        }

        if (controller != null)
        {
            controller.CompleteFinalCountdown();
        }

        localStartRoutine = null;
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

        if (lobbyManager == null ||
            string.IsNullOrWhiteSpace(lobbyManager.CurrentUserId) ||
            lobbyViewData?.playerBoards == null)
        {
            return null;
        }

        for (int i = 0; i < lobbyViewData.playerBoards.Count; i++)
        {
            LobbyPlayerBoardViewData playerBoard = lobbyViewData.playerBoards[i];

            if (playerBoard != null && playerBoard.userId == lobbyManager.CurrentUserId)
            {
                return playerBoard.boardData;
            }
        }

        return null;
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
            lobbyManager.CurrentLobby?.Controller == null ||
            string.IsNullOrWhiteSpace(lobbyManager.CurrentUserId))
        {
            return;
        }

        if (lobbyManager.RuntimeType == SessionRuntimeType.Local)
        {
            lobbyManager.CurrentLobby.Controller.SetPlayerReady(
                lobbyManager.CurrentUserId,
                true);

            return;
        }

        NetworkLobbyConnection lobbyConnection =
            NetworkLobbyConnection.GetLocalConnection();

        if (lobbyConnection == null)
        {
            Debug.LogWarning(
                "[LobbySceneController] The network lobby connection was not available for Ready.");

            return;
        }

        lobbyConnection.RequestSetPlayerReady(true);
    }

    private void RerollBoard()
    {
        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null ||
            lobbyManager.CurrentLobby == null ||
            string.IsNullOrWhiteSpace(lobbyManager.CurrentUserId))
        {
            return;
        }

        if (lobbyManager.RuntimeType == SessionRuntimeType.Local)
        {
            lobbyManager.CurrentLobby.Controller?.RerollPlayerBoard(lobbyManager.CurrentUserId);
            return;
        }

        NetworkLobbyConnection lobbyConnection = NetworkLobbyConnection.GetLocalConnection();

        if (lobbyConnection == null)
        {
            Debug.LogWarning("[LobbySceneController] The network lobby connection was not available for board reroll.");
            return;
        }

        lobbyConnection.RequestRerollBoard();
    }

    private async void KickPlayer(string targetUserId)
    {
        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null ||
            lobbyManager.CurrentLobby?.Controller == null ||
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

        NetworkLobbyManager networkLobbyManager =
            NetworkLobbyManager.instance;

        if (networkLobbyManager == null ||
            !networkLobbyManager.IsReady)
        {
            Debug.LogWarning(
                "[LobbySceneController] NetworkLobbyManager was not ready for Kick.");

            return;
        }

        LobbyExitResult networkResult =
            await networkLobbyManager.KickPlayerAsync(targetUserId);

        if (networkResult == null || !networkResult.success)
        {
            Debug.LogWarning(
                $"[LobbySceneController] Kick failed: {networkResult?.failureMessage}");
        }
    }
}