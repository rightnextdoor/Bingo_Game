using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LobbyHostSettingsPopupController : MonoBehaviour
{
    #region Fields

    private const string HostSettingsTitle = "Host Settings";

    [Header("Header")]
    [SerializeField] private TMP_Text titleText;

    [Header("Settings Scroll View")]
    [SerializeField] private ScrollRect settingsScrollView;

    [Header("Game Mode")]
    [SerializeField] private TMP_Dropdown gameModeDropdown;

    [Header("Ball Count")]
    [SerializeField] private TMP_Dropdown ballCountDropdown;

    [Header("Free Cell")]
    [SerializeField] private Toggle useFreeCellToggle;

    [Header("Patterns")]
    [SerializeField] private RectTransform patternListContent;
    [SerializeField] private LobbyHostPatternToggleItem patternTogglePrefab;

    [Header("Player Limit")]
    [SerializeField] private Toggle unlimitedPlayersToggle;
    [SerializeField] private GameObject playerCountRow;
    [SerializeField] private TMP_InputField playerCountInput;

    [Header("Bots")]
    [SerializeField] private Toggle addBotsToggle;
    [SerializeField] private GameObject botCountRow;
    [SerializeField] private TMP_InputField botCountInput;

    [Header("Messages")]
    [SerializeField] private TMP_Text errorText;

    [Header("Bottom Controls")]
    [SerializeField] private Button closeButton;

    private readonly List<BingoGameModeType> gameModeOptions = new List<BingoGameModeType>();
    private readonly List<BingoBallCountType> ballCountOptions = new List<BingoBallCountType>();
    private readonly List<LobbyHostPatternToggleItem> patternItems = new List<LobbyHostPatternToggleItem>();

    private LobbyHostSettingsData workingData = new LobbyHostSettingsData();
    private Coroutine initializeRoutine;

    private bool isUiReady;
    private bool isLoadingUi;

    public LobbyHostSettingsData WorkingData => workingData;
    public bool IsUiReady => isUiReady;

    public event Action SettingsLayoutChanged;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        if (titleText != null)
        {
            titleText.text = HostSettingsTitle;
        }

        ClearPatternContent();
        ConfigureInputs();
        ClearError();
    }

    private void OnEnable()
    {
        RegisterListeners();

        if (initializeRoutine != null)
        {
            StopCoroutine(initializeRoutine);
        }

        initializeRoutine = StartCoroutine(InitializeWhenReady());
    }

    private void OnDisable()
    {
        UnregisterListeners();
        UnregisterPatternItemListeners();

        if (initializeRoutine != null)
        {
            StopCoroutine(initializeRoutine);
            initializeRoutine = null;
        }

        isUiReady = false;
        isLoadingUi = false;
    }

    #endregion

    #region Initialization

    private IEnumerator InitializeWhenReady()
    {
        isUiReady = false;

        while (LobbySettings.instance == null ||
               GameModeManager.instance == null ||
               !GameModeManager.instance.IsReady ||
               LobbyManager.instance == null ||
               !LobbyManager.instance.HasEnteredLobby ||
               LobbyManager.instance.CurrentLobby == null)
        {
            yield return null;
        }

        BuildGameModeDropdownOptions();
        BuildBallCountDropdownOptions();
        BuildPatternItems();

        LobbyViewData lobbyViewData = GetCurrentLobbyViewData();

        if (lobbyViewData == null)
        {
            ShowError("The current lobby settings could not be loaded.");
            initializeRoutine = null;
            yield break;
        }

        LoadLobbyViewData(lobbyViewData);

        isUiReady = true;
        initializeRoutine = null;
    }

    private void ConfigureInputs()
    {
        if (playerCountInput != null)
        {
            playerCountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        }

        if (botCountInput != null)
        {
            botCountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        }
    }

    #endregion

    #region Lobby Data

    public void RefreshFromCurrentLobby()
    {
        LobbyViewData lobbyViewData = GetCurrentLobbyViewData();

        if (lobbyViewData == null)
        {
            ShowError("The current lobby settings could not be loaded.");
            return;
        }

        LoadLobbyViewData(lobbyViewData);
    }

    public void SetLobbyViewData(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData == null)
        {
            ShowError("The lobby settings data is missing.");
            return;
        }

        LoadLobbyViewData(lobbyViewData);
    }

    private LobbyViewData GetCurrentLobbyViewData()
    {
        LobbyManager lobbyManager = LobbyManager.instance;

        if (lobbyManager == null ||
            !lobbyManager.HasEnteredLobby ||
            lobbyManager.CurrentLobby == null)
        {
            return null;
        }

        if (lobbyManager.RuntimeType == SessionRuntimeType.Local)
        {
            return lobbyManager.CurrentLobby.Controller != null
                ? lobbyManager.CurrentLobby.Controller.BuildViewData()
                : null;
        }

        if (lobbyManager.CurrentLobbyViewData != null)
        {
            return lobbyManager.CurrentLobbyViewData;
        }

        return lobbyManager.CurrentLobby.Controller != null
            ? lobbyManager.CurrentLobby.Controller.BuildViewData()
            : null;
    }

    private void LoadLobbyViewData(LobbyViewData lobbyViewData)
    {
        if (lobbyViewData == null)
        {
            return;
        }

        isLoadingUi = true;

        workingData = new LobbyHostSettingsData(lobbyViewData);

        SetDropdownValueWithoutNotify(gameModeDropdown, gameModeOptions, workingData.gameModeType);
        SetDropdownValueWithoutNotify(ballCountDropdown, ballCountOptions, workingData.ballCountType);

        if (useFreeCellToggle != null)
        {
            useFreeCellToggle.SetIsOnWithoutNotify(workingData.useFreeCell);
        }

        LoadPatternSelection(workingData.patternTypes);

        if (unlimitedPlayersToggle != null)
        {
            unlimitedPlayersToggle.SetIsOnWithoutNotify(workingData.unlimitedPlayers);
        }

        if (playerCountInput != null)
        {
            int playerCountValue = workingData.unlimitedPlayers
                ? LobbySettings.instance.MinimumPlayers
                : Mathf.Clamp(
                    workingData.maxPlayers,
                    LobbySettings.instance.MinimumPlayers,
                    LobbySettings.instance.UnlimitedPlayerCount);

            playerCountInput.SetTextWithoutNotify(playerCountValue.ToString());
        }

        if (addBotsToggle != null)
        {
            addBotsToggle.SetIsOnWithoutNotify(workingData.addBots);
        }

        if (botCountInput != null)
        {
            int botCountValue = workingData.addBots ? Mathf.Max(1, workingData.botCount) : 1;
            botCountInput.SetTextWithoutNotify(botCountValue.ToString());
        }

        ApplyUnlimitedPlayersState();
        ApplyAddBotsState();
        RefreshPatternInteractableState();

        ClearError();
        RefreshLayout();
        ResetScroll();

        isLoadingUi = false;
    }

    #endregion

    #region Dropdown Setup

    private void BuildGameModeDropdownOptions()
    {
        gameModeOptions.Clear();

        if (gameModeDropdown == null)
        {
            return;
        }

        List<string> labels = new List<string>();

        foreach (BingoGameModeType gameModeType in Enum.GetValues(typeof(BingoGameModeType)))
        {
            if (gameModeType == BingoGameModeType.Custom)
            {
                continue;
            }

            BingoGameModeData gameModeData = GameModeManager.instance.GetGameModeData(gameModeType);

            if (gameModeData == null)
            {
                continue;
            }

            gameModeOptions.Add(gameModeType);
            labels.Add(string.IsNullOrWhiteSpace(gameModeData.GameName) ? gameModeType.ToString() : gameModeData.GameName);
        }

        gameModeDropdown.ClearOptions();
        gameModeDropdown.AddOptions(labels);
    }

    private void BuildBallCountDropdownOptions()
    {
        ballCountOptions.Clear();

        foreach (BingoBallCountType ballCountType in Enum.GetValues(typeof(BingoBallCountType)))
        {
            ballCountOptions.Add(ballCountType);
        }

        if (ballCountDropdown == null)
        {
            return;
        }

        List<string> labels = new List<string>();

        for (int i = 0; i < ballCountOptions.Count; i++)
        {
            labels.Add($"{(int)ballCountOptions[i]} Ball");
        }

        ballCountDropdown.ClearOptions();
        ballCountDropdown.AddOptions(labels);
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
            return false;
        }

        value = options[index];
        return true;
    }

    #endregion

    #region Pattern Setup

    private void BuildPatternItems()
    {
        ClearPatternContent();

        if (patternListContent == null || patternTogglePrefab == null || GameModeManager.instance == null)
        {
            return;
        }

        List<BingoPatternData> patternDataList = GameModeManager.instance.GetBingoPatternDataList();

        for (int i = 0; i < patternDataList.Count; i++)
        {
            BingoPatternData patternData = patternDataList[i];

            if (patternData == null)
            {
                continue;
            }

            LobbyHostPatternToggleItem patternItem = Instantiate(patternTogglePrefab, patternListContent);
            patternItem.Setup(patternData);
            patternItem.ValueChanged += OnPatternValueChanged;

            patternItems.Add(patternItem);
        }

        RefreshLayout();
    }

    private void UnregisterPatternItemListeners()
    {
        for (int i = 0; i < patternItems.Count; i++)
        {
            LobbyHostPatternToggleItem patternItem = patternItems[i];

            if (patternItem != null)
            {
                patternItem.ValueChanged -= OnPatternValueChanged;
            }
        }
    }

    private void LoadPatternSelection(IReadOnlyList<BingoPatternType> selectedPatterns)
    {
        HashSet<BingoPatternType> selectedPatternSet = new HashSet<BingoPatternType>();

        if (selectedPatterns != null)
        {
            for (int i = 0; i < selectedPatterns.Count; i++)
            {
                selectedPatternSet.Add(selectedPatterns[i]);
            }
        }

        for (int i = 0; i < patternItems.Count; i++)
        {
            LobbyHostPatternToggleItem patternItem = patternItems[i];

            if (patternItem == null)
            {
                continue;
            }

            patternItem.SetIsOnWithoutNotify(selectedPatternSet.Contains(patternItem.PatternType));
        }

        if (GetSelectedPatternCount() == 0 && workingData.usesDefaultPatterns)
        {
            ApplyDefaultPatternsForGameMode(workingData.gameModeType);
        }

        UpdateWorkingPatternList();
    }

    private void ApplyDefaultPatternsForGameMode(BingoGameModeType gameModeType)
    {
        BingoGameModeData gameModeData = GameModeManager.instance != null
            ? GameModeManager.instance.GetGameModeData(gameModeType)
            : null;

        if (gameModeData == null)
        {
            return;
        }

        List<BingoPatternData> defaultPatterns = gameModeData.GetAllPatterns();
        HashSet<BingoPatternType> defaultPatternTypes = new HashSet<BingoPatternType>();

        for (int i = 0; i < defaultPatterns.Count; i++)
        {
            BingoPatternData patternData = defaultPatterns[i];

            if (patternData != null)
            {
                defaultPatternTypes.Add(patternData.PatternType);
            }
        }

        for (int i = 0; i < patternItems.Count; i++)
        {
            LobbyHostPatternToggleItem patternItem = patternItems[i];

            if (patternItem == null)
            {
                continue;
            }

            patternItem.SetIsOnWithoutNotify(defaultPatternTypes.Contains(patternItem.PatternType));
        }

        UpdateWorkingPatternList();
        RefreshPatternInteractableState();
    }

    private void OnPatternValueChanged(LobbyHostPatternToggleItem patternItem, bool isOn)
    {
        if (isLoadingUi || patternItem == null)
        {
            return;
        }

        workingData.usesDefaultPatterns = false;
        UpdateWorkingPatternList();
        RefreshPatternInteractableState();
        ClearError();
    }

    private void UpdateWorkingPatternList()
    {
        workingData.patternTypes.Clear();

        for (int i = 0; i < patternItems.Count; i++)
        {
            LobbyHostPatternToggleItem patternItem = patternItems[i];

            if (patternItem == null || !patternItem.IsOn)
            {
                continue;
            }

            if (!workingData.patternTypes.Contains(patternItem.PatternType))
            {
                workingData.patternTypes.Add(patternItem.PatternType);
            }
        }
    }

    private void RefreshPatternInteractableState()
    {
        int selectedPatternCount = GetSelectedPatternCount();

        for (int i = 0; i < patternItems.Count; i++)
        {
            LobbyHostPatternToggleItem patternItem = patternItems[i];

            if (patternItem == null)
            {
                continue;
            }

            bool canInteract = !(selectedPatternCount == 1 && patternItem.IsOn);
            patternItem.SetInteractable(canInteract);
        }
    }

    private int GetSelectedPatternCount()
    {
        int selectedPatternCount = 0;

        for (int i = 0; i < patternItems.Count; i++)
        {
            if (patternItems[i] != null && patternItems[i].IsOn)
            {
                selectedPatternCount++;
            }
        }

        return selectedPatternCount;
    }

    private void ClearPatternContent()
    {
        UnregisterPatternItemListeners();
        patternItems.Clear();

        if (patternListContent == null)
        {
            return;
        }

        for (int i = patternListContent.childCount - 1; i >= 0; i--)
        {
            Transform child = patternListContent.GetChild(i);

            if (child == null)
            {
                continue;
            }

            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    #endregion

    #region Listener Setup

    private void RegisterListeners()
    {
        if (gameModeDropdown != null)
        {
            gameModeDropdown.onValueChanged.RemoveListener(OnGameModeChanged);
            gameModeDropdown.onValueChanged.AddListener(OnGameModeChanged);
        }

        if (ballCountDropdown != null)
        {
            ballCountDropdown.onValueChanged.RemoveListener(OnBallCountChanged);
            ballCountDropdown.onValueChanged.AddListener(OnBallCountChanged);
        }

        if (useFreeCellToggle != null)
        {
            useFreeCellToggle.onValueChanged.RemoveListener(OnUseFreeCellChanged);
            useFreeCellToggle.onValueChanged.AddListener(OnUseFreeCellChanged);
        }

        if (unlimitedPlayersToggle != null)
        {
            unlimitedPlayersToggle.onValueChanged.RemoveListener(OnUnlimitedPlayersChanged);
            unlimitedPlayersToggle.onValueChanged.AddListener(OnUnlimitedPlayersChanged);
        }

        if (playerCountInput != null)
        {
            playerCountInput.onValueChanged.RemoveListener(OnPlayerCountChanged);
            playerCountInput.onValueChanged.AddListener(OnPlayerCountChanged);
        }

        if (addBotsToggle != null)
        {
            addBotsToggle.onValueChanged.RemoveListener(OnAddBotsChanged);
            addBotsToggle.onValueChanged.AddListener(OnAddBotsChanged);
        }

        if (botCountInput != null)
        {
            botCountInput.onValueChanged.RemoveListener(OnBotCountChanged);
            botCountInput.onValueChanged.AddListener(OnBotCountChanged);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ApplyAndClose);
            closeButton.onClick.AddListener(ApplyAndClose);
        }
    }

    private void UnregisterListeners()
    {
        if (gameModeDropdown != null)
        {
            gameModeDropdown.onValueChanged.RemoveListener(OnGameModeChanged);
        }

        if (ballCountDropdown != null)
        {
            ballCountDropdown.onValueChanged.RemoveListener(OnBallCountChanged);
        }

        if (useFreeCellToggle != null)
        {
            useFreeCellToggle.onValueChanged.RemoveListener(OnUseFreeCellChanged);
        }

        if (unlimitedPlayersToggle != null)
        {
            unlimitedPlayersToggle.onValueChanged.RemoveListener(OnUnlimitedPlayersChanged);
        }

        if (playerCountInput != null)
        {
            playerCountInput.onValueChanged.RemoveListener(OnPlayerCountChanged);
        }

        if (addBotsToggle != null)
        {
            addBotsToggle.onValueChanged.RemoveListener(OnAddBotsChanged);
        }

        if (botCountInput != null)
        {
            botCountInput.onValueChanged.RemoveListener(OnBotCountChanged);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ApplyAndClose);
        }
    }

    #endregion

    #region UI Events

    private void OnGameModeChanged(int value)
    {
        if (isLoadingUi || !TryGetSelectedDropdownValue(gameModeDropdown, gameModeOptions, out BingoGameModeType selectedGameModeType))
        {
            return;
        }

        workingData.gameModeType = selectedGameModeType;

        if (workingData.usesDefaultPatterns)
        {
            ApplyDefaultPatternsForGameMode(selectedGameModeType);
        }

        ClearError();
    }

    private void OnBallCountChanged(int value)
    {
        if (isLoadingUi || !TryGetSelectedDropdownValue(ballCountDropdown, ballCountOptions, out BingoBallCountType selectedBallCountType))
        {
            return;
        }

        workingData.ballCountType = selectedBallCountType;
        ClearError();
    }

    private void OnUseFreeCellChanged(bool isOn)
    {
        if (isLoadingUi)
        {
            return;
        }

        workingData.useFreeCell = isOn;
        ClearError();
    }

    private void OnUnlimitedPlayersChanged(bool isOn)
    {
        if (isLoadingUi)
        {
            return;
        }

        workingData.unlimitedPlayers = isOn;

        if (isOn)
        {
            workingData.maxPlayers = LobbySettings.instance.UnlimitedPlayerCount;
            ClearError();
        }

        ApplyUnlimitedPlayersState();
    }

    private void OnPlayerCountChanged(string value)
    {
        if (isLoadingUi)
        {
            return;
        }

        if (TryGetPlayerCountValue(false, out int playerCount))
        {
            workingData.maxPlayers = playerCount;

            if (HasError())
            {
                ClearError();
            }
        }
    }

    private void OnAddBotsChanged(bool isOn)
    {
        if (isLoadingUi)
        {
            return;
        }

        workingData.addBots = isOn;

        if (!isOn)
        {
            workingData.botCount = 0;
            ClearError();
        }
        else if (botCountInput != null && string.IsNullOrWhiteSpace(botCountInput.text))
        {
            botCountInput.SetTextWithoutNotify("1");
            workingData.botCount = 1;
        }

        ApplyAddBotsState();
    }

    private void OnBotCountChanged(string value)
    {
        if (isLoadingUi || !workingData.addBots)
        {
            return;
        }

        if (TryGetBotCountValue(false, out int botCount))
        {
            workingData.botCount = botCount;

            if (HasError())
            {
                ClearError();
            }
        }
    }

    #endregion

    #region UI State

    private void ApplyUnlimitedPlayersState()
    {
        bool unlimitedPlayers = unlimitedPlayersToggle != null && unlimitedPlayersToggle.isOn;

        SetActive(playerCountRow, !unlimitedPlayers);

        if (playerCountInput != null)
        {
            playerCountInput.interactable = !unlimitedPlayers;
        }

        RefreshLayout();
    }

    private void ApplyAddBotsState()
    {
        bool addBots = addBotsToggle != null && addBotsToggle.isOn;

        SetActive(botCountRow, addBots);

        if (botCountInput != null)
        {
            botCountInput.interactable = addBots;
        }

        RefreshLayout();
    }

    #endregion

    #region Validation / Apply

    public bool TryBuildHostSettingsData(out LobbyHostSettingsData settingsData)
    {
        settingsData = null;

        if (!isUiReady)
        {
            ShowError("Host settings are not ready.");
            return false;
        }

        if (!TryGetSelectedDropdownValue(gameModeDropdown, gameModeOptions, out BingoGameModeType selectedGameModeType))
        {
            ShowError("Game mode is not ready.");
            return false;
        }

        if (!TryGetSelectedDropdownValue(ballCountDropdown, ballCountOptions, out BingoBallCountType selectedBallCountType))
        {
            ShowError("Ball count is not ready.");
            return false;
        }

        bool useFreeCell = useFreeCellToggle != null
            ? useFreeCellToggle.isOn
            : workingData.useFreeCell;

        UpdateWorkingPatternList();

        if (workingData.patternTypes.Count == 0)
        {
            ShowError("At least one pattern must be selected.");
            return false;
        }

        bool unlimitedPlayers = unlimitedPlayersToggle != null && unlimitedPlayersToggle.isOn;
        int maxPlayers = LobbySettings.instance.UnlimitedPlayerCount;

        if (!unlimitedPlayers && !TryGetPlayerCountValue(true, out maxPlayers))
        {
            return false;
        }

        bool addBots = addBotsToggle != null && addBotsToggle.isOn;
        int botCount = 0;

        if (addBots && !TryGetBotCountValue(true, out botCount))
        {
            return false;
        }

        settingsData = new LobbyHostSettingsData
        {
            gameModeType = selectedGameModeType,
            ballCountType = selectedBallCountType,
            useFreeCell = useFreeCell,
            patternTypes = new List<BingoPatternType>(workingData.patternTypes),
            usesDefaultPatterns = workingData.usesDefaultPatterns,
            unlimitedPlayers = unlimitedPlayers,
            maxPlayers = maxPlayers,
            addBots = addBots,
            botCount = botCount
        };

        ClearError();
        return true;
    }

    private async void ApplyAndClose()
    {
        if (!TryBuildHostSettingsData(
                out LobbyHostSettingsData settingsData))
        {
            return;
        }

        Debug.Log(
         $"[LobbyHostSettings] Close / Apply Settings | " +
         $"Game Mode: {settingsData.gameModeType} | " +
         $"Ball Count: {settingsData.ballCountType} | " +
         $"Use Free Cell: {settingsData.useFreeCell} | " +
         $"Patterns: [{string.Join(", ", settingsData.patternTypes)}] | " +
         $"Uses Default Patterns: {settingsData.usesDefaultPatterns} | " +
         $"Unlimited Players: {settingsData.unlimitedPlayers} | " +
         $"Max Players: {settingsData.maxPlayers} | " +
         $"Add Bots: {settingsData.addBots} | " +
         $"Bot Count: {settingsData.botCount}");

        LobbyManager lobbyManager =
            LobbyManager.instance;

        if (lobbyManager == null ||
            lobbyManager.CurrentLobby == null)
        {
            ShowError(
                "The current lobby could not be found.");

            return;
        }

        bool settingsApplied;

        if (lobbyManager.RuntimeType == SessionRuntimeType.Local)
        {
            LobbyController lobbyController =
                lobbyManager.CurrentLobby.Controller;

            settingsApplied =
                lobbyController != null &&
                lobbyController.ApplyHostSettings(
                    lobbyManager.CurrentUserId,
                    settingsData);
        }
        else
        {
            NetworkLobbyConnection lobbyConnection =
                NetworkLobbyConnection.GetLocalConnection();

            if (lobbyConnection == null)
            {
                ShowError(
                    "The network lobby connection is not available.");

                return;
            }

            settingsApplied =
                await lobbyConnection
                    .RequestApplyHostSettingsAsync(
                        settingsData);
        }

        if (!settingsApplied)
        {
            ShowError(
                "The lobby settings could not be applied.");

            return;
        }

        workingData = settingsData;

        ClosePopup();
    }

    #endregion

    #region Input Validation

    private bool TryGetPlayerCountValue(bool showError, out int playerCount)
    {
        playerCount = LobbySettings.instance.MinimumPlayers;

        if (playerCountInput == null)
        {
            if (showError)
            {
                ShowError("Player count input is missing.");
            }

            return false;
        }

        string value = playerCountInput.text.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            if (showError)
            {
                ShowError($"Player count must be at least {LobbySettings.instance.MinimumPlayers}.");
            }

            return false;
        }

        if (!int.TryParse(value, out int parsedPlayerCount))
        {
            if (showError)
            {
                ShowError("Player count must be a number.");
            }

            return false;
        }

        if (parsedPlayerCount < LobbySettings.instance.MinimumPlayers)
        {
            if (showError)
            {
                ShowError($"Player count must be at least {LobbySettings.instance.MinimumPlayers}.");
            }

            return false;
        }

        if (parsedPlayerCount > LobbySettings.instance.UnlimitedPlayerCount)
        {
            parsedPlayerCount = LobbySettings.instance.UnlimitedPlayerCount;
            playerCountInput.SetTextWithoutNotify(parsedPlayerCount.ToString());
        }

        playerCount = parsedPlayerCount;
        return true;
    }

    private bool TryGetBotCountValue(bool showError, out int botCount)
    {
        botCount = 0;

        if (botCountInput == null)
        {
            if (showError)
            {
                ShowError("Bot count input is missing.");
            }

            return false;
        }

        string value = botCountInput.text.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            if (showError)
            {
                ShowError("Bot count must be at least 1.");
            }

            return false;
        }

        if (!int.TryParse(value, out int parsedBotCount))
        {
            if (showError)
            {
                ShowError("Bot count must be a number.");
            }

            return false;
        }

        if (parsedBotCount < 1)
        {
            if (showError)
            {
                ShowError("Bot count must be at least 1.");
            }

            return false;
        }

        if (parsedBotCount > LobbySettings.instance.UnlimitedPlayerCount)
        {
            parsedBotCount = LobbySettings.instance.UnlimitedPlayerCount;
            botCountInput.SetTextWithoutNotify(parsedBotCount.ToString());
        }

        botCount = parsedBotCount;
        return true;
    }

    #endregion

    #region Layout / Popup

    private void RefreshLayout()
    {
        if (settingsScrollView != null && settingsScrollView.content != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(settingsScrollView.content);
        }

        SettingsLayoutChanged?.Invoke();
    }

    private void ResetScroll()
    {
        if (settingsScrollView != null)
        {
            Canvas.ForceUpdateCanvases();
            settingsScrollView.verticalNormalizedPosition = 1f;
        }
    }

    private void ClosePopup()
    {
        if (PopupManager.instance != null)
        {
            PopupManager.instance.CloseActivePopup();
            return;
        }

        gameObject.SetActive(false);
    }

    #endregion

    #region Helpers

    private void SetActive(GameObject target, bool isActive)
    {
        if (target != null)
        {
            target.SetActive(isActive);
        }
    }

    private void ShowError(string message)
    {
        if (errorText != null)
        {
            errorText.text = message;
        }
    }

    private void ClearError()
    {
        if (errorText != null)
        {
            errorText.text = string.Empty;
        }
    }

    private bool HasError()
    {
        return errorText != null && !string.IsNullOrWhiteSpace(errorText.text);
    }

    #endregion
}
