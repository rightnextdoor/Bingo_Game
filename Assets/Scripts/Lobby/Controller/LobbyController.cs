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
    [SerializeField] private int maxPlayer;
    [SerializeField] private bool maxPlayers;
    [SerializeField] private LobbyCloseReason closeReason = LobbyCloseReason.None;

    [NonSerialized] private Lobby lobby;

    public string LobbyName => lobbyName;
    public int MaxPlayer => maxPlayer;
    public bool MaxPlayers => maxPlayers;
    public LobbyCloseReason CloseReason => closeReason;

    #endregion

    #region Player Fields

    [SerializeField] private List<LobbyPlayerData> players = new List<LobbyPlayerData>();

    [NonSerialized] private Queue<UserData> pendingBotUsers = new Queue<UserData>();
    [NonSerialized] private Queue<string> pendingBoardRegenerationUserIds = new Queue<string>();
    [NonSerialized] private bool pendingViewRefresh;

    public IReadOnlyList<LobbyPlayerData> Players => players;
    public int PlayerCount => players != null ? players.Count : 0;
    public int SceneReadyPlayerCount => GetVisiblePlayerCount();
    public int PendingPlayerCount => pendingBotUsers != null ? pendingBotUsers.Count : 0;
    public int ReservedPlayerCount => PlayerCount + PendingPlayerCount;
    public bool HasPendingWork => pendingViewRefresh || PendingPlayerCount > 0 || (pendingBoardRegenerationUserIds != null && pendingBoardRegenerationUserIds.Count > 0);
    public bool IsFull => maxPlayer > 0 && ReservedPlayerCount >= maxPlayer;
    public bool IsEmpty => PlayerCount == 0;

    [field: NonSerialized]
    public event Action<LobbyController, LobbyExitResult> PlayerExitProcessed;

    [field: NonSerialized]
    public event Action<LobbyController, LobbyPlayerBoardViewData> PlayerBoardChanged;

    [SerializeField] private bool addBots;
    [NonSerialized] private Func<IReadOnlyList<UserData>> botUserProvider;

    #endregion

    #region Timer Fields

    [SerializeField] private LobbyTimer timer = new LobbyTimer();

    public LobbyTimer Timer => timer;
    public bool IsTimerActive => timer != null && timer.IsActive;
    public double TimerEndTime => timer != null ? timer.EndTime : 0d;
    public bool IsJoinLocked => GetIsJoinLocked();

    [field: NonSerialized] public event Action<LobbyController> FinalCountdownStarted;

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

    public BingoGameModeType GameModeType => gameModeType;

    public bool HasRule => hasRule;
    public BingoRuleType RuleType => ruleType;

    public IReadOnlyList<BingoPatternType> PatternTypes => patternTypes;
    public bool UsesDefaultPatterns => usesDefaultPatterns;

    public BingoBallCountType BallCountType => ballCountType;
    public bool UseFreeCell => useFreeCell;

    public bool AddBots => addBots;
    public int BotCount => GetBotCount();

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
        pendingBotUsers = new Queue<UserData>();
        pendingBoardRegenerationUserIds = new Queue<string>();
        pendingViewRefresh = false;

        lobbyName = GetDefaultLobbyName();
        roomCode = string.Empty;
        password = string.Empty;
        hasPassword = false;

        gameModeType = BingoGameModeType.Traditional;
        ballCountType = BingoBallCountType.Ball75;
        patternTypes.Clear();
        usesDefaultPatterns = true;
        useFreeCell = true;

        maxPlayers = false;
        maxPlayer = GetMinimumPlayers();
        addBots = false;

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
        usesDefaultPatterns = soloSetupData.usesDefaultPatterns;

        if (usesDefaultPatterns)
        {
            patternTypes.Clear();
        }
        else
        {
            ApplyCustomPatterns(soloSetupData.patternTypes);
        }

        maxPlayers = soloSetupData.maxPlayers;
        maxPlayer = GetValidMaximumPlayers(soloSetupData.maxPlayer, maxPlayers);
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
        maxPlayers = onlineSetupData.maxPlayers;
        maxPlayer = GetValidMaximumPlayers(onlineSetupData.maxPlayer, maxPlayers);
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
        usesDefaultPatterns = hostSetupData.usesDefaultPatterns;

        if (usesDefaultPatterns)
        {
            patternTypes.Clear();
        }
        else
        {
            ApplyCustomPatterns(hostSetupData.patternTypes);
        }

        maxPlayers = hostSetupData.maxPlayers;
        maxPlayer = GetValidMaximumPlayers(hostSetupData.maxPlayer, maxPlayers);

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

    private int GetMaxPlayerCount()
    {
        return LobbySettings.instance.MaxPlayerCount;
    }

    private int GetValidMaximumPlayers(int requestedMaximumPlayers, bool isMax)
    {
        if (isMax)
        {
            return GetMaxPlayerCount();
        }

        return Mathf.Clamp(requestedMaximumPlayers, GetMinimumPlayers(), GetMaxPlayerCount());
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
        return AddPlayerInternal(playerData, true);
    }

    private bool AddPlayerInternal(LobbyPlayerData playerData, bool refreshViews)
    {
        if (playerData == null || !playerData.HasValidUser)
        {
            return false;
        }

        EnsureCollections();

        if (lobby != null && lobby.lobbyState != LobbyState.Open)
        {
            return false;
        }

        if (HasPlayer(playerData.userData.userId) || IsFull)
        {
            return false;
        }

        GeneratePlayerBoard(playerData);

        players.Add(playerData);

        NotifyPlayerBoardChanged(playerData);

        if (refreshViews)
        {
            RefreshViews();
        }

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
        return true;
    }

    public bool UpdatePlayerProfile(PlayerProfileData profile)
    {
        if (profile == null || !profile.IsValid)
        {
            return false;
        }

        LobbyPlayerData playerData = GetPlayer(profile.userId);

        if (playerData?.userData == null || playerData.userData.userTag == UserTag.Bot)
        {
            return false;
        }

        string playerName = profile.playerName.Trim();
        string iconId = profile.iconId?.Trim() ?? string.Empty;

        if (string.Equals(playerData.userData.playerName, playerName, StringComparison.Ordinal) &&
            string.Equals(playerData.userData.iconId, iconId, StringComparison.Ordinal))
        {
            return false;
        }

        playerData.userData.playerName = playerName;
        playerData.userData.iconId = iconId;
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

    public void SetBotUserProvider(Func<IReadOnlyList<UserData>> provider)
    {
        botUserProvider = provider;
    }

    public bool SetPlayerReady(string userId, bool isReady)
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

        if (playerData.userData != null && playerData.userData.userTag == UserTag.Bot)
        {
            playerData.isReady = true;
            return true;
        }

        if (playerData.isReady == isReady)
        {
            return true;
        }

        playerData.isReady = isReady;

        if (TryBeginOnlineReadyFinalCountdown())
        {
            return true;
        }

        RefreshViews();

        return true;
    }

    private bool TryBeginOnlineReadyFinalCountdown()
    {
        if (lobby == null ||
            lobby.playMode != MainMenuPlayMode.Online ||
            lobby.lobbyState != LobbyState.Open)
        {
            return false;
        }

        if (PlayerCount < GetMinimumPlayers())
        {
            return false;
        }

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData == null || !playerData.isReady)
            {
                return false;
            }
        }

        return BeginFinalCountdown();
    }

    public LobbyExitResult KickPlayer(string requesterUserId, string targetUserId)
    {
        if (lobby == null ||
            (lobby.playMode != MainMenuPlayMode.Solo &&
             lobby.playMode != MainMenuPlayMode.Custom))
        {
            return LobbyExitResult.Failed(
                targetUserId,
                LobbyPlayerExitReason.Kicked,
                "This lobby does not support host kicking.");
        }

        LobbyPlayerData requesterPlayer = GetPlayer(requesterUserId);

        if (requesterPlayer == null || !requesterPlayer.isHost)
        {
            return LobbyExitResult.Failed(
                targetUserId,
                LobbyPlayerExitReason.Kicked,
                "Only the lobby host can remove a player.");
        }

        if (requesterUserId == targetUserId)
        {
            return LobbyExitResult.Failed(
                targetUserId,
                LobbyPlayerExitReason.Kicked,
                "The host cannot kick themselves.");
        }

        LobbyPlayerData targetPlayer = GetPlayer(targetUserId);

        if (targetPlayer == null)
        {
            return LobbyExitResult.Failed(
                targetUserId,
                LobbyPlayerExitReason.Kicked,
                "The target player was not found in the lobby.");
        }

        if (targetPlayer.isHost)
        {
            return LobbyExitResult.Failed(
                targetUserId,
                LobbyPlayerExitReason.Kicked,
                "The lobby host cannot be kicked.");
        }

        return RemovePlayer(targetUserId, LobbyPlayerExitReason.Kicked);
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

        NotifyPlayerBoardChanged(playerData);

        return true;
    }

    public LobbyPlayerBoardViewData GetPlayerBoardViewData(string userId)
    {
        LobbyPlayerData playerData = GetPlayer(userId);

        if (playerData == null)
        {
            return null;
        }

        return new LobbyPlayerBoardViewData(userId, playerData.boardData);
    }

    public LobbyBoardCollectionData BuildPlayerBoardCollectionData()
    {
        return new LobbyBoardCollectionData(lobby != null ? lobby.GetLobbyId() : string.Empty, BuildPlayerBoardViewDataList());
    }

    private void NotifyPlayerBoardChanged(LobbyPlayerData playerData)
    {
        if (playerData?.userData == null || string.IsNullOrWhiteSpace(playerData.userData.userId))
        {
            return;
        }

        PlayerBoardChanged?.Invoke(this, new LobbyPlayerBoardViewData(playerData.userData.userId, playerData.boardData));
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
            LobbyPlayerData playerData = players[i];
            GeneratePlayerBoard(playerData);
            NotifyPlayerBoardChanged(playerData);
        }
    }

    #endregion

    #region Bots

    public int FillBotsToMinimumPlayers()
    {
        int botsNeeded = Mathf.Max(0, GetMinimumPlayers() - PlayerCount);

        if (botsNeeded <= 0)
        {
            return 0;
        }

        int previousBotCount = BotCount;

        SetBotCountInternal(BotCount + botsNeeded);

        if (BotCount != previousBotCount)
        {
            RefreshViews();
        }

        return BotCount - previousBotCount;
    }

    private bool SetBotCountInternal(int desiredBotCount)
    {
        desiredBotCount = Mathf.Max(0, desiredBotCount);

        int currentBotCount = BotCount;

        if (desiredBotCount == currentBotCount)
        {
            return false;
        }

        if (desiredBotCount < currentBotCount)
        {
            int botsToRemove = currentBotCount - desiredBotCount;
            int removedBots = 0;

            for (int i = players.Count - 1; i >= 0 && removedBots < botsToRemove; i--)
            {
                LobbyPlayerData playerData = players[i];

                if (playerData?.userData == null ||
                    playerData.userData.userTag != UserTag.Bot)
                {
                    continue;
                }

                players.RemoveAt(i);
                removedBots++;
            }

            return removedBots > 0;
        }

        int botsToAdd = desiredBotCount - currentBotCount;
        return AddRandomBotsInternal(botsToAdd, int.MaxValue) > 0;
    }

    private int AddRandomBotsInternal(int requestedCount, int maximumTotalBots)
    {
        if (requestedCount <= 0 || botUserProvider == null)
        {
            return 0;
        }

        List<UserData> eligibleBots = BuildEligibleBotList();

        if (eligibleBots.Count == 0)
        {
            return 0;
        }

        ShuffleBots(eligibleBots);

        int availablePlayerSlots = Mathf.Max(0, maxPlayer - PlayerCount);
        int remainingBotAllowance = Mathf.Max(0, maximumTotalBots - BotCount);

        int addCount = Mathf.Min(requestedCount, eligibleBots.Count);
        addCount = Mathf.Min(addCount, availablePlayerSlots);
        addCount = Mathf.Min(addCount, remainingBotAllowance);

        int addedBots = 0;

        for (int i = 0; i < addCount; i++)
        {
            LobbyPlayerData botPlayer = new LobbyPlayerData(eligibleBots[i], false);
            botPlayer.isLobbySceneReady = true;

            if (AddPlayerInternal(botPlayer, false))
            {
                addedBots++;
            }
        }

        return addedBots;
    }

    private List<UserData> BuildEligibleBotList()
    {
        List<UserData> eligibleBots = new List<UserData>();

        IReadOnlyList<UserData> availableBots = botUserProvider?.Invoke();

        if (availableBots == null)
        {
            return eligibleBots;
        }

        for (int i = 0; i < availableBots.Count; i++)
        {
            UserData botUser = availableBots[i];

            if (botUser == null ||
                !botUser.HasUser ||
                botUser.userTag != UserTag.Bot ||
                HasPlayer(botUser.userId) ||
                HasPendingBotUser(botUser.userId))
            {
                continue;
            }

            eligibleBots.Add(botUser);
        }

        return eligibleBots;
    }

    private void ShuffleBots(List<UserData> bots)
    {
        if (bots == null)
        {
            return;
        }

        for (int i = bots.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);

            UserData temp = bots[i];
            bots[i] = bots[randomIndex];
            bots[randomIndex] = temp;
        }
    }

    private int GetBotCount()
    {
        int count = 0;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData?.userData != null &&
                playerData.userData.userTag == UserTag.Bot)
            {
                count++;
            }
        }

        return count;
    }

    private int QueueRandomBotsInternal(int requestedCount, int maximumTotalBots)
    {
        if (requestedCount <= 0 || botUserProvider == null)
        {
            return 0;
        }

        EnsurePendingWorkCollections();

        List<UserData> eligibleBots = BuildEligibleBotList();

        if (eligibleBots.Count == 0)
        {
            return 0;
        }

        ShuffleBots(eligibleBots);

        int availablePlayerSlots = Mathf.Max(0, maxPlayer - ReservedPlayerCount);
        int remainingBotAllowance = Mathf.Max(0, maximumTotalBots - BotCount - PendingPlayerCount);
        int reserveCount = Mathf.Min(requestedCount, eligibleBots.Count);
        reserveCount = Mathf.Min(reserveCount, availablePlayerSlots);
        reserveCount = Mathf.Min(reserveCount, remainingBotAllowance);

        for (int i = 0; i < reserveCount; i++)
        {
            pendingBotUsers.Enqueue(eligibleBots[i]);
        }

        return reserveCount;
    }

    private void QueueRegenerateAllPlayerBoards()
    {
        EnsurePendingWorkCollections();
        pendingBoardRegenerationUserIds.Clear();

        for (int i = 0; i < players.Count; i++)
        {
            string userId = players[i]?.userData?.userId;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                pendingBoardRegenerationUserIds.Enqueue(userId);
            }
        }
    }

    private bool HasPendingBotUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || pendingBotUsers == null)
        {
            return false;
        }

        foreach (UserData pendingBot in pendingBotUsers)
        {
            if (pendingBot != null && pendingBot.userId == userId)
            {
                return true;
            }
        }

        return false;
    }

    private void TrimPendingBotsToCapacity()
    {
        EnsurePendingWorkCollections();

        int allowedPendingBots = Mathf.Max(0, maxPlayer - PlayerCount);

        if (pendingBotUsers.Count <= allowedPendingBots)
        {
            return;
        }

        Queue<UserData> trimmedBots = new Queue<UserData>();

        while (trimmedBots.Count < allowedPendingBots && pendingBotUsers.Count > 0)
        {
            trimmedBots.Enqueue(pendingBotUsers.Dequeue());
        }

        pendingBotUsers = trimmedBots;
    }

    public bool ProcessPendingWorkBatch(int maximumItems, out LobbyWorkBatchResult batchResult)
    {
        batchResult = new LobbyWorkBatchResult();

        if (maximumItems <= 0)
        {
            return false;
        }

        EnsurePendingWorkCollections();
        bool shouldRefreshViews = pendingViewRefresh;
        pendingViewRefresh = false;
        int processedItems = 0;

        while (processedItems < maximumItems && pendingBotUsers.Count > 0)
        {
            UserData botUser = pendingBotUsers.Dequeue();
            processedItems++;

            if (botUser == null || !botUser.HasUser || HasPlayer(botUser.userId))
            {
                continue;
            }

            LobbyPlayerData botPlayer = new LobbyPlayerData(botUser, false);
            botPlayer.isLobbySceneReady = true;

            if (!AddPlayerInternal(botPlayer, false))
            {
                continue;
            }

            batchResult.addedPlayers.Add(new LobbyPlayerViewData(botPlayer));
            batchResult.changedBoards.Add(new LobbyPlayerBoardViewData(botUser.userId, botPlayer.boardData));
        }

        while (processedItems < maximumItems && pendingBoardRegenerationUserIds.Count > 0)
        {
            string userId = pendingBoardRegenerationUserIds.Dequeue();
            processedItems++;

            LobbyPlayerData playerData = GetPlayer(userId);

            if (playerData == null)
            {
                continue;
            }

            GeneratePlayerBoard(playerData);
            NotifyPlayerBoardChanged(playerData);
            batchResult.changedBoards.Add(new LobbyPlayerBoardViewData(userId, playerData.boardData));
        }

        if (shouldRefreshViews || batchResult.HasChanges)
        {
            RefreshViews();
        }

        return shouldRefreshViews || batchResult.HasChanges;
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
        EnsurePendingWorkCollections();
        pendingBotUsers.Clear();
        pendingBoardRegenerationUserIds.Clear();
        pendingViewRefresh = false;
        RefreshViews();

        return removedUserIds;
    }

    #endregion

    #region Timer

    private bool GetIsJoinLocked()
    {
        if (lobby == null || lobby.lobbyState != LobbyState.Open)
        {
            return true;
        }

        if (lobby.playMode != MainMenuPlayMode.Online || timer == null || !timer.IsActive)
        {
            return false;
        }

        LobbySettings lobbySettings = LobbySettings.instance;

        if (lobbySettings == null)
        {
            return false;
        }

        float joinLockThreshold = timer.FinalCountdownSeconds + lobbySettings.JoinLockSeconds;
        return timer.GetRemainingSeconds() <= joinLockThreshold;
    }

    public bool IsOnlineFinalCountdownDue()
    {
        return lobby != null &&
               lobby.playMode == MainMenuPlayMode.Online &&
               lobby.lobbyState == LobbyState.Open &&
               timer != null &&
               timer.HasReachedFinalCountdown();
    }

    public bool BeginFinalCountdown()
    {
        if (lobby == null || lobby.lobbyState != LobbyState.Open)
        {
            return false;
        }

        lobby.lobbyState = LobbyState.FinalCountdown;
        timer.StartFinalCountdown();

        RefreshViews();
        FinalCountdownStarted?.Invoke(this);

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

        if (PlayerCount < GetMinimumPlayers())
        {
            return false;
        }

        return BeginFinalCountdown();
    }

    public bool TryBeginOnlineFinalCountdown()
    {
        if (lobby == null || lobby.playMode != MainMenuPlayMode.Online || lobby.lobbyState != LobbyState.Open || timer == null || !timer.HasReachedFinalCountdown())
        {
            return false;
        }

        bool botsChanged = false;

        if (PlayerCount < GetMinimumPlayers())
        {
            LobbySettings lobbySettings = LobbySettings.instance;

            if (lobbySettings == null)
            {
                return false;
            }

            int requiredBots = GetMinimumPlayers() - PlayerCount;
            List<UserData> eligibleBots = BuildEligibleBotList();

            int availablePlayerSlots = Mathf.Max(0, maxPlayer - PlayerCount);
            int remainingOnlineBotAllowance = Mathf.Max(0, lobbySettings.MaxOnlineBots - BotCount);

            int maximumBotsToAdd = Mathf.Min(eligibleBots.Count, availablePlayerSlots);
            maximumBotsToAdd = Mathf.Min(maximumBotsToAdd, remainingOnlineBotAllowance);

            if (maximumBotsToAdd > 0)
            {
                int requestedBots = maximumBotsToAdd >= requiredBots ? UnityEngine.Random.Range(requiredBots, maximumBotsToAdd + 1) : maximumBotsToAdd;
                int addedBots = AddRandomBotsInternal(requestedBots, lobbySettings.MaxOnlineBots);

                botsChanged = addedBots > 0;
            }
        }

        if (PlayerCount < GetMinimumPlayers())
        {
            if (botsChanged)
            {
                RefreshViews();
            }

            return false;
        }

        lobby.lobbyState = LobbyState.FinalCountdown;

        RefreshViews();
        FinalCountdownStarted?.Invoke(this);

        return true;
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
            !timer.HasExpired())
        {
            return false;
        }

        lobby.lobbyState = LobbyState.InGame;
        timer.Stop();

        RefreshViews();

        return true;
    }

    public bool ResetAfterGameCreationFailure()
    {
        if (lobby == null ||
            (lobby.lobbyState != LobbyState.FinalCountdown && lobby.lobbyState != LobbyState.InGame))
        {
            return false;
        }

        lobby.lobbyState = LobbyState.Open;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData?.userData == null)
            {
                continue;
            }

            playerData.isReady = playerData.userData.userTag == UserTag.Bot;
        }

        InitializeTimer(lobby.playMode);
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
        string currentPassword = password ?? string.Empty;
        requestedPassword ??= string.Empty;

        return string.Equals(currentPassword, requestedPassword, StringComparison.Ordinal);
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
        return ApplyHostSettings(requesterUserId, settingsData, out _);
    }

    public bool ApplyHostSettings(string requesterUserId, LobbyHostSettingsData settingsData, out List<LobbyExitResult> capacityTrimResults)
    {
        capacityTrimResults = new List<LobbyExitResult>();

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

        maxPlayers = settingsData.maxPlayers;
        maxPlayer = GetValidMaximumPlayers(settingsData.maxPlayer, maxPlayers);

        addBots = settingsData.addBots;
        int requestedBotCount = addBots ? Mathf.Max(0, settingsData.botCount) : 0;

        ResolveGameModeData();
        TrimPendingBotsToCapacity();
        capacityTrimResults = TrimPlayersToCapacity();

        if (requestedBotCount > 0)
        {
            QueueRandomBotsInternal(requestedBotCount, int.MaxValue);
        }

        bool boardGenerationChanged = ballCountType != previousBallCountType || useFreeCell != previousUseFreeCell;

        if (boardGenerationChanged)
        {
            QueueRegenerateAllPlayerBoards();
        }

        pendingViewRefresh = true;
        return true;
    }

    private List<LobbyExitResult> TrimPlayersToCapacity()
    {
        List<LobbyExitResult> removalResults = new List<LobbyExitResult>();
        int playersToRemove = Mathf.Max(0, PlayerCount - maxPlayer);

        if (playersToRemove <= 0)
        {
            return removalResults;
        }

        List<string> userIdsToRemove = new List<string>(playersToRemove);

        for (int i = players.Count - 1; i >= 0 && userIdsToRemove.Count < playersToRemove; i--)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData?.userData == null || playerData.isHost || playerData.userData.userTag != UserTag.Bot)
            {
                continue;
            }

            userIdsToRemove.Add(playerData.userData.userId);
        }

        for (int i = players.Count - 1; i >= 0 && userIdsToRemove.Count < playersToRemove; i--)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData?.userData == null || playerData.isHost || playerData.userData.userTag == UserTag.Bot)
            {
                continue;
            }

            userIdsToRemove.Add(playerData.userData.userId);
        }

        for (int i = 0; i < userIdsToRemove.Count; i++)
        {
            LobbyExitResult removalResult = RemovePlayer(userIdsToRemove[i], LobbyPlayerExitReason.Kicked);

            if (removalResult != null && removalResult.success)
            {
                removalResults.Add(removalResult);
            }
        }

        return removalResults;
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
        return BuildViewData(true);
    }

    public LobbyViewData BuildViewData(bool includePlayers)
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
            maxPlayer = maxPlayer,
            maxPlayers = maxPlayers,
            players = includePlayers ? BuildPlayerViewDataList() : new List<LobbyPlayerViewData>(),
            addBots = addBots,
            botCount = BotCount
        };

        return lobbyViewData;
    }

    private List<LobbyPlayerViewData> BuildPlayerViewDataList()
    {
        List<LobbyPlayerViewData> playerViewData = new List<LobbyPlayerViewData>();

        LobbyPlayerData visibleHost = null;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData == null || !playerData.isLobbySceneReady || !playerData.HasValidUser)
            {
                continue;
            }

            if (playerData.isHost)
            {
                visibleHost = playerData;
                break;
            }
        }

        if (visibleHost != null)
        {
            playerViewData.Add(new LobbyPlayerViewData(visibleHost));
        }

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData == null || playerData == visibleHost || !playerData.isLobbySceneReady || !playerData.HasValidUser)
            {
                continue;
            }

            playerViewData.Add(new LobbyPlayerViewData(playerData));
        }

        return playerViewData;
    }

    private int GetVisiblePlayerCount()
    {
        int visiblePlayerCount = 0;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerData playerData = players[i];

            if (playerData != null && playerData.isLobbySceneReady && playerData.HasValidUser)
            {
                visiblePlayerCount++;
            }
        }

        return visiblePlayerCount;
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
        EnsurePendingWorkCollections();
    }

    private void EnsurePendingWorkCollections()
    {
        pendingBotUsers ??= new Queue<UserData>();
        pendingBoardRegenerationUserIds ??= new Queue<string>();
    }

    #endregion
}
