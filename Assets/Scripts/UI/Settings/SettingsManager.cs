using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SettingsManager : MonoBehaviour, ISaveManager, ISceneReadyCheck
{
    public static SettingsManager instance;

    #region Inspector Fields

    [Header("Controllers")]
    [SerializeField] private SettingsSoundController soundController;
    [SerializeField] private SettingsGraphicsController graphicsController;
    [SerializeField] private SettingsThemeController themeController;

    private PopupManager popupManager;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string masterVolumeParameter = "Master";
    [SerializeField] private string musicVolumeParameter = "Music";
    [SerializeField] private string soundVolumeParameter = "SoundEffects";

    #endregion

    #region Private Types

    private struct ResolutionOption
    {
        public int width;
        public int height;

        public ResolutionOption(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

        public string Label => $"{width} x {height}";
    }

    #endregion

    #region Private Fields

    private UIThemeManager themeManager;

    private SettingsData settingsData;

    private readonly List<ResolutionOption> resolutionOptions = new();
    private readonly List<FullScreenMode> screenModeOptions = new();

    private bool hasLoadedData;
    private bool isSavingQueued;
    private bool hasUnsavedSettingsChanges;

    public bool IsReady => hasLoadedData;

    private Coroutine deferredSaveRoutine;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;

        BuildScreenModeOptions();
        RebuildResolutionOptions();
    }

    private void OnDestroy()
    {
        UnregisterReadyCheck();

        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnEnable()
    {
        HookControllerEvents();
        HookPopupEvents();
    }

    private void Start()
    {
        if (PopupManager.instance != null)
        {
            popupManager = PopupManager.instance;
        }

        CacheThemeManager();

        RegisterReadyCheck();
        TryLoadFromCurrentSaveData();
    }

    private bool CacheThemeManager()
    {
        if (themeManager == null)
        {
            themeManager = UIThemeManager.instance;
        }

        return themeManager != null;
    }

    private void OnDisable()
    {
        if (deferredSaveRoutine != null)
        {
            StopCoroutine(deferredSaveRoutine);
            deferredSaveRoutine = null;
        }

        SavePendingSettings();

        UnhookControllerEvents();
        UnhookPopupEvents();
    }

    #endregion

    #region ISaveManager

    public void LoadData(GameData data)
    {
        if (data == null)
        {
            return;
        }

        if (data.settingsData == null)
        {
            data.settingsData = new SettingsData();
        }

        settingsData = data.settingsData;

        RebuildResolutionOptions();

        bool changed = false;

        if (ApplyDefaultResolutionIfMissing())
        {
            changed = true;
        }

        if (ValidateGraphicsSettings())
        {
            changed = true;
        }

        ClampSoundSettings();

        if (ValidateLeaderboardSettings())
        {
            changed = true;
        }

        ApplySoundSettings();
        ApplyGraphicsSettings();

        if (ApplyThemeSettings())
        {
            changed = true;
        }

        RefreshControllers();

        hasLoadedData = true;

        if (changed)
        {
            MarkSettingsDirty();
        }
    }

    public void SaveData(ref GameData data)
    {
        if (data == null)
        {
            data = new GameData();
        }

        if (settingsData == null)
        {
            settingsData = new SettingsData();
        }

        data.settingsData = settingsData;
    }

    bool ISaveManager.IsReady()
    {
        return hasLoadedData;
    }

    string ISceneReadyCheck.ReadyName => "Settings Manager";

    bool ISceneReadyCheck.IsReady => hasLoadedData;

    #endregion

    #region Leaderboard Settings

    public ScorePlayMode GetSavedLeaderboardScorePlayMode()
    {
        EnsureSettingsData();
        return settingsData.leaderboardScorePlayMode;
    }

    public LeaderboardModeFilter GetSavedLeaderboardGameModeFilter()
    {
        EnsureSettingsData();
        return settingsData.leaderboardGameModeFilter;
    }

    public LeaderboardPageSizeType GetSavedLeaderboardPageSize()
    {
        EnsureSettingsData();
        return settingsData.leaderboardPageSize;
    }

    public void SetLeaderboardFilterSettings(
        ScorePlayMode scorePlayMode,
        LeaderboardModeFilter gameModeFilter,
        LeaderboardPageSizeType pageSize)
    {
        EnsureSettingsData();

        if (!Enum.IsDefined(typeof(ScorePlayMode), scorePlayMode))
        {
            scorePlayMode = ScorePlayMode.Solo;
        }

        if (!IsValidLeaderboardGameModeFilter(gameModeFilter))
        {
            gameModeFilter = LeaderboardModeFilter.CreateOverall();
        }

        if (!Enum.IsDefined(typeof(LeaderboardPageSizeType), pageSize))
        {
            pageSize = LeaderboardPageSizeType.Show10;
        }

        if (settingsData.leaderboardScorePlayMode == scorePlayMode &&
            AreSameLeaderboardFilters(settingsData.leaderboardGameModeFilter, gameModeFilter) &&
            settingsData.leaderboardPageSize == pageSize)
        {
            return;
        }

        settingsData.leaderboardScorePlayMode = scorePlayMode;
        settingsData.leaderboardGameModeFilter = gameModeFilter;
        settingsData.leaderboardPageSize = pageSize;

        MarkSettingsDirty();
        SavePendingSettingsDeferred();
    }

    #endregion

    #region Controller Events

    private void HookControllerEvents()
    {
        if (soundController != null)
        {
            soundController.MasterVolumeChanged += OnMasterVolumeChanged;
            soundController.MusicVolumeChanged += OnMusicVolumeChanged;
            soundController.SoundVolumeChanged += OnSoundVolumeChanged;
        }

        if (graphicsController != null)
        {
            graphicsController.ResolutionIndexChanged += OnResolutionIndexChanged;
            graphicsController.ScreenModeIndexChanged += OnScreenModeIndexChanged;
        }

        if (themeController != null)
        {
            themeController.ThemeSelected += OnThemeSelected;
        }
    }

    private void UnhookControllerEvents()
    {
        if (soundController != null)
        {
            soundController.MasterVolumeChanged -= OnMasterVolumeChanged;
            soundController.MusicVolumeChanged -= OnMusicVolumeChanged;
            soundController.SoundVolumeChanged -= OnSoundVolumeChanged;
        }

        if (graphicsController != null)
        {
            graphicsController.ResolutionIndexChanged -= OnResolutionIndexChanged;
            graphicsController.ScreenModeIndexChanged -= OnScreenModeIndexChanged;
        }

        if (themeController != null)
        {
            themeController.ThemeSelected -= OnThemeSelected;
        }
    }

    #endregion

    #region Sound Events

    private void OnMasterVolumeChanged(float value)
    {
        EnsureSettingsData();

        settingsData.masterVolume = Mathf.Clamp01(value);

        ApplyMasterVolume();
        MarkSettingsDirty();
    }

    private void OnMusicVolumeChanged(float value)
    {
        EnsureSettingsData();

        settingsData.musicVolume = Mathf.Clamp01(value);

        ApplyMusicVolume();
        MarkSettingsDirty();
    }

    private void OnSoundVolumeChanged(float value)
    {
        EnsureSettingsData();

        settingsData.soundVolume = Mathf.Clamp01(value);

        ApplySoundVolume();
        MarkSettingsDirty();
    }

    #endregion

    #region Graphics Events

    private void OnResolutionIndexChanged(int selectedIndex)
    {
        EnsureSettingsData();

        if (selectedIndex < 0 || selectedIndex >= resolutionOptions.Count)
        {
            return;
        }

        ResolutionOption selectedResolution = resolutionOptions[selectedIndex];

        settingsData.resolutionWidth = selectedResolution.width;
        settingsData.resolutionHeight = selectedResolution.height;

        ApplyGraphicsSettings();
        MarkSettingsDirty();
    }

    private void OnScreenModeIndexChanged(int selectedIndex)
    {
        EnsureSettingsData();

        if (selectedIndex < 0 || selectedIndex >= screenModeOptions.Count)
        {
            return;
        }

        settingsData.screenMode = screenModeOptions[selectedIndex];

        ApplyGraphicsSettings();
        MarkSettingsDirty();
    }

    #endregion

    #region Theme Events

    private void OnThemeSelected(UIThemeType selectedThemeType)
    {
        EnsureSettingsData();

        if (!CacheThemeManager())
        {
            Debug.LogWarning("SettingsManager could not apply theme because UIThemeManager.instance is missing.");
            return;
        }

        UIThemeType finalThemeType = themeManager.ValidateAndSetTheme(selectedThemeType);

        settingsData.selectedThemeType = finalThemeType;

        if (themeController != null)
        {
            themeController.UpdateSelectedTheme(finalThemeType, true);
        }

        MarkSettingsDirty();
    }

    #endregion

    #region Apply Settings

    private void ApplySoundSettings()
    {
        ApplyMasterVolume();
        ApplyMusicVolume();
        ApplySoundVolume();
    }

    private void ApplyMasterVolume()
    {
        SetMixerVolume(masterVolumeParameter, settingsData.masterVolume);
    }

    private void ApplyMusicVolume()
    {
        SetMixerVolume(musicVolumeParameter, settingsData.musicVolume);
    }

    private void ApplySoundVolume()
    {
        SetMixerVolume(soundVolumeParameter, settingsData.soundVolume);
    }

    private void SetMixerVolume(string parameterName, float normalizedValue)
    {
        if (audioMixer == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        float decibelValue = ConvertNormalizedVolumeToDecibels(normalizedValue);
        audioMixer.SetFloat(parameterName, decibelValue);
    }

    private float ConvertNormalizedVolumeToDecibels(float normalizedValue)
    {
        normalizedValue = Mathf.Clamp01(normalizedValue);

        if (normalizedValue <= 0.0001f)
        {
            return -80f;
        }

        return Mathf.Log10(normalizedValue) * 20f;
    }

    private void ApplyGraphicsSettings()
    {
        if (settingsData == null)
        {
            return;
        }

        if (settingsData.resolutionWidth <= 0 || settingsData.resolutionHeight <= 0)
        {
            return;
        }

        Screen.SetResolution(
            settingsData.resolutionWidth,
            settingsData.resolutionHeight,
            settingsData.screenMode
        );
    }

    private bool ApplyThemeSettings()
    {
        if (settingsData == null || !CacheThemeManager())
        {
            return false;
        }

        UIThemeType beforeThemeType = settingsData.selectedThemeType;
        UIThemeType finalThemeType = themeManager.ValidateAndSetTheme(beforeThemeType);

        settingsData.selectedThemeType = finalThemeType;
        return beforeThemeType != finalThemeType;
    }

    #endregion

    #region Validation

    private void ClampSoundSettings()
    {
        settingsData.masterVolume = Mathf.Clamp01(settingsData.masterVolume);
        settingsData.musicVolume = Mathf.Clamp01(settingsData.musicVolume);
        settingsData.soundVolume = Mathf.Clamp01(settingsData.soundVolume);
    }

    private bool ApplyDefaultResolutionIfMissing()
    {
        if (settingsData.resolutionWidth > 0 && settingsData.resolutionHeight > 0)
        {
            return false;
        }

        ResolutionOption defaultResolution = GetDefaultResolutionOption();

        settingsData.resolutionWidth = defaultResolution.width;
        settingsData.resolutionHeight = defaultResolution.height;

        return true;
    }

    private bool ValidateGraphicsSettings()
    {
        bool changed = false;

        if (FindResolutionIndex(settingsData.resolutionWidth, settingsData.resolutionHeight) < 0)
        {
            ResolutionOption defaultResolution = GetDefaultResolutionOption();

            settingsData.resolutionWidth = defaultResolution.width;
            settingsData.resolutionHeight = defaultResolution.height;

            changed = true;
        }

        if (!Enum.IsDefined(typeof(FullScreenMode), settingsData.screenMode))
        {
            settingsData.screenMode = FullScreenMode.FullScreenWindow;
            changed = true;
        }

        return changed;
    }

    private bool ValidateLeaderboardSettings()
    {
        bool changed = false;

        if (settingsData.settingsVersion < 2)
        {
            settingsData.settingsVersion = 2;
            changed = true;
        }

        if (!Enum.IsDefined(typeof(ScorePlayMode), settingsData.leaderboardScorePlayMode))
        {
            settingsData.leaderboardScorePlayMode = ScorePlayMode.Solo;
            changed = true;
        }

        if (!IsValidLeaderboardGameModeFilter(settingsData.leaderboardGameModeFilter))
        {
            settingsData.leaderboardGameModeFilter = LeaderboardModeFilter.CreateOverall();
            changed = true;
        }

        if (!Enum.IsDefined(typeof(LeaderboardPageSizeType), settingsData.leaderboardPageSize))
        {
            settingsData.leaderboardPageSize = LeaderboardPageSizeType.Show10;
            changed = true;
        }

        return changed;
    }

    private static bool IsValidLeaderboardGameModeFilter(LeaderboardModeFilter gameModeFilter)
    {
        if (!Enum.IsDefined(typeof(LeaderboardModeFilterType), gameModeFilter.filterType))
        {
            return false;
        }

        return gameModeFilter.IsOverall ||
               (gameModeFilter.IsGameMode && UserStats.IsScoredGameMode(gameModeFilter.gameModeType));
    }

    private static bool AreSameLeaderboardFilters(
        LeaderboardModeFilter firstFilter,
        LeaderboardModeFilter secondFilter)
    {
        return firstFilter.filterType == secondFilter.filterType &&
               (firstFilter.IsOverall || firstFilter.gameModeType == secondFilter.gameModeType);
    }

    #endregion

    #region Controller Refresh

    public void RefreshControllers()
    {
        RefreshSoundController();
        RefreshGraphicsController();
        RefreshThemeController();
    }

    private void RefreshSoundController()
    {
        if (soundController == null || settingsData == null)
        {
            return;
        }

        soundController.SetSoundValues(
            settingsData.masterVolume,
            settingsData.musicVolume,
            settingsData.soundVolume
        );
    }

    private void RefreshGraphicsController()
    {
        if (graphicsController == null || settingsData == null)
        {
            return;
        }

        List<string> resolutionLabels = GetResolutionLabels();
        List<string> screenModeLabels = GetScreenModeLabels();

        int selectedResolutionIndex = FindResolutionIndex(
            settingsData.resolutionWidth,
            settingsData.resolutionHeight
        );

        int selectedScreenModeIndex = FindScreenModeIndex(settingsData.screenMode);

        graphicsController.SetGraphicsOptions(
            resolutionLabels,
            selectedResolutionIndex,
            screenModeLabels,
            selectedScreenModeIndex
        );
    }

    private void RefreshThemeController()
    {
        if (themeController == null || settingsData == null)
        {
            return;
        }

        if (!CacheThemeManager())
        {
            themeController.InitializeThemeOptions(null, settingsData.selectedThemeType);
            return;
        }

        themeController.InitializeThemeOptions(
            themeManager.GetThemeDataList(),
            settingsData.selectedThemeType
        );
    }

    #endregion

    #region Save

    private void MarkSettingsDirty()
    {
        if (!hasLoadedData)
        {
            return;
        }

        hasUnsavedSettingsChanges = true;
    }

    public void SavePendingSettings()
    {
        if (!hasUnsavedSettingsChanges)
        {
            return;
        }

        if (SaveSettings())
        {
            hasUnsavedSettingsChanges = false;
        }
    }

    private void SavePendingSettingsDeferred()
    {
        if (!hasUnsavedSettingsChanges)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            SavePendingSettings();
            return;
        }

        if (deferredSaveRoutine != null)
        {
            StopCoroutine(deferredSaveRoutine);
        }

        deferredSaveRoutine = StartCoroutine(SavePendingSettingsNextFrame());
    }

    private IEnumerator SavePendingSettingsNextFrame()
    {
        yield return null;

        deferredSaveRoutine = null;
        SavePendingSettings();
    }

    private bool SaveSettings()
    {
        if (!hasLoadedData)
        {
            return false;
        }

        if (SaveManager.instance == null)
        {
            return false;
        }

        SaveManager.instance.SaveGame();
        return true;
    }

    #endregion

    #region Scene Ready

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

    #region Resolution Options

    private void RebuildResolutionOptions()
    {
        resolutionOptions.Clear();

        Resolution[] unityResolutions = Screen.resolutions;

        for (int i = 0; i < unityResolutions.Length; i++)
        {
            Resolution resolution = unityResolutions[i];

            if (resolution.width <= 0 || resolution.height <= 0)
            {
                continue;
            }

            if (ContainsResolution(resolution.width, resolution.height))
            {
                continue;
            }

            resolutionOptions.Add(new ResolutionOption(resolution.width, resolution.height));
        }

        if (resolutionOptions.Count == 0)
        {
            Resolution currentResolution = Screen.currentResolution;

            if (currentResolution.width > 0 && currentResolution.height > 0)
            {
                resolutionOptions.Add(new ResolutionOption(currentResolution.width, currentResolution.height));
            }
            else
            {
                resolutionOptions.Add(new ResolutionOption(1280, 720));
            }
        }

        resolutionOptions.Sort(SortResolutionsDescending);
    }

    private bool ContainsResolution(int width, int height)
    {
        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            ResolutionOption option = resolutionOptions[i];

            if (option.width == width && option.height == height)
            {
                return true;
            }
        }

        return false;
    }

    private int SortResolutionsDescending(ResolutionOption first, ResolutionOption second)
    {
        int widthComparison = second.width.CompareTo(first.width);

        if (widthComparison != 0)
        {
            return widthComparison;
        }

        return second.height.CompareTo(first.height);
    }

    private ResolutionOption GetDefaultResolutionOption()
    {
        Resolution currentResolution = Screen.currentResolution;

        if (currentResolution.width > 0 && currentResolution.height > 0)
        {
            return new ResolutionOption(currentResolution.width, currentResolution.height);
        }

        if (resolutionOptions.Count > 0)
        {
            return resolutionOptions[0];
        }

        return new ResolutionOption(1280, 720);
    }

    private int FindResolutionIndex(int width, int height)
    {
        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            ResolutionOption option = resolutionOptions[i];

            if (option.width == width && option.height == height)
            {
                return i;
            }
        }

        return -1;
    }

    private List<string> GetResolutionLabels()
    {
        List<string> labels = new();

        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            labels.Add(resolutionOptions[i].Label);
        }

        return labels;
    }

    #endregion

    #region Screen Mode Options

    private void BuildScreenModeOptions()
    {
        screenModeOptions.Clear();

        screenModeOptions.Add(FullScreenMode.ExclusiveFullScreen);
        screenModeOptions.Add(FullScreenMode.FullScreenWindow);
        screenModeOptions.Add(FullScreenMode.MaximizedWindow);
        screenModeOptions.Add(FullScreenMode.Windowed);
    }

    private int FindScreenModeIndex(FullScreenMode screenMode)
    {
        for (int i = 0; i < screenModeOptions.Count; i++)
        {
            if (screenModeOptions[i] == screenMode)
            {
                return i;
            }
        }

        return 0;
    }

    private List<string> GetScreenModeLabels()
    {
        List<string> labels = new();

        for (int i = 0; i < screenModeOptions.Count; i++)
        {
            labels.Add(GetScreenModeLabel(screenModeOptions[i]));
        }

        return labels;
    }

    private string GetScreenModeLabel(FullScreenMode screenMode)
    {
        switch (screenMode)
        {
            case FullScreenMode.ExclusiveFullScreen:
                return "Exclusive Fullscreen";

            case FullScreenMode.FullScreenWindow:
                return "Fullscreen Window";

            case FullScreenMode.MaximizedWindow:
                return "Maximized Window";

            case FullScreenMode.Windowed:
                return "Windowed";

            default:
                return screenMode.ToString();
        }
    }

    #endregion

    #region Popup Events

    private void HookPopupEvents()
    {
        if (popupManager == null)
        {
            return;
        }

        popupManager.PopupClosed += OnPopupClosed;
    }

    private void UnhookPopupEvents()
    {
        if (popupManager == null)
        {
            return;
        }

        popupManager.PopupClosed -= OnPopupClosed;
    }

    private void OnPopupClosed(PopupId popupId)
    {
        if (popupId != PopupId.Settings)
        {
            return;
        }

        SavePendingSettingsDeferred();
    }

    #endregion

    #region Helpers

    private void EnsureSettingsData()
    {
        if (settingsData != null)
        {
            return;
        }

        settingsData = new SettingsData();
    }

    #endregion
}
