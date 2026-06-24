using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuSettingsController : MonoBehaviour, ISaveManager, ISceneReadyCheck
{
    #region Constants

    private const int MinimumLobbySize = 6;
    private const int UnlimitedPlayerCount = 100000;

    #endregion

    #region Inspector Fields

    [Header("Settings Groups")]
    [SerializeField] private GameObject soloSettingsGroup;
    [SerializeField] private GameObject onlineSettingsGroup;
    [SerializeField] private GameObject customSettingsGroup;

    [Header("Solo Settings")]
    [SerializeField] private TMP_InputField soloLobbySizeInput;
    [SerializeField] private Toggle soloUnlimitedToggle;
    [SerializeField] private TMP_Text soloErrorText;

    #endregion

    #region Private Fields

    private MenuData menuData;

    private bool hasLoadedData;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        EnsureMenuData();
        ApplyAllDefaults();
        ClearAllErrors();
    }

    private void Start()
    {
        RegisterReadyCheck();
        TryLoadFromCurrentSaveData();
    }

    private void OnEnable()
    {
        RegisterAllListeners();
    }

    private void OnDisable()
    {
        UnregisterAllListeners();
    }

    private void OnDestroy()
    {
        UnregisterReadyCheck();
    }

    #endregion

    #region ISaveManager

    public void LoadData(GameData data)
    {
        if (data == null)
        {
            EnsureMenuData();
            ApplyAllDefaults();
            hasLoadedData = true;
            return;
        }

        if (data.menuData == null)
        {
            data.menuData = new MenuData();
        }

        menuData = data.menuData;

        EnsureMenuData();
        LoadAllMenuData();

        hasLoadedData = true;
    }

    public void SaveData(ref GameData data)
    {
        if (data == null)
        {
            data = new GameData();
        }

        EnsureMenuData();
        SaveAllMenuData();

        data.menuData = menuData;
    }

    bool ISaveManager.IsReady()
    {
        return hasLoadedData;
    }

    string ISceneReadyCheck.ReadyName => "Main Menu Settings Controller";

    bool ISceneReadyCheck.IsReady => hasLoadedData;

    #endregion

    #region Public Methods

    public void ShowModeSettings(MainMenuPlayMode playMode)
    {
        SetActive(soloSettingsGroup, playMode == MainMenuPlayMode.Solo);
        SetActive(onlineSettingsGroup, playMode == MainMenuPlayMode.Online);
        SetActive(customSettingsGroup, playMode == MainMenuPlayMode.Custom);

        ClearAllErrors();
    }

    public bool TryBuildLobbySetupData(MainMenuPlayMode playMode, out LobbySetupData lobbySetupData)
    {
        lobbySetupData = new LobbySetupData();
        lobbySetupData.playMode = playMode;

        switch (playMode)
        {
            case MainMenuPlayMode.Solo:
                return TryBuildSoloLobbySetupData(lobbySetupData);

            default:
                Debug.LogWarning($"MainMenuSettingsController does not have validation setup for {playMode} yet.");
                return false;
        }
    }

    public bool SaveMenuData()
    {
        EnsureMenuData();
        SaveAllMenuData();

        if (SaveManager.instance == null)
        {
            Debug.LogWarning("MainMenuSettingsController could not save because SaveManager was not found.");
            return false;
        }

        SaveManager.instance.SaveGame();
        return true;
    }

    public bool SaveMenuDataForMode(MainMenuPlayMode playMode)
    {
        return SaveMenuData();
    }

    public void ClearAllErrors()
    {
        ClearError(soloErrorText);
    }

    #endregion

    #region Menu Data Setup

    private void EnsureMenuData()
    {
        if (menuData == null)
        {
            menuData = new MenuData();
        }

        if (menuData.soloMenuData == null)
        {
            menuData.soloMenuData = new SoloMenuData();
        }
    }

    private void ApplyAllDefaults()
    {
        EnsureMenuData();

        ApplySoloDefaults(menuData.soloMenuData);

    }

    private void LoadAllMenuData()
    {
        EnsureMenuData();

        LoadSoloMenuData(menuData.soloMenuData);

        ClearAllErrors();
    }

    private void SaveAllMenuData()
    {
        EnsureMenuData();

        SaveSoloMenuData(menuData.soloMenuData);
    }

    #endregion

    #region Listener Setup

    private void RegisterAllListeners()
    {
        RegisterSoloListeners();
    }

    private void UnregisterAllListeners()
    {
        UnregisterSoloListeners();

    }

    #endregion

    #region Solo Events

    private void RegisterSoloListeners()
    {
        if (soloLobbySizeInput != null)
        {
            soloLobbySizeInput.onValueChanged.RemoveListener(OnSoloLobbySizeChanged);
            soloLobbySizeInput.onValueChanged.AddListener(OnSoloLobbySizeChanged);
        }

        if (soloUnlimitedToggle != null)
        {
            soloUnlimitedToggle.onValueChanged.RemoveListener(OnSoloUnlimitedChanged);
            soloUnlimitedToggle.onValueChanged.AddListener(OnSoloUnlimitedChanged);
        }
    }

    private void UnregisterSoloListeners()
    {
        if (soloLobbySizeInput != null)
        {
            soloLobbySizeInput.onValueChanged.RemoveListener(OnSoloLobbySizeChanged);
        }

        if (soloUnlimitedToggle != null)
        {
            soloUnlimitedToggle.onValueChanged.RemoveListener(OnSoloUnlimitedChanged);
        }
    }

    private void OnSoloLobbySizeChanged(string value)
    {
        if (!HasError(soloErrorText))
        {
            return;
        }

        if (TryGetLobbySizeValue(soloLobbySizeInput, false, soloErrorText, out _))
        {
            ClearError(soloErrorText);
        }
    }

    private void OnSoloUnlimitedChanged(bool isOn)
    {
        ApplySoloUnlimitedState();

        if (isOn)
        {
            ClampLobbySizeInputToAllowedRange(soloLobbySizeInput);
            ClearError(soloErrorText);
            return;
        }

        if (HasError(soloErrorText) && TryGetLobbySizeValue(soloLobbySizeInput, false, soloErrorText, out _))
        {
            ClearError(soloErrorText);
        }
    }

    #endregion

    #region Solo Defaults Load Save

    private void ApplySoloDefaults(SoloMenuData soloMenuData)
    {
        if (soloMenuData == null)
        {
            soloMenuData = new SoloMenuData();
        }

        int lobbySize = ClampLobbySizeToAllowedRange(soloMenuData.lobbySize);

        if (soloLobbySizeInput != null)
        {
            soloLobbySizeInput.SetTextWithoutNotify(lobbySize.ToString());
        }

        if (soloUnlimitedToggle != null)
        {
            soloUnlimitedToggle.SetIsOnWithoutNotify(soloMenuData.unlimitedPlayers);
        }

        ApplySoloUnlimitedState();
        ClearError(soloErrorText);
    }

    private void LoadSoloMenuData(SoloMenuData soloMenuData)
    {
        if (soloMenuData == null)
        {
            ApplySoloDefaults(new SoloMenuData());
            return;
        }

        int lobbySize = ClampLobbySizeToAllowedRange(soloMenuData.lobbySize);

        if (soloLobbySizeInput != null)
        {
            soloLobbySizeInput.SetTextWithoutNotify(lobbySize.ToString());
        }

        if (soloUnlimitedToggle != null)
        {
            soloUnlimitedToggle.SetIsOnWithoutNotify(soloMenuData.unlimitedPlayers);
        }

        ApplySoloUnlimitedState();
        ClearError(soloErrorText);
    }

    private void SaveSoloMenuData(SoloMenuData soloMenuData)
    {
        if (soloMenuData == null)
        {
            return;
        }

        bool unlimitedPlayers = soloUnlimitedToggle != null && soloUnlimitedToggle.isOn;

        soloMenuData.unlimitedPlayers = unlimitedPlayers;

        if (unlimitedPlayers)
        {
            soloMenuData.lobbySize = GetDefaultSoloLobbySize();
            return;
        }

        if (TryGetLobbySizeValue(soloLobbySizeInput, false, soloErrorText, out int lobbySize))
        {
            soloMenuData.lobbySize = lobbySize;
        }
    }

    #endregion

    #region Solo Lobby Data

    private bool TryBuildSoloLobbySetupData(LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null)
        {
            return false;
        }

        bool unlimitedPlayers = soloUnlimitedToggle != null && soloUnlimitedToggle.isOn;

        if (unlimitedPlayers)
        {
            lobbySetupData.soloSetupData.unlimitedPlayers = true;
            lobbySetupData.soloSetupData.maxPlayers = UnlimitedPlayerCount;

            ClearError(soloErrorText);
            return true;
        }

        if (!TryGetLobbySizeValue(soloLobbySizeInput, true, soloErrorText, out int lobbySize))
        {
            return false;
        }

        lobbySetupData.soloSetupData.unlimitedPlayers = false;
        lobbySetupData.soloSetupData.maxPlayers = lobbySize;

        ClearError(soloErrorText);
        return true;
    }

    #endregion

    #region Lobby Size Validation

    private bool TryGetLobbySizeValue(TMP_InputField inputField, bool showError, TMP_Text errorText, out int lobbySize)
    {
        lobbySize = GetSavedDefaultLobbySize();

        if (inputField == null)
        {
            if (showError)
            {
                ShowError(errorText, "Lobby size input is missing.");
            }

            return false;
        }

        string value = inputField.text.Trim();

        if (string.IsNullOrEmpty(value))
        {
            if (showError)
            {
                ShowError(errorText, $"Lobby size must be at least {MinimumLobbySize}.");
            }

            return false;
        }

        if (!int.TryParse(value, out int parsedLobbySize))
        {
            if (showError)
            {
                ShowError(errorText, "Lobby size must be a number.");
            }

            return false;
        }

        if (parsedLobbySize < MinimumLobbySize)
        {
            if (showError)
            {
                ShowError(errorText, $"Lobby size must be at least {MinimumLobbySize}.");
            }

            return false;
        }

        if (parsedLobbySize > UnlimitedPlayerCount)
        {
            parsedLobbySize = UnlimitedPlayerCount;
            inputField.SetTextWithoutNotify(UnlimitedPlayerCount.ToString());
        }

        lobbySize = parsedLobbySize;
        return true;
    }

    private int ClampLobbySizeToAllowedRange(int lobbySize)
    {
        if (lobbySize < MinimumLobbySize)
        {
            return GetSavedDefaultLobbySize();
        }

        if (lobbySize > UnlimitedPlayerCount)
        {
            return UnlimitedPlayerCount;
        }

        return lobbySize;
    }

    private void ClampLobbySizeInputToAllowedRange(TMP_InputField inputField)
    {
        if (inputField == null)
        {
            return;
        }

        if (!int.TryParse(inputField.text.Trim(), out int parsedLobbySize))
        {
            return;
        }

        int clampedLobbySize = ClampLobbySizeToAllowedRange(parsedLobbySize);

        if (clampedLobbySize != parsedLobbySize)
        {
            inputField.SetTextWithoutNotify(clampedLobbySize.ToString());
        }
    }

    private int GetSavedDefaultLobbySize()
    {
        EnsureMenuData();

        int savedLobbySize = menuData.soloMenuData.lobbySize;

        if (savedLobbySize < MinimumLobbySize)
        {
            return MinimumLobbySize;
        }

        if (savedLobbySize > UnlimitedPlayerCount)
        {
            return UnlimitedPlayerCount;
        }

        return savedLobbySize;
    }

    private int GetDefaultSoloLobbySize()
    {
        SoloMenuData defaultSoloMenuData = new SoloMenuData();
        return ClampLobbySizeToAllowedRange(defaultSoloMenuData.lobbySize);
    }

    #endregion

    #region Solo UI State

    private void ApplySoloUnlimitedState()
    {
        if (soloLobbySizeInput == null || soloUnlimitedToggle == null)
        {
            return;
        }

        soloLobbySizeInput.interactable = !soloUnlimitedToggle.isOn;
    }

    #endregion

    #region Error Helpers

    private void ShowError(TMP_Text errorText, string message)
    {
        if (errorText != null)
        {
            errorText.text = message;
        }
    }

    private void ClearError(TMP_Text errorText)
    {
        if (errorText != null)
        {
            errorText.text = string.Empty;
        }
    }

    private bool HasError(TMP_Text errorText)
    {
        return errorText != null && !string.IsNullOrWhiteSpace(errorText.text);
    }

    #endregion

    #region Save Helpers

    private void RegisterReadyCheck()
    {
        if (SceneReadyController.instance == null)
        {
            return;
        }

        SceneReadyController.instance.RegisterReadyCheck(this);
    }

    private void UnregisterReadyCheck()
    {
        if (SceneReadyController.instance == null)
        {
            return;
        }

        SceneReadyController.instance.UnregisterReadyCheck(this);
    }

    private void TryLoadFromCurrentSaveData()
    {
        if (hasLoadedData)
        {
            return;
        }

        if (SaveManager.instance == null)
        {
            return;
        }

        if (SaveManager.instance.Data == null)
        {
            return;
        }

        LoadData(SaveManager.instance.Data);
    }

    #endregion

    #region General Helpers

    private void SetActive(GameObject target, bool isActive)
    {
        if (target != null)
        {
            target.SetActive(isActive);
        }
    }

    #endregion
}