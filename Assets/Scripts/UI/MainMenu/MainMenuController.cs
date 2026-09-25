using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Screens")]
    [SerializeField] private GameObject landingScreen;
    [SerializeField] private GameObject modeSelectScreen;
    [SerializeField] private GameObject modeSetupScreen;

    [Header("Landing Screen")]
    [SerializeField] private Button landingPlayButton;

    [Header("Mode Select Screen")]
    [SerializeField] private Button soloButton;
    [SerializeField] private Button onlineButton;
    [SerializeField] private Button customButton;
    [SerializeField] private Button quitButton;

    [Header("Mode Setup Screen")]
    [SerializeField] private MainMenuSettingsController settingsController;
    [SerializeField] private MainMenuGameInfoController gameInfoController;
    [SerializeField] private TMP_Text modeTitleText;
    [SerializeField] private ScrollRect settingsScrollRect;
    [SerializeField] private Button setupBackButton;
    [SerializeField] private Button setupPlayButton;

    private MainMenuPlayMode selectedMode = MainMenuPlayMode.None;
    private bool isStartingSelectedMode;
    private bool isCheckingPreviousGame;
    private bool connectionRecoveredOnPlay;

    private UserManager userManager;
    private PopupManager popupManager;
    private GameSceneManager gameSceneManager;
    private GameManager gameManager;
    private LobbyManager lobbyManager;

    private void Awake()
    {
        CacheManagers();
    }

    private void OnEnable()
    {
        CacheManagers();
        RegisterButtonListeners();
        ShowLandingScreen();
    }

    private void OnDisable()
    {
        UnregisterButtonListeners();
    }
    private void CacheManagers()
    {
        if (userManager == null)
        {
            userManager = UserManager.instance;
        }

        if (popupManager == null)
        {
            popupManager = PopupManager.instance;
        }

        if (gameSceneManager == null)
        {
            gameSceneManager = GameSceneManager.instance;
        }

        if (gameManager == null)
        {
            gameManager = GameManager.instance;
        }

        if (settingsController == null)
        {
            settingsController = GetComponentInChildren<MainMenuSettingsController>(true);
        }

        if (lobbyManager == null)
        {
            lobbyManager = LobbyManager.instance;
        }

        if (gameInfoController == null)
        {
            gameInfoController = GetComponentInChildren<MainMenuGameInfoController>(true);
        }
    }

    private void RegisterButtonListeners()
    {
        if (landingPlayButton != null)
        {
            landingPlayButton.onClick.RemoveListener(OnLandingPlayButtonClicked);
            landingPlayButton.onClick.AddListener(OnLandingPlayButtonClicked);
        }

        if (soloButton != null)
        {
            soloButton.onClick.RemoveListener(OnSoloButtonClicked);
            soloButton.onClick.AddListener(OnSoloButtonClicked);
        }

        if (onlineButton != null)
        {
            onlineButton.onClick.RemoveListener(OnOnlineButtonClicked);
            onlineButton.onClick.AddListener(OnOnlineButtonClicked);
        }

        if (customButton != null)
        {
            customButton.onClick.RemoveListener(OnCustomButtonClicked);
            customButton.onClick.AddListener(OnCustomButtonClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
            quitButton.onClick.AddListener(QuitGame);
        }

        if (setupBackButton != null)
        {
            setupBackButton.onClick.RemoveListener(ShowModeSelectScreen);
            setupBackButton.onClick.AddListener(ShowModeSelectScreen);
        }

        if (setupPlayButton != null)
        {
            setupPlayButton.onClick.RemoveListener(PlaySelectedMode);
            setupPlayButton.onClick.AddListener(PlaySelectedMode);
        }

        if (settingsController != null)
        {
            settingsController.SettingsLayoutChanged -= OnSettingsLayoutChanged;
            settingsController.SettingsLayoutChanged += OnSettingsLayoutChanged;

            settingsController.OnlineGameModeChanged -= OnOnlineGameModeChanged;
            settingsController.OnlineGameModeChanged += OnOnlineGameModeChanged;

            settingsController.OnlineBallCountChanged -= OnOnlineBallCountChanged;
            settingsController.OnlineBallCountChanged += OnOnlineBallCountChanged;
        }
    }

    private void UnregisterButtonListeners()
    {
        if (landingPlayButton != null)
        {
            landingPlayButton.onClick.RemoveListener(OnLandingPlayButtonClicked);
        }

        if (soloButton != null)
        {
            soloButton.onClick.RemoveListener(OnSoloButtonClicked);
        }

        if (onlineButton != null)
        {
            onlineButton.onClick.RemoveListener(OnOnlineButtonClicked);
        }

        if (customButton != null)
        {
            customButton.onClick.RemoveListener(OnCustomButtonClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
        }

        if (setupBackButton != null)
        {
            setupBackButton.onClick.RemoveListener(ShowModeSelectScreen);
        }

        if (setupPlayButton != null)
        {
            setupPlayButton.onClick.RemoveListener(PlaySelectedMode);
        }

        if (settingsController != null)
        {
            settingsController.SettingsLayoutChanged -= OnSettingsLayoutChanged;
            settingsController.OnlineGameModeChanged -= OnOnlineGameModeChanged;
            settingsController.OnlineBallCountChanged -= OnOnlineBallCountChanged;
        }
    }

    private void ShowLandingScreen()
    {
        selectedMode = MainMenuPlayMode.None;

        SetScreenActive(landingScreen, true);
        SetScreenActive(modeSelectScreen, false);
        SetScreenActive(modeSetupScreen, false);
    }

    private void ShowModeSelectScreen()
    {
        selectedMode = MainMenuPlayMode.None;

        SetScreenActive(landingScreen, false);
        SetScreenActive(modeSelectScreen, true);
        SetScreenActive(modeSetupScreen, false);
    }

    private async void OnLandingPlayButtonClicked()
    {
        if (isCheckingPreviousGame)
        {
            return;
        }

        connectionRecoveredOnPlay = false;
        CacheManagers();

        UserData currentUser = userManager?.CurrentUser;

        if (currentUser != null &&
            currentUser.HasUser &&
            !string.IsNullOrWhiteSpace(currentUser.lastGameId) &&
            currentUser.lastGameId.StartsWith(
                NetworkGameSessionManager.GameIdPrefix,
                System.StringComparison.Ordinal))
        {
            string previousGameId = currentUser.lastGameId;

            isCheckingPreviousGame = true;
            if (landingPlayButton != null)
            {
                landingPlayButton.interactable = false;
            }

            ConnectionRecoveryResult previousConnection =
                ConnectionRecoveryManager.instance != null
                    ? await ConnectionRecoveryManager.instance.RecoverAsync(true)
                    : ConnectionRecoveryResult.MultiplayerUnavailable;

            // The simulation switch can be restored just as the final attempt
            // completes. Give that newly available connection one fresh cycle.
            if (previousConnection != ConnectionRecoveryResult.Connected &&
                OnlineConnectionManager.instance?.IsConnectionAvailableForTesting == true &&
                NetworkBootstrap.instance?.IsConnectionAvailableForTesting == true &&
                ConnectionRecoveryManager.instance != null)
            {
                previousConnection = await ConnectionRecoveryManager.instance.RecoverAsync(true);
            }

            if (previousConnection != ConnectionRecoveryResult.Connected)
            {
                isCheckingPreviousGame = false;
                if (landingPlayButton != null)
                {
                    landingPlayButton.interactable = true;
                }

                // An unavailable connection does not prove the previous game ended.
                // Keep its ID so the player can try Rejoin again when service returns.
                ShowModeSelectScreen();
                return;
            }

            connectionRecoveredOnPlay = true;

            GameSessionResult rejoinCheck;

            try
            {
                rejoinCheck = GameSessionManager.instance != null
                    ? await GameSessionManager.instance.CheckNetworkRejoinAsync(previousGameId)
                    : null;

                if (rejoinCheck?.success != true &&
                    NetworkBootstrap.instance?.IsConnected == true &&
                    (rejoinCheck == null ||
                     rejoinCheck.failureType == GameSessionFailureType.ServiceUnavailable ||
                     rejoinCheck.failureType == GameSessionFailureType.NetworkConnectionFailed ||
                     rejoinCheck.failureType == GameSessionFailureType.NetworkGameConnectionUnavailable))
                {
                    await System.Threading.Tasks.Task.Delay(500);
                    rejoinCheck = GameSessionManager.instance != null
                        ? await GameSessionManager.instance.CheckNetworkRejoinAsync(previousGameId)
                        : null;
                }
            }
            finally
            {
                isCheckingPreviousGame = false;
                if (landingPlayButton != null)
                {
                    landingPlayButton.interactable = true;
                }
            }

            if (rejoinCheck?.success != true)
            {
                if (NetworkBootstrap.instance?.IsConnected != true)
                {
                    ShowModeSelectScreen();
                    return;
                }

                if (rejoinCheck?.failureType == GameSessionFailureType.GameNotFound ||
                    rejoinCheck?.failureType == GameSessionFailureType.PlayerNotFound ||
                    rejoinCheck?.failureType == GameSessionFailureType.PlayerNotEligible)
                {
                    GameSessionData previousSession = GameSessionManager.instance?.CurrentGameSession;
                    bool cancelledBeforeStart = previousSession != null &&
                        string.Equals(previousSession.gameId, previousGameId, System.StringComparison.Ordinal) &&
                        previousSession.gameState == GameSessionState.Created;

                    if (GameSessionManager.instance != null)
                    {
                        await GameSessionManager.instance.ClearPreviousSessionForFreshLobbyEntryAsync();
                    }

                    userManager.ClearLastGameId();

                    ShowModeSelectScreen();
                    if (!cancelledBeforeStart)
                    {
                        popupManager?.OpenFailurePopup("This game has ended and can no longer be rejoined.");
                    }
                    return;
                }

                popupManager?.OpenFailurePopup("Unable to check the previous game. Please try again.");
                return;
            }

            if (popupManager == null)
            {
                Debug.LogWarning("MainMenuController could not open Game Rejoin because PopupManager was not found.");
                return;
            }

            popupManager.OpenGameRejoinPopup();
            return;
        }

        if (OnlineConnectionManager.instance?.IsOnline == false &&
            ConnectionRecoveryManager.instance != null)
        {
            isCheckingPreviousGame = true;

            if (landingPlayButton != null)
            {
                landingPlayButton.interactable = false;
            }

            try
            {
                connectionRecoveredOnPlay =
                    await ConnectionRecoveryManager.instance.RecoverAsync(false) ==
                    ConnectionRecoveryResult.Connected;
            }
            finally
            {
                isCheckingPreviousGame = false;

                if (landingPlayButton != null)
                {
                    landingPlayButton.interactable = true;
                }
            }
        }

        ShowModeSelectScreen();
    }

    private void SetScreenActive(GameObject screen, bool isActive)
    {
        if (screen != null)
        {
            screen.SetActive(isActive);
        }
    }

    private void OnSoloButtonClicked()
    {
        CacheManagers();

        if (userManager != null && userManager.HasUser)
        {
            OpenSoloSetupOrSavedGamePrompt();
            return;
        }

        if (popupManager == null)
        {
            Debug.LogWarning("MainMenuController could not open Create User because PopupManager was not found.");
            return;
        }

        popupManager.OpenCreateUserPopup(OpenSoloSetupOrSavedGamePrompt);
    }

    private void OpenSoloSetupOrSavedGamePrompt()
    {
        CacheManagers();

        if (GameSessionManager.instance?.HasSavedSoloGameForCurrentUser == true)
        {
            if (popupManager == null)
            {
                Debug.LogWarning("MainMenuController could not open the saved Solo rejoin popup because PopupManager was not found.");
                return;
            }

            popupManager.OpenSavedSoloGameRejoinPopup(
                () => OpenModeSetup(MainMenuPlayMode.Solo));
            return;
        }

        OpenModeSetup(MainMenuPlayMode.Solo);
    }

    private void OnOnlineButtonClicked()
    {
        TryOpenModeSetup(MainMenuPlayMode.Online);
    }

    private void OnCustomButtonClicked()
    {
        TryOpenModeSetup(MainMenuPlayMode.Custom);
    }

    private void TryOpenModeSetup(MainMenuPlayMode mode)
    {
        CacheManagers();

        if (mode == MainMenuPlayMode.None)
        {
            return;
        }

        if (userManager != null && userManager.HasUser)
        {
            OpenModeSetup(mode);
            return;
        }

        if (popupManager == null)
        {
            Debug.LogWarning("MainMenuController could not open Create User because PopupManager was not found.");
            return;
        }

        popupManager.OpenCreateUserPopup(() => OpenModeSetup(mode));
    }

    private void OpenModeSetup(MainMenuPlayMode mode)
    {
        selectedMode = mode;

        SetScreenActive(landingScreen, false);
        SetScreenActive(modeSelectScreen, false);
        SetScreenActive(modeSetupScreen, true);

        if (modeTitleText != null)
        {
            modeTitleText.text = GetModeTitle(mode);
        }

        if (settingsController != null)
        {
            settingsController.ShowModeSettings(mode);
            UpdateGameInfoForMode(mode);
        }
        else
        {
            Debug.LogWarning("MainMenuController could not show mode settings because MainMenuSettingsController was not assigned.");
        }

        ResetSettingsScroll();
    }

    private string GetModeTitle(MainMenuPlayMode mode)
    {
        switch (mode)
        {
            case MainMenuPlayMode.Solo:
                return "SOLO";

            case MainMenuPlayMode.Online:
                return "ONLINE";

            case MainMenuPlayMode.Custom:
                return "CUSTOM";

            default:
                return string.Empty;
        }
    }

    private void UpdateGameInfoForMode(MainMenuPlayMode mode)
    {
        if (gameInfoController == null)
        {
            Debug.LogWarning("MainMenuController could not update game info because MainMenuGameInfoController was not assigned.");
            return;
        }

        switch (mode)
        {
            case MainMenuPlayMode.Solo:
                gameInfoController.ShowSoloInfo();
                break;

            case MainMenuPlayMode.Online:
                gameInfoController.ShowOnlineInfo(GetSelectedOnlineGameModeType(), GetSelectedOnlineBallCountType());
                break;

            case MainMenuPlayMode.Custom:
                gameInfoController.ShowCustomInfo();
                break;

            default:
                gameInfoController.ClearInfo();
                break;
        }
    }

    private BingoBallCountType GetSelectedOnlineBallCountType()
    {
        if (settingsController == null)
        {
            return BingoBallCountType.Ball75;
        }

        return settingsController.GetSelectedOnlineBallCountType();
    }

    private BingoGameModeType GetSelectedOnlineGameModeType()
    {
        if (settingsController == null)
        {
            return BingoGameModeType.Traditional;
        }

        return settingsController.GetSelectedOnlineGameModeType();
    }

    private void OnOnlineGameModeChanged(BingoGameModeType selectedGameModeType)
    {
        if (selectedMode != MainMenuPlayMode.Online || gameInfoController == null)
        {
            return;
        }

        gameInfoController.ShowOnlineInfo(selectedGameModeType, GetSelectedOnlineBallCountType());
    }

    private void OnOnlineBallCountChanged(BingoBallCountType selectedBallCountType)
    {
        if (selectedMode != MainMenuPlayMode.Online || gameInfoController == null)
        {
            return;
        }

        gameInfoController.ShowOnlineInfo(GetSelectedOnlineGameModeType(), selectedBallCountType);
    }

    private void ResetSettingsScroll()
    {
        ResizeSettingsContentToActiveGroup();

        if (settingsScrollRect != null)
        {
            settingsScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void OnSettingsLayoutChanged()
    {
        ResetSettingsScroll();
    }

    private void ResizeSettingsContentToActiveGroup()
    {
        if (settingsScrollRect == null || settingsScrollRect.content == null)
        {
            return;
        }

        RectTransform contentRect = settingsScrollRect.content;
        RectTransform activeGroupRect = GetActiveSettingsGroupRect(contentRect);

        if (activeGroupRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        LayoutRebuilder.ForceRebuildLayoutImmediate(activeGroupRect);

        float preferredHeight = GetActiveGroupContentHeight(activeGroupRect);

        if (preferredHeight < 0f)
        {
            preferredHeight = 0f;
        }

        Vector2 contentSize = contentRect.sizeDelta;
        contentSize.y = preferredHeight;
        contentRect.sizeDelta = contentSize;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }

    private float GetActiveGroupContentHeight(RectTransform activeGroupRect)
    {
        if (activeGroupRect == null)
        {
            return 0f;
        }

        VerticalLayoutGroup verticalLayoutGroup = activeGroupRect.GetComponent<VerticalLayoutGroup>();

        if (verticalLayoutGroup == null)
        {
            float preferredHeight = LayoutUtility.GetPreferredHeight(activeGroupRect);

            if (preferredHeight <= 0f)
            {
                preferredHeight = activeGroupRect.rect.height;
            }

            return preferredHeight;
        }

        float height = verticalLayoutGroup.padding.top + verticalLayoutGroup.padding.bottom;
        int activeLayoutChildCount = 0;

        for (int i = 0; i < activeGroupRect.childCount; i++)
        {
            RectTransform childRect = activeGroupRect.GetChild(i) as RectTransform;

            if (childRect == null)
            {
                continue;
            }

            if (!childRect.gameObject.activeSelf)
            {
                continue;
            }

            LayoutElement layoutElement = childRect.GetComponent<LayoutElement>();

            if (layoutElement != null && layoutElement.ignoreLayout)
            {
                continue;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(childRect);

            float childHeight = LayoutUtility.GetPreferredHeight(childRect);

            if (childHeight <= 0f)
            {
                childHeight = childRect.rect.height;
            }

            height += childHeight;
            activeLayoutChildCount++;
        }

        if (activeLayoutChildCount > 1)
        {
            height += verticalLayoutGroup.spacing * (activeLayoutChildCount - 1);
        }

        return height;
    }

    private RectTransform GetActiveSettingsGroupRect(RectTransform contentRect)
    {
        if (contentRect == null)
        {
            return null;
        }

        for (int i = 0; i < contentRect.childCount; i++)
        {
            RectTransform childRect = contentRect.GetChild(i) as RectTransform;

            if (childRect == null)
            {
                continue;
            }

            if (childRect.gameObject.activeSelf)
            {
                return childRect;
            }
        }

        return null;
    }

    private async void PlaySelectedMode()
    {
        CacheManagers();

        if (isStartingSelectedMode)
        {
            return;
        }

        if (selectedMode == MainMenuPlayMode.None)
        {
            Debug.LogWarning(
                "Cannot play because no mode is selected.");

            return;
        }

        if (settingsController == null)
        {
            Debug.LogWarning(
                "MainMenuController could not play because MainMenuSettingsController was not found.");

            return;
        }

        if (userManager == null ||
            !userManager.IsReady ||
            !userManager.HasUser)
        {
            Debug.LogWarning(
                "MainMenuController could not play because the current user is not ready.");

            return;
        }

        if (!settingsController.TryBuildLobbySetupData(
                selectedMode,
                out LobbySetupData lobbySetupData))
        {
            ScrollSettingsToBottom();
            return;
        }

        lobbySetupData.userData = userManager.CurrentUser;

        if (!settingsController.SaveMenuDataForMode(
                selectedMode))
        {
            return;
        }

        if (lobbyManager == null)
        {
            lobbyManager =
                LobbyManager.instance;
        }

        if (lobbyManager == null)
        {
            Debug.LogWarning(
                "MainMenuController could not send lobby setup data because LobbyManager was not found.");

            return;
        }

        if (gameSceneManager == null)
        {
            gameSceneManager =
                GameSceneManager.instance;
        }

        if (gameSceneManager == null)
        {
            Debug.LogWarning(
                "MainMenuController could not load Lobby because GameSceneManager was not found.");

            return;
        }

        isStartingSelectedMode = true;

        try
        {
            if (selectedMode != MainMenuPlayMode.Solo)
            {
                ConnectionRecoveryResult connectionResult =
                    ConnectionRecoveryManager.instance != null
                        ? connectionRecoveredOnPlay &&
                          OnlineConnectionManager.instance?.IsOnline == true &&
                          NetworkBootstrap.instance?.IsConnectionAvailableForTesting == true
                            ? await ConnectionRecoveryManager.instance.PrepareLobbyAfterPlayRecoveryAsync(lobbySetupData)
                            : await ConnectionRecoveryManager.instance.RecoverAsync(true, lobbySetupData)
                        : ConnectionRecoveryResult.MultiplayerUnavailable;

                if (connectionResult != ConnectionRecoveryResult.Connected ||
                    OnlineConnectionManager.instance?.IsOnline != true ||
                    NetworkBootstrap.instance?.IsConnected != true ||
                    NetworkLobbyConnection.GetLocalConnection() == null)
                {
                    popupManager?.OpenFailurePopup(
                        connectionResult == ConnectionRecoveryResult.OnlineUnavailable ||
                        OnlineConnectionManager.instance?.IsOnline != true
                            ? "Online services are unavailable. Check your connection and try again."
                            : "The lobby and game connection is unavailable. Please try again.");
                    return;
                }
            }

            ConnectionRecoveryManager.instance?.SetExpectingNetworkLobbyLoading(
                selectedMode != MainMenuPlayMode.Solo);
            gameSceneManager.LoadLobbyScene();

            if (GameSessionManager.instance?.HasSavedSoloGameForCurrentUser == true)
            {
                await GameSessionManager.instance.DeclineSavedSoloGameAsync();
            }

            if (GameSessionManager.instance != null)
            {
                await GameSessionManager.instance.ClearPreviousSessionForFreshLobbyEntryAsync();
            }
            else
            {
                await lobbyManager.ClearPreviousLobbyMembershipAsync(userManager.CurrentUser);
                userManager.ClearLastGameId();
            }

            if (gameSceneManager.CurrentSceneType != GameSceneType.Lobby ||
                (selectedMode != MainMenuPlayMode.Solo &&
                 (OnlineConnectionManager.instance?.IsOnline != true ||
                  NetworkBootstrap.instance?.IsConnected != true)))
            {
                if (selectedMode != MainMenuPlayMode.Solo &&
                    gameSceneManager.CurrentSceneType == GameSceneType.Lobby)
                {
                    bool onlineLost = OnlineConnectionManager.instance?.IsOnline != true;
                    FailureManager.instance?.ReportFailure(
                        onlineLost
                            ? "The connection to online services was lost while loading the lobby."
                            : "The lobby and game connection was lost while loading.",
                        onlineLost ? FailurePrecedence.Online : FailurePrecedence.SessionConnection,
                        FailureDisplayMode.WaitForMain);
                    gameSceneManager.ReturnToMainSceneAfterFailure();
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(userManager.CurrentUser.pendingNetworkGameCleanupId))
            {
                FailureManager.instance?.ShowFailureOnMain(
                    "The previous game could not be cleared. Please try again.",
                    FailurePrecedence.SessionConnection);
                gameSceneManager.ReturnToMainSceneAfterFailure();
                return;
            }

            lobbySetupData.startFreshEntry = true;
            lobbyManager.SetPendingLobbySetupData(
                lobbySetupData);

            lobbyManager.BeginPendingLobbyEntry();
            ConnectionRecoveryManager.instance?.SetExpectingNetworkLobbyLoading(false);
        }
        finally
        {
            ConnectionRecoveryManager.instance?.SetExpectingNetworkLobbyLoading(false);
            isStartingSelectedMode = false;
        }
    }

    private void ScrollSettingsToBottom()
    {
        ResizeSettingsContentToActiveGroup();

        if (settingsScrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        settingsScrollRect.verticalNormalizedPosition = 0f;
    }

    private void QuitGame()
    {
        CacheManagers();

        if (gameManager == null)
        {
            Debug.LogWarning("MainMenuController could not quit because GameManager was not found.");
            return;
        }

        gameManager.QuitGame();
    }
}
