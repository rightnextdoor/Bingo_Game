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
    [SerializeField] private LobbyGameInfoController gameInfoController;

    private LobbyController lobbyController;
    private Coroutine bindRoutine;

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

        gameInfoController?.DisplayLobbyInfo(lobbyViewData);

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
