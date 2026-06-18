using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum MainMenuPlayMode
{
    None,
    Solo,
    Online,
    Custom
}

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
    [SerializeField] private TMP_Text modeTitleText;
    [SerializeField] private ScrollRect settingsScrollRect;
    [SerializeField] private GameObject soloSettingsGroup;
    [SerializeField] private GameObject onlineSettingsGroup;
    [SerializeField] private GameObject customSettingsGroup;
    [SerializeField] private Button setupBackButton;
    [SerializeField] private Button setupPlayButton;

    private MainMenuPlayMode selectedMode = MainMenuPlayMode.None;

    private UserManager userManager;
    private PopupManager popupManager;
    private GameSceneManager gameSceneManager;
    private GameManager gameManager;

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

        ShowSettingsGroup(mode);
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

    private void ShowSettingsGroup(MainMenuPlayMode mode)
    {
        SetScreenActive(soloSettingsGroup, mode == MainMenuPlayMode.Solo);
        SetScreenActive(onlineSettingsGroup, mode == MainMenuPlayMode.Online);
        SetScreenActive(customSettingsGroup, mode == MainMenuPlayMode.Custom);
    }

    private void ResetSettingsScroll()
    {
        if (settingsScrollRect != null)
        {
            settingsScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void PlaySelectedMode()
    {
        CacheManagers();

        if (selectedMode == MainMenuPlayMode.None)
        {
            Debug.LogWarning("Cannot play because no mode is selected.");
            return;
        }

        if (gameSceneManager == null)
        {
            Debug.LogWarning("MainMenuController could not load Lobby because GameSceneManager was not found.");
            return;
        }

        gameSceneManager.LoadLobbyScene();
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