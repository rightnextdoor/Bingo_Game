using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbySceneController : MonoBehaviour, ILobbyView
{
    [Header("Sections")]
    [SerializeField] private LobbyHeaderController headerController;
    [SerializeField] private LobbyCustomPanelController customPanelController;

    private LobbyController lobbyController;
    private Coroutine bindRoutine;
    private Coroutine localStartRoutine;

    private void OnEnable()
    {
        SubscribeToHeader();
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
        UnbindLobbyController();
    }

    public void DisplayLobbyInfo(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData == null)
        {
            return;
        }

        headerController?.DisplayLobbyInfo(
            lobbyViewData,
            CanOpenHostSettings(lobbyViewData),
            CanStartLobby(lobbyViewData));

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

        if (lobbyManager.RuntimeType == SessionRuntimeType.Network)
        {
            UnbindLobbyController();

            LobbyViewData lobbyViewData = lobbyManager.CurrentLobbyViewData ?? lobbyManager.CurrentLobby.Controller.BuildViewData();
            DisplayLobbyInfo(lobbyViewData);
        }
        else
        {
            BindLobbyController(lobbyManager.CurrentLobby.Controller);
        }

        bindRoutine = null;
    }

    private void OnLobbyViewUpdated(LobbyViewData lobbyViewData)
    {
        DisplayLobbyInfo(lobbyViewData);
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
        if (LobbyManager.instance != null)
        {
            LobbyManager.instance.LobbyViewUpdated -= OnLobbyViewUpdated;
        }
    }

    private void BindLobbyController(LobbyController controller)
    {
        if (lobbyController == controller)
        {
            lobbyController.RefreshViews();
            return;
        }

        UnbindLobbyController();

        lobbyController = controller;
        lobbyController?.BindView(this);
    }

    private void UnbindLobbyController()
    {
        if (lobbyController == null)
        {
            return;
        }

        lobbyController.UnbindView(this);
        lobbyController = null;
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
}