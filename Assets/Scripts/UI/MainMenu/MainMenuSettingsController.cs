using System.Collections.Generic;
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

    [Header("Online Settings")]
    [SerializeField] private TMP_Dropdown onlineGameModeDropdown;
    [SerializeField] private TMP_Dropdown onlineSearchTypeDropdown;
    [SerializeField] private GameObject onlineBallCountRow;
    [SerializeField] private TMP_Dropdown onlineBallCountDropdown;
    [SerializeField] private TMP_Text onlineErrorText;

    #endregion

    #region Private Fields

    private MenuData menuData;

    private bool hasLoadedData;

    private readonly List<BingoGameModeType> onlineGameModeOptions = new List<BingoGameModeType>();
    private readonly List<OnlineSearchType> onlineSearchTypeOptions = new List<OnlineSearchType>();
    private readonly List<BingoBallCountType> onlineBallCountOptions = new List<BingoBallCountType>();

    #endregion

    #region Unity Methods

    private void Awake()
    {
        EnsureMenuData();
        BuildAllDropdownOptions();
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

            case MainMenuPlayMode.Online:
                return TryBuildOnlineLobbySetupData(lobbySetupData);

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
        ClearError(onlineErrorText);
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

        if (menuData.onlineMenuData == null)
        {
            menuData.onlineMenuData = new OnlineMenuData();
        }
    }

    private void ApplyAllDefaults()
    {
        EnsureMenuData();

        ApplySoloDefaults(menuData.soloMenuData);
        ApplyOnlineDefaults(menuData.onlineMenuData);
    }

    private void LoadAllMenuData()
    {
        EnsureMenuData();

        LoadSoloMenuData(menuData.soloMenuData);
        LoadOnlineMenuData(menuData.onlineMenuData);

        ClearAllErrors();
    }

    private void SaveAllMenuData()
    {
        EnsureMenuData();

        SaveSoloMenuData(menuData.soloMenuData);
        SaveOnlineMenuData(menuData.onlineMenuData);
    }

    #endregion

    #region Listener Setup

    private void RegisterAllListeners()
    {
        RegisterSoloListeners();
        RegisterOnlineListeners();
    }

    private void UnregisterAllListeners()
    {
        UnregisterSoloListeners();
        UnregisterOnlineListeners();
    }

    #endregion

    #region Solo

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

    #endregion

    #region Online

    #region Online Dropdown Setup

    private void BuildAllDropdownOptions()
    {
        BuildOnlineGameModeDropdownOptions();
        BuildOnlineSearchTypeDropdownOptions();
        BuildOnlineBallCountDropdownOptions();
    }

    private void BuildOnlineGameModeDropdownOptions()
    {
        onlineGameModeOptions.Clear();

        foreach (BingoGameModeType gameModeType in System.Enum.GetValues(typeof(BingoGameModeType)))
        {
            if (gameModeType == BingoGameModeType.Custom)
            {
                continue;
            }

            onlineGameModeOptions.Add(gameModeType);
        }

        if (onlineGameModeDropdown == null)
        {
            return;
        }

        List<string> labels = new List<string>();

        for (int i = 0; i < onlineGameModeOptions.Count; i++)
        {
            labels.Add(GetGameModeLabel(onlineGameModeOptions[i]));
        }

        onlineGameModeDropdown.ClearOptions();
        onlineGameModeDropdown.AddOptions(labels);
    }

    private void BuildOnlineSearchTypeDropdownOptions()
    {
        onlineSearchTypeOptions.Clear();

        onlineSearchTypeOptions.Add(OnlineSearchType.QuickPlay);
        onlineSearchTypeOptions.Add(OnlineSearchType.CustomSearch);

        if (onlineSearchTypeDropdown == null)
        {
            return;
        }

        List<string> labels = new List<string>();

        for (int i = 0; i < onlineSearchTypeOptions.Count; i++)
        {
            labels.Add(GetOnlineSearchTypeLabel(onlineSearchTypeOptions[i]));
        }

        onlineSearchTypeDropdown.ClearOptions();
        onlineSearchTypeDropdown.AddOptions(labels);
    }

    private void BuildOnlineBallCountDropdownOptions()
    {
        onlineBallCountOptions.Clear();

        foreach (BingoBallCountType ballCountType in System.Enum.GetValues(typeof(BingoBallCountType)))
        {
            onlineBallCountOptions.Add(ballCountType);
        }

        if (onlineBallCountDropdown == null)
        {
            return;
        }

        List<string> labels = new List<string>();

        for (int i = 0; i < onlineBallCountOptions.Count; i++)
        {
            labels.Add(GetBallCountLabel(onlineBallCountOptions[i]));
        }

        onlineBallCountDropdown.ClearOptions();
        onlineBallCountDropdown.AddOptions(labels);
    }

    #endregion

    #region Online Events

    private void RegisterOnlineListeners()
    {
        if (onlineGameModeDropdown != null)
        {
            onlineGameModeDropdown.onValueChanged.RemoveListener(OnOnlineGameModeChanged);
            onlineGameModeDropdown.onValueChanged.AddListener(OnOnlineGameModeChanged);
        }

        if (onlineSearchTypeDropdown != null)
        {
            onlineSearchTypeDropdown.onValueChanged.RemoveListener(OnOnlineSearchTypeChanged);
            onlineSearchTypeDropdown.onValueChanged.AddListener(OnOnlineSearchTypeChanged);
        }

        if (onlineBallCountDropdown != null)
        {
            onlineBallCountDropdown.onValueChanged.RemoveListener(OnOnlineBallCountChanged);
            onlineBallCountDropdown.onValueChanged.AddListener(OnOnlineBallCountChanged);
        }
    }

    private void UnregisterOnlineListeners()
    {
        if (onlineGameModeDropdown != null)
        {
            onlineGameModeDropdown.onValueChanged.RemoveListener(OnOnlineGameModeChanged);
        }

        if (onlineSearchTypeDropdown != null)
        {
            onlineSearchTypeDropdown.onValueChanged.RemoveListener(OnOnlineSearchTypeChanged);
        }

        if (onlineBallCountDropdown != null)
        {
            onlineBallCountDropdown.onValueChanged.RemoveListener(OnOnlineBallCountChanged);
        }
    }

    private void OnOnlineGameModeChanged(int value)
    {
        ClearError(onlineErrorText);
    }

    private void OnOnlineSearchTypeChanged(int value)
    {
        ApplyOnlineSearchTypeState();
        ClearError(onlineErrorText);
    }

    private void OnOnlineBallCountChanged(int value)
    {
        ClearError(onlineErrorText);
    }

    #endregion

    #region Online Defaults Load Save

    private void ApplyOnlineDefaults(OnlineMenuData onlineMenuData)
    {
        if (onlineMenuData == null)
        {
            onlineMenuData = new OnlineMenuData();
        }

        SetDropdownValueWithoutNotify(onlineGameModeDropdown, onlineGameModeOptions, ValidateOnlineGameModeType(onlineMenuData.gameModeType));
        SetDropdownValueWithoutNotify(onlineSearchTypeDropdown, onlineSearchTypeOptions, onlineMenuData.searchType);
        SetDropdownValueWithoutNotify(onlineBallCountDropdown, onlineBallCountOptions, onlineMenuData.ballCountType);

        ApplyOnlineSearchTypeState();
        ClearError(onlineErrorText);
    }

    private void LoadOnlineMenuData(OnlineMenuData onlineMenuData)
    {
        if (onlineMenuData == null)
        {
            ApplyOnlineDefaults(new OnlineMenuData());
            return;
        }

        SetDropdownValueWithoutNotify(onlineGameModeDropdown, onlineGameModeOptions, ValidateOnlineGameModeType(onlineMenuData.gameModeType));
        SetDropdownValueWithoutNotify(onlineSearchTypeDropdown, onlineSearchTypeOptions, onlineMenuData.searchType);
        SetDropdownValueWithoutNotify(onlineBallCountDropdown, onlineBallCountOptions, onlineMenuData.ballCountType);

        ApplyOnlineSearchTypeState();
        ClearError(onlineErrorText);
    }

    private void SaveOnlineMenuData(OnlineMenuData onlineMenuData)
    {
        if (onlineMenuData == null)
        {
            return;
        }

        if (TryGetSelectedDropdownValue(onlineGameModeDropdown, onlineGameModeOptions, out BingoGameModeType selectedGameModeType))
        {
            onlineMenuData.gameModeType = selectedGameModeType;
        }

        if (TryGetSelectedDropdownValue(onlineSearchTypeDropdown, onlineSearchTypeOptions, out OnlineSearchType selectedSearchType))
        {
            onlineMenuData.searchType = selectedSearchType;
        }

        if (TryGetSelectedDropdownValue(onlineBallCountDropdown, onlineBallCountOptions, out BingoBallCountType selectedBallCountType))
        {
            onlineMenuData.ballCountType = selectedBallCountType;
        }
    }

    #endregion

    #region Online Lobby Data

    private bool TryBuildOnlineLobbySetupData(LobbySetupData lobbySetupData)
    {
        if (lobbySetupData == null)
        {
            return false;
        }

        if (!TryGetSelectedDropdownValue(onlineGameModeDropdown, onlineGameModeOptions, out BingoGameModeType selectedGameModeType))
        {
            ShowError(onlineErrorText, "Online game mode is not ready.");
            return false;
        }

        if (!TryGetSelectedDropdownValue(onlineSearchTypeDropdown, onlineSearchTypeOptions, out OnlineSearchType selectedSearchType))
        {
            ShowError(onlineErrorText, "Online search type is not ready.");
            return false;
        }

        if (!TryGetSelectedDropdownValue(onlineBallCountDropdown, onlineBallCountOptions, out BingoBallCountType selectedBallCountType))
        {
            ShowError(onlineErrorText, "Ball count is not ready.");
            return false;
        }

        lobbySetupData.onlineSetupData.gameModeType = selectedGameModeType;
        lobbySetupData.onlineSetupData.searchType = selectedSearchType;
        lobbySetupData.onlineSetupData.ballCountType = selectedBallCountType;

        ClearError(onlineErrorText);
        return true;
    }

    #endregion

    #region Online UI State

    private void ApplyOnlineSearchTypeState()
    {
        OnlineSearchType selectedSearchType = OnlineSearchType.QuickPlay;

        TryGetSelectedDropdownValue(onlineSearchTypeDropdown, onlineSearchTypeOptions, out selectedSearchType);

        bool showBallCount = selectedSearchType == OnlineSearchType.CustomSearch;

        SetActive(onlineBallCountRow, showBallCount);

        if (onlineBallCountDropdown != null)
        {
            onlineBallCountDropdown.interactable = showBallCount;
        }
    }

    #endregion

    #region Online Helpers

    private BingoGameModeType ValidateOnlineGameModeType(BingoGameModeType gameModeType)
    {
        if (gameModeType == BingoGameModeType.Custom)
        {
            return GetDefaultOnlineGameModeType();
        }

        if (!onlineGameModeOptions.Contains(gameModeType))
        {
            return GetDefaultOnlineGameModeType();
        }

        return gameModeType;
    }

    private BingoGameModeType GetDefaultOnlineGameModeType()
    {
        if (onlineGameModeOptions.Count > 0)
        {
            return onlineGameModeOptions[0];
        }

        return BingoGameModeType.Traditional;
    }

    private string GetGameModeLabel(BingoGameModeType gameModeType)
    {
        if (GameModeManager.instance == null)
        {
            return gameModeType.ToString();
        }

        return GameModeManager.instance.GetGameModeName(gameModeType);
    }

    private string GetOnlineSearchTypeLabel(OnlineSearchType searchType)
    {
        switch (searchType)
        {
            case OnlineSearchType.QuickPlay:
                return "Quick Play";

            case OnlineSearchType.CustomSearch:
                return "Custom Search";

            default:
                return searchType.ToString();
        }
    }

    private string GetBallCountLabel(BingoBallCountType ballCountType)
    {
        return $"{(int)ballCountType} Ball";
    }

    private void SetDropdownValueWithoutNotify<T>(TMP_Dropdown dropdown, List<T> options, T value)
    {
        if (dropdown == null || options == null || options.Count == 0)
        {
            return;
        }

        int index = options.IndexOf(value);

        if (index < 0)
        {
            index = 0;
        }

        dropdown.SetValueWithoutNotify(index);
        dropdown.RefreshShownValue();
    }

    private bool TryGetSelectedDropdownValue<T>(TMP_Dropdown dropdown, List<T> options, out T value)
    {
        value = default;

        if (dropdown == null || options == null || options.Count == 0)
        {
            return false;
        }

        int index = dropdown.value;

        if (index < 0 || index >= options.Count)
        {
            index = 0;
            dropdown.SetValueWithoutNotify(index);
            dropdown.RefreshShownValue();
        }

        value = options[index];
        return true;
    }

    #endregion

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