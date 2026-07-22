using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LobbyController
{
    #region Lobby Setup Fields

    private const string DefaultSoloLobbyName = "Solo Lobby";
    private const string DefaultOnlineLobbyName = "Online Lobby";
    private const string DefaultCustomLobbyName = "Custom Lobby";

    [SerializeField] private string lobbyName = string.Empty;
    [SerializeField] private int maxPlayers;
    [SerializeField] private bool unlimitedPlayers;
    [SerializeField] private LobbyCloseReason closeReason = LobbyCloseReason.None;

    [NonSerialized] private Lobby lobby;

    public string LobbyName => lobbyName;
    public int MaxPlayers => maxPlayers;
    public bool UnlimitedPlayers => unlimitedPlayers;
    public LobbyCloseReason CloseReason => closeReason;

    #endregion

    #region Player Fields

    [SerializeField] private List<LobbyPlayerData> players = new List<LobbyPlayerData>();

    public IReadOnlyList<LobbyPlayerData> Players => players;
    public int PlayerCount => players != null ? players.Count : 0;
    public bool IsFull => maxPlayers > 0 && PlayerCount >= maxPlayers;
    public bool IsEmpty => PlayerCount == 0;

    [field: NonSerialized]
    public event Action<LobbyController, LobbyExitResult> PlayerExitProcessed;

    #endregion

    #region Timer Fields

    [SerializeField] private LobbyTimer timer = new LobbyTimer();

    public LobbyTimer Timer => timer;
    public bool IsTimerActive => timer != null && timer.IsActive;
    public double TimerEndTime => timer != null ? timer.EndTime : 0d;

    #endregion

    #region Custom Lobby Fields

    private const int MinimumCustomRoomCode = 1000;
    private const int MaximumCustomRoomCodeExclusive = 10000;
    private const int CustomRoomCodeGenerationAttempts = 100;

    [SerializeField] private string roomCode = string.Empty;
    [NonSerialized] private string password = string.Empty;
    [SerializeField] private bool hasPassword;

    public string RoomCode => roomCode;
    public bool HasPassword => hasPassword;

    #endregion

    #region Game Data Fields

    [SerializeField] private BingoGameModeType gameModeType = BingoGameModeType.Traditional;

    [SerializeField] private bool hasRule;
    [SerializeField] private BingoRuleType ruleType = BingoRuleType.Traditional;

    [SerializeField] private List<BingoPatternType> patternTypes = new List<BingoPatternType>();
    [SerializeField] private bool usesDefaultPatterns = true;

    [SerializeField] private BingoBallCountType ballCountType = BingoBallCountType.Ball75;
    [SerializeField] private bool useFreeCell = true;

    [SerializeField] private bool addBots;
    [SerializeField] private int botCount;

    public BingoGameModeType GameModeType => gameModeType;

    public bool HasRule => hasRule;
    public BingoRuleType RuleType => ruleType;

    public IReadOnlyList<BingoPatternType> PatternTypes => patternTypes;
    public bool UsesDefaultPatterns => usesDefaultPatterns;

    public BingoBallCountType BallCountType => ballCountType;
    public bool UseFreeCell => useFreeCell;

    public bool AddBots => addBots;
    public int BotCount => botCount;

    #endregion

    #region View Fields

    [NonSerialized] private List<ILobbyView> views;

    #endregion

    #region Constructors

    public LobbyController()
    {
        EnsureCollections();
        EnsureTimer();
    }

    public LobbyController(Lobby lobby, LobbySetupData lobbySetupData, Func<string, bool> isRoomCodeAvailable)
    {
        EnsureCollections();
        EnsureTimer();
        AttachLobby(lobby);
        Initialize(lobbySetupData, isRoomCodeAvailable);
    }

    #endregion

    #region Lobby Setup

    public void AttachLobby(Lobby lobby)
    {
        this.lobby = lobby;
        EnsureCollections();
    }

    private void Initialize(LobbySetupData lobbySetupData, Func<string, bool> isRoomCodeAvailable)
    {
        players = new List<LobbyPlayerData>();

        lobbyName = GetDefaultLobbyName();
        roomCode = string.Empty;
        password = string.Empty;
        hasPassword = false;

        gameModeType = BingoGameModeType.Traditional;
        ballCountType = BingoBallCountType.Ball75;
        patternTypes.Clear();
        usesDefaultPatterns = true;
        useFreeCell = true;

        unlimitedPlayers = false;
        maxPlayers = GetMinimumPlayers();

        addBots = false;
        botCount = 0;

        closeReason = LobbyCloseReason.None;

        MainMenuPlayMode playMode = lobby != null
            ? lobby.playMode
            : lobbySetupData?.playMode ?? MainMenuPlayMode.None;

        InitializeTimer(playMode);

        switch (playMode)
        {
            case MainMenuPlayMode.Solo:
                ApplySoloSetup(lobbySetupData?.soloSetupData);
                break;

            case MainMenuPlayMode.Online:
                ApplyOnlineSetup(lobbySetupData?.onlineSetupData);
                break;

            case MainMenuPlayMode.Custom:
                ApplyCustomSetup(
                    lobbySetupData?.customSetupData,
                    isRoomCodeAvailable);
                break;
        }

        gameModeType = ResolveGameModeType(gameModeType);
        ballCountType = ResolveBallCountType(ballCountType);
        ResolveGameModeData();
    }

    private void ApplySoloSetup(SoloLobbySetupData soloSetupData)
    {
        lobbyName = DefaultSoloLobbyName;

        if (soloSetupData == null)
        {
            return;
        }

        gameModeType = soloSetupData.gameModeType;
        ballCountType = soloSetupData.ballCountType;
        useFreeCell = soloSetupData.useFreeCell;
        unlimitedPlayers = soloSetupData.unlimitedPlayers;
        maxPlayers = GetValidMaximumPlayers(soloSetupData.maxPlayers, unlimitedPlayers);
    }

    private void ApplyOnlineSetup(OnlineLobbySetupData onlineSetupData)
    {
        lobbyName = DefaultOnlineLobbyName;

        if (onlineSetupData == null)
        {
            return;
        }

        gameModeType = onlineSetupData.gameModeType;
        ballCountType = onlineSetupData.ballCountType;
        useFreeCell = onlineSetupData.useFreeCell;
        unlimitedPlayers = onlineSetupData.unlimitedPlayers;
        maxPlayers = GetValidMaximumPlayers(onlineSetupData.maxPlayers, unlimitedPlayers);
    }

    private void ApplyCustomSetup(CustomLobbySetupData customSetupData, Func<string, bool> isRoomCodeAvailable)
    {
        lobbyName = DefaultCustomLobbyName;

        if (customSetupData == null ||
            customSetupData.actionType != CustomLobbyActionType.HostLobby)
        {
            return;
        }

        CustomHostLobbySetupData hostSetupData = customSetupData.hostSetupData;

        if (hostSetupData == null)
        {
            return;
        }

        lobbyName = string.IsNullOrWhiteSpace(hostSetupData.lobbyName)
            ? DefaultCustomLobbyName
            : hostSetupData.lobbyName.Trim();

        password = hostSetupData.password ?? string.Empty;
        hasPassword = !string.IsNullOrWhiteSpace(password);

        gameModeType = hostSetupData.gameModeType;
        ballCountType = hostSetupData.ballCountType;
        useFreeCell = hostSetupData.useFreeCell;
        unlimitedPlayers = hostSetupData.unlimitedPlayers;
        maxPlayers = GetValidMaximumPlayers(hostSetupData.maxPlayers, unlimitedPlayers);

        roomCode = GenerateUniqueRoomCode(isRoomCodeAvailable);
    }

    private string GetDefaultLobbyName()
    {
        if (lobby == null)
        {
            return string.Empty;
        }

        switch (lobby.playMode)
        {
            case MainMenuPlayMode.Solo:
                return DefaultSoloLobbyName;

            case MainMenuPlayMode.Online:
                return DefaultOnlineLobbyName;

            case MainMenuPlayMode.Custom:
                return DefaultCustomLobbyName;

            default:
                return string.Empty;
        }
    }

    private int GetMinimumPlayers()
    {
        return LobbySettings.instance.MinimumPlayers;
    }

    private int GetUnlimitedPlayerCount()
    {
        return LobbySettings.instance.UnlimitedPlayerCount;
    }

    private int GetValidMaximumPlayers(int requestedMaximumPlayers, bool isUnlimited)
    {
        if (isUnlimited)
        {
            return GetUnlimitedPlayerCount();
        }

        return Mathf.Clamp(requestedMaximumPlayers, GetMinimumPlayers(), GetUnlimitedPlayerCount());
    }

    #endregion

    #region Players

    public bool HasPlayer(string userId)
    {
        return GetPlayer(userId) != null;
    }

    public LobbyPlayerData GetPlayer(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        EnsureCollections();

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData?.userData == null)
            {
                continue;
            }

            if (playerData.userData.userId == userId)
            {
                return playerData;
            }
        }

        return null;
    }

    public bool AddPlayer(LobbyPlayerData playerData)
    {
        if (playerData == null || !playerData.HasValidUser)
        {
            return false;
        }

        EnsureCollections();

        if (lobby != null && lobby.lobbyState == LobbyState.Closed)
        {
            return false;
        }

        if (HasPlayer(playerData.userData.userId) || IsFull)
        {
            return false;
        }

        GeneratePlayerBoard(playerData);

        players.Add(playerData);
        RefreshViews();

        return true;
    }

    public bool SetLobbySceneReady(string userId, bool isReady)
    {
        LobbyPlayerData playerData = GetPlayer(userId);

        if (playerData == null)
        {
            return false;
        }

        if (playerData.isLobbySceneReady == isReady)
        {
            return true;
        }

        playerData.isLobbySceneReady = isReady;
        RefreshViews();

        return true;
    }

    public LobbyExitResult RemovePlayer(string userId, LobbyPlayerExitReason exitReason)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return LobbyExitResult.Failed(
                userId,
                exitReason,
                "The player UserId is missing.");
        }

        EnsureCollections();

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData?.userData == null ||
                playerData.userData.userId != userId)
            {
                continue;
            }

            bool wasHost = playerData.isHost;

            players.RemoveAt(i);

            bool customHostLeft =
                lobby != null &&
                lobby.playMode == MainMenuPlayMode.Custom &&
                wasHost;

            bool shouldCloseLobby = customHostLeft || IsEmpty;

            LobbyCloseReason requestedCloseReason = customHostLeft
                ? LobbyCloseReason.HostLeft
                : IsEmpty
                    ? LobbyCloseReason.Empty
                    : LobbyCloseReason.None;

            LobbyExitResult result = LobbyExitResult.Succeeded(
                userId,
                exitReason,
                wasHost,
                PlayerCount,
                shouldCloseLobby,
                requestedCloseReason);

            RefreshViews();
            PlayerExitProcessed?.Invoke(this, result);

            return result;
        }

        return LobbyExitResult.Failed(
            userId,
            exitReason,
            "The player was not found in the lobby.");
    }

    #endregion

    #region Boards

    public bool RerollPlayerBoard(string userId)
    {
        if (lobby == null || lobby.lobbyState != LobbyState.Open)
        {
            return false;
        }

        LobbyPlayerData playerData = GetPlayer(userId);

        if (playerData == null)
        {
            return false;
        }

        GeneratePlayerBoard(playerData);
        RefreshViews();

        return true;
    }

    private void GeneratePlayerBoard(LobbyPlayerData playerData)
    {
        if (playerData == null)
        {
            return;
        }

        playerData.boardData = BingoBoardGenerator.Generate(ballCountType, useFreeCell);
    }

    private void RegenerateAllPlayerBoards()
    {
        for (int i = 0; i < players.Count; i++)
        {
            GeneratePlayerBoard(players[i]);
        }
    }

    #endregion

    #region Lobby State

    public List<string> CloseLobby(LobbyCloseReason requestedCloseReason)
    {
        EnsureCollections();

        closeReason = requestedCloseReason;

        if (lobby != null)
        {
            lobby.lobbyState = LobbyState.Closed;
        }

        List<string> removedUserIds = new List<string>();

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData?.userData == null ||
                string.IsNullOrWhiteSpace(playerData.userData.userId))
            {
                continue;
            }

            removedUserIds.Add(playerData.userData.userId);
        }

        players.Clear();
        RefreshViews();

        return removedUserIds;
    }

    #endregion

    #region Timer

    public bool BeginFinalCountdown()
    {
        if (lobby == null || lobby.lobbyState != LobbyState.Open)
        {
            return false;
        }

        lobby.lobbyState = LobbyState.FinalCountdown;
        timer.StartFinalCountdown();

        RefreshViews();

        return true;
    }

    public bool BeginFinalCountdown(string requesterUserId)
    {
        if (lobby == null ||
            (lobby.playMode != MainMenuPlayMode.Solo &&
             lobby.playMode != MainMenuPlayMode.Custom))
        {
            return false;
        }

        LobbyPlayerData requesterPlayer = GetPlayer(requesterUserId);

        if (requesterPlayer == null || !requesterPlayer.isHost)
        {
            return false;
        }

        return BeginFinalCountdown();
    }

    private void InitializeTimer(MainMenuPlayMode playMode)
    {
        EnsureTimer();

        LobbySettings timerSettings = LobbySettings.instance;

        if (timerSettings == null)
        {
            timer.Stop();
            Debug.LogWarning("[LobbyController] LobbySettings was not found. The lobby timer was not started.");
            return;
        }

        timer.Initialize(
            playMode,
            timerSettings.OnlineTimerSeconds,
            timerSettings.FinalCountdownSeconds);
    }

    private void EnsureTimer()
    {
        timer ??= new LobbyTimer();
    }

    public bool CompleteFinalCountdown()
    {
        if (lobby == null ||
            lobby.lobbyState != LobbyState.FinalCountdown ||
            timer == null ||
            !timer.IsActive ||
            LobbyTimer.GetCurrentTime() < timer.EndTime)
        {
            return false;
        }

        lobby.lobbyState = LobbyState.InGame;
        timer.Stop();

        RefreshViews();

        return true;
    }

    #endregion

    #region Custom Lobby Access

    public bool MatchesRoomCode(string requestedRoomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode) ||
            string.IsNullOrWhiteSpace(requestedRoomCode))
        {
            return false;
        }

        return string.Equals(
            NormalizeRoomCode(roomCode),
            NormalizeRoomCode(requestedRoomCode),
            StringComparison.OrdinalIgnoreCase);
    }

    public bool IsPasswordValid(string requestedPassword)
    {
        if (!HasPassword)
        {
            return true;
        }

        requestedPassword ??= string.Empty;

        return string.Equals(
            password,
            requestedPassword,
            StringComparison.Ordinal);
    }

    public bool EnsureCustomRoomCode(Func<string, bool> isRoomCodeAvailable)
    {
        if (lobby == null || lobby.playMode != MainMenuPlayMode.Custom)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(roomCode))
        {
            return true;
        }

        roomCode = GenerateUniqueRoomCode(isRoomCodeAvailable);
        RefreshViews();

        return !string.IsNullOrWhiteSpace(roomCode);
    }

    private string GenerateUniqueRoomCode(Func<string, bool> isRoomCodeAvailable)
    {
        for (int attempt = 0; attempt < CustomRoomCodeGenerationAttempts; attempt++)
        {
            string proposedRoomCode = UnityEngine.Random
                .Range(MinimumCustomRoomCode, MaximumCustomRoomCodeExclusive)
                .ToString();

            if (isRoomCodeAvailable == null ||
                isRoomCodeAvailable(proposedRoomCode))
            {
                return proposedRoomCode;
            }
        }

        Debug.LogWarning("[LobbyController] Could not generate a unique Custom lobby room code.");
        return string.Empty;
    }

    private string NormalizeRoomCode(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    #endregion

    #region Game Data

    public void RefreshResolvedGameData()
    {
        ResolveGameModeData();
        RefreshViews();
    }

    private BingoGameModeType ResolveGameModeType(BingoGameModeType requestedGameModeType)
    {
        if (!Enum.IsDefined(typeof(BingoGameModeType), requestedGameModeType))
        {
            requestedGameModeType = BingoGameModeType.Traditional;
        }

        GameModeManager gameModeManager = GameModeManager.instance;

        if (gameModeManager == null)
        {
            return requestedGameModeType;
        }

        if (gameModeManager.HasGameMode(requestedGameModeType))
        {
            return requestedGameModeType;
        }

        IReadOnlyList<BingoGameModeData> gameModes = gameModeManager.GameModes;

        for (int i = 0; i < gameModes.Count; i++)
        {
            BingoGameModeData gameModeData = gameModes[i];

            if (gameModeData != null)
            {
                return gameModeData.GameModeType;
            }
        }

        return requestedGameModeType;
    }

    private BingoBallCountType ResolveBallCountType(BingoBallCountType requestedBallCountType)
    {
        if (Enum.IsDefined(typeof(BingoBallCountType), requestedBallCountType))
        {
            return requestedBallCountType;
        }

        return BingoBallCountType.Ball75;
    }

    private void ResolveGameModeData()
    {
        GameModeManager gameModeManager = GameModeManager.instance;

        if (gameModeManager == null)
        {
            return;
        }

        gameModeType = ResolveGameModeType(gameModeType);

        BingoGameModeData gameModeData = gameModeManager.GetGameModeData(gameModeType);

        hasRule = false;

        if (gameModeData == null)
        {
            return;
        }

        if (gameModeData.RuleData != null)
        {
            hasRule = true;
            ruleType = gameModeData.RuleData.RuleType;
        }

        if (usesDefaultPatterns)
        {
            ApplyDefaultPatterns(gameModeData);
        }
    }

    public bool ApplyHostSettings(string requesterUserId, LobbyHostSettingsData settingsData)
    {
        if (lobby == null || settingsData == null)
        {
            return false;
        }

        if (lobby.lobbyState != LobbyState.Open)
        {
            return false;
        }

        if (lobby.playMode != MainMenuPlayMode.Solo &&
            lobby.playMode != MainMenuPlayMode.Custom)
        {
            return false;
        }

        LobbyPlayerData requesterPlayer = GetPlayer(requesterUserId);

        if (requesterPlayer == null || !requesterPlayer.isHost)
        {
            return false;
        }

        if (!settingsData.usesDefaultPatterns &&
            (settingsData.patternTypes == null || settingsData.patternTypes.Count == 0))
        {
            return false;
        }

        BingoBallCountType previousBallCountType = ballCountType;
        bool previousUseFreeCell = useFreeCell;

        gameModeType = ResolveGameModeType(settingsData.gameModeType);
        ballCountType = ResolveBallCountType(settingsData.ballCountType);
        useFreeCell = settingsData.useFreeCell;

        usesDefaultPatterns = settingsData.usesDefaultPatterns;

        if (usesDefaultPatterns)
        {
            patternTypes.Clear();
        }
        else
        {
            ApplyCustomPatterns(settingsData.patternTypes);
        }

        unlimitedPlayers = settingsData.unlimitedPlayers;
        maxPlayers = GetValidMaximumPlayers(settingsData.maxPlayers, unlimitedPlayers);

        addBots = settingsData.addBots;
        botCount = addBots ? Mathf.Max(0, settingsData.botCount) : 0;

        ResolveGameModeData();

        bool boardGenerationChanged =
            ballCountType != previousBallCountType ||
            useFreeCell != previousUseFreeCell;

        if (boardGenerationChanged)
        {
            RegenerateAllPlayerBoards();
        }

        RefreshViews();

        return true;
    }

    private void ApplyDefaultPatterns(BingoGameModeData gameModeData)
    {
        patternTypes.Clear();

        if (gameModeData == null)
        {
            return;
        }

        List<BingoPatternData> patterns = gameModeData.GetAllPatterns();

        for (int i = 0; i < patterns.Count; i++)
        {
            BingoPatternData patternData = patterns[i];

            if (patternData == null || patternTypes.Contains(patternData.PatternType))
            {
                continue;
            }

            patternTypes.Add(patternData.PatternType);
        }
    }

    private void ApplyCustomPatterns(List<BingoPatternType> requestedPatternTypes)
    {
        patternTypes.Clear();

        if (requestedPatternTypes == null)
        {
            return;
        }

        for (int i = 0; i < requestedPatternTypes.Count; i++)
        {
            BingoPatternType patternType = requestedPatternTypes[i];

            if (!Enum.IsDefined(typeof(BingoPatternType), patternType) ||
                patternTypes.Contains(patternType))
            {
                continue;
            }

            patternTypes.Add(patternType);
        }
    }

    private string GetGameModeName()
    {
        if (GameModeManager.instance == null)
        {
            return gameModeType.ToString();
        }

        return GameModeManager.instance.GetGameModeName(gameModeType);
    }

    #endregion

    #region Lobby Views

    public void BindView(ILobbyView view)
    {
        if (view == null)
        {
            return;
        }

        EnsureCollections();

        if (!views.Contains(view))
        {
            views.Add(view);
        }

        ResolveGameModeData();
        view.DisplayLobbyInfo(BuildViewData());
    }

    public void UnbindView(ILobbyView view)
    {
        if (view == null || views == null)
        {
            return;
        }

        views.Remove(view);
    }

    public void RefreshViews()
    {
        if (views == null || views.Count == 0)
        {
            return;
        }

        LobbyViewData lobbyViewData = BuildViewData();

        for (int i = views.Count - 1; i >= 0; i--)
        {
            ILobbyView view = views[i];

            if (view == null)
            {
                views.RemoveAt(i);
                continue;
            }

            view.DisplayLobbyInfo(lobbyViewData);
        }
    }

    public LobbyViewData BuildViewData()
    {
        EnsureCollections();
        ResolveGameModeData();

        LobbyViewData lobbyViewData = new LobbyViewData
        {
            runtimeType = GetRuntimeType(),
            lobbyId = lobby != null ? lobby.GetLobbyId() : string.Empty,
            playMode = lobby != null ? lobby.playMode : MainMenuPlayMode.None,
            lobbyState = lobby != null ? lobby.lobbyState : LobbyState.Open,
            isTimerActive = IsTimerActive,
            timerEndTime = TimerEndTime,
            lobbyName = lobbyName,
            roomCode = roomCode,
            hasPassword = HasPassword,
            lobbyPassword = password,
            gameModeType = gameModeType,
            gameModeName = GetGameModeName(),
            hasRule = hasRule,
            ruleType = ruleType,
            patternTypes = new List<BingoPatternType>(patternTypes),
            usesDefaultPatterns = usesDefaultPatterns,
            ballCountType = ballCountType,
            useFreeCell = useFreeCell,
            playerCount = GetVisiblePlayerCount(),
            maxPlayers = maxPlayers,
            unlimitedPlayers = unlimitedPlayers,
            playerBoards = BuildPlayerBoardViewDataList(),
            addBots = addBots,
            botCount = botCount,
            playerNames = BuildPlayerNameList()
        };

        return lobbyViewData;
    }

    private int GetVisiblePlayerCount()
    {
        int visiblePlayerCount = 0;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData != null && playerData.isLobbySceneReady)
            {
                visiblePlayerCount++;
            }
        }

        return visiblePlayerCount;
    }

    private List<string> BuildPlayerNameList()
    {
        List<string> playerNames = new List<string>();

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData?.userData == null || !playerData.isLobbySceneReady || string.IsNullOrWhiteSpace(playerData.userData.playerName))
            {
                continue;
            }

            playerNames.Add(playerData.userData.playerName);
        }

        return playerNames;
    }

    private List<LobbyPlayerBoardViewData> BuildPlayerBoardViewDataList()
    {
        List<LobbyPlayerBoardViewData> playerBoards = new List<LobbyPlayerBoardViewData>();

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData?.userData == null ||
                string.IsNullOrWhiteSpace(playerData.userData.userId))
            {
                continue;
            }

            playerBoards.Add(
                new LobbyPlayerBoardViewData(
                    playerData.userData.userId,
                    playerData.boardData));
        }

        return playerBoards;
    }

    private SessionRuntimeType GetRuntimeType()
    {
        if (lobby != null && lobby.playMode == MainMenuPlayMode.Solo)
        {
            return SessionRuntimeType.Local;
        }

        return SessionRuntimeType.Network;
    }

    #endregion

    #region Helpers

    private void EnsureCollections()
    {
        players ??= new List<LobbyPlayerData>();
        patternTypes ??= new List<BingoPatternType>();
        views ??= new List<ILobbyView>();
    }

    #endregion
}
