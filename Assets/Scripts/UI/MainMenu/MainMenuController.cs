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
    [SerializeField] private TMP_Text modeTitleText;
    [SerializeField] private ScrollRect settingsScrollRect;
    [SerializeField] private Button setupBackButton;
    [SerializeField] private Button setupPlayButton;

    private MainMenuPlayMode selectedMode = MainMenuPlayMode.None;

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
    }

    private void RegisterButtonListeners()
    {
        if (landingPlayButton != null)
        {
            landingPlayButton.onClick.RemoveListener(ShowModeSelectScreen);
            landingPlayButton.onClick.AddListener(ShowModeSelectScreen);
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
        }
    }

    private void UnregisterButtonListeners()
    {
        if (landingPlayButton != null)
        {
            landingPlayButton.onClick.RemoveListener(ShowModeSelectScreen);
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

    private void SetScreenActive(GameObject screen, bool isActive)
    {
        if (screen != null)
        {
            screen.SetActive(isActive);
        }
    }

    private void OnSoloButtonClicked()
    {
        TryOpenModeSetup(MainMenuPlayMode.Solo);
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

    private void PlaySelectedMode()
    {
        CacheManagers();

        if (selectedMode == MainMenuPlayMode.None)
        {
            Debug.LogWarning("Cannot play because no mode is selected.");
            return;
        }

        if (settingsController == null)
        {
            Debug.LogWarning("MainMenuController could not play because MainMenuSettingsController was not found.");
            return;
        }

        if (!settingsController.TryBuildLobbySetupData(selectedMode, out LobbySetupData lobbySetupData))
        {
            ScrollSettingsToBottom();
            return;
        }

        if (!settingsController.SaveMenuDataForMode(selectedMode))
        {
            return;
        }

        if (lobbyManager == null)
        {
            lobbyManager = LobbyManager.instance;
        }

        if (lobbyManager == null)
        {
            Debug.LogWarning("MainMenuController could not send lobby setup data because LobbyManager was not found.");
            return;
        }

        lobbyManager.SetPendingLobbySetupData(lobbySetupData);

        if (gameSceneManager == null)
        {
            Debug.LogWarning("MainMenuController could not load Lobby because GameSceneManager was not found.");
            return;
        }

        gameSceneManager.LoadLobbyScene();
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