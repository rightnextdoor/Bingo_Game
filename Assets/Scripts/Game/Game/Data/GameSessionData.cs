using System;
using System.Collections.Generic;

[Serializable]
public class GameSessionData
{
    public int dataVersion;
    public long revision;
    public string gameId;
    public string lobbyId;
    public SessionRuntimeType runtimeType;
    public MainMenuPlayMode playMode;
    public GameSessionState gameState;
    public GamePlayController gamePlayController;

    public string lobbyName;
    public string roomCode;
    public bool hasPassword;
    public string lobbyPassword;

    public BingoGameModeType gameModeType;
    public bool hasRule;
    public BingoRuleType ruleType;
    public List<BingoPatternType> patternTypes;
    public bool usesDefaultPatterns;
    public BingoBallCountType ballCountType;
    public bool useFreeCell;
    public bool hasRiskMatchDurationOverride;
    public float riskMatchDurationMinutes;

    public bool hasCachedScoreValues;
    public int cachedLossPoints;
    public int cachedDeathWinPoints;

    public int lastRiskPatternScanBallCallCount;
    public int lastRiskSubmitCutoffBallCallCount;

    public List<GamePlayerData> players;

    [NonSerialized]
    private List<GameBingoCheckResolvedData> pendingBingoCheckPresentations;

    public GameSessionData()
    {
        dataVersion = 9;
        revision = 1;
        gameId = string.Empty;
        lobbyId = string.Empty;
        runtimeType = SessionRuntimeType.Local;
        playMode = MainMenuPlayMode.None;
        gameState = GameSessionState.Created;
        gamePlayController = new GamePlayController();
        lobbyName = string.Empty;
        roomCode = string.Empty;
        hasPassword = false;
        lobbyPassword = string.Empty;
        gameModeType = BingoGameModeType.Traditional;
        hasRule = false;
        ruleType = BingoRuleType.Traditional;
        patternTypes = new List<BingoPatternType>();
        usesDefaultPatterns = true;
        ballCountType = BingoBallCountType.Ball75;
        useFreeCell = true;
        hasRiskMatchDurationOverride = false;
        riskMatchDurationMinutes = GameSettings.DefaultRiskMatchDurationMinutes;
        hasCachedScoreValues = false;
        cachedLossPoints = 0;
        cachedDeathWinPoints = 0;
        lastRiskPatternScanBallCallCount = 0;
        lastRiskSubmitCutoffBallCallCount = 0;
        players = new List<GamePlayerData>();
        pendingBingoCheckPresentations = new List<GameBingoCheckResolvedData>();
    }

    public GameSessionData(string gameId, GameSessionSetupData setupData) : this()
    {
        if (setupData == null)
        {
            return;
        }

        this.gameId = gameId ?? string.Empty;
        lobbyId = setupData.lobbyId ?? string.Empty;
        runtimeType = setupData.runtimeType;
        playMode = setupData.playMode;
        lobbyName = setupData.lobbyName ?? string.Empty;
        roomCode = setupData.roomCode ?? string.Empty;
        hasPassword = setupData.hasPassword;
        lobbyPassword = setupData.lobbyPassword ?? string.Empty;
        gameModeType = setupData.gameModeType;
        hasRule = setupData.hasRule;
        ruleType = setupData.ruleType;
        patternTypes = setupData.patternTypes != null
            ? new List<BingoPatternType>(setupData.patternTypes)
            : new List<BingoPatternType>();
        usesDefaultPatterns = setupData.usesDefaultPatterns;
        ballCountType = setupData.ballCountType;
        useFreeCell = setupData.useFreeCell;
        hasRiskMatchDurationOverride = setupData.hasRiskMatchDurationOverride;
        riskMatchDurationMinutes = ResolveRiskMatchDurationMinutes(setupData);
        EnsureScoreValuesCached();

        GameSettings settings = GameSettings.instance;
        gamePlayController.Initialize(
            gameModeType,
            ballCountType,
            useFreeCell,
            settings != null ? settings.FirstBallCountdownSeconds : GameSettings.DefaultFirstBallCountdownSeconds,
            settings != null ? settings.NextBallCountdownSeconds : GameSettings.DefaultNextBallCountdownSeconds,
            GameSettings.MinutesToSeconds(riskMatchDurationMinutes),
            hasRule,
            ruleType);

        if (setupData.players == null)
        {
            return;
        }

        for (int i = 0; i < setupData.players.Count; i++)
        {
            players.Add(new GamePlayerData(setupData.players[i]));
        }
    }

    public GameSessionData(GameSessionData gameSessionData) : this()
    {
        if (gameSessionData == null)
        {
            return;
        }

        dataVersion = gameSessionData.dataVersion;
        revision = gameSessionData.revision;
        gameId = gameSessionData.gameId ?? string.Empty;
        lobbyId = gameSessionData.lobbyId ?? string.Empty;
        runtimeType = gameSessionData.runtimeType;
        playMode = gameSessionData.playMode;
        gameState = gameSessionData.gameState;
        gamePlayController = new GamePlayController(gameSessionData.gamePlayController);
        lobbyName = gameSessionData.lobbyName ?? string.Empty;
        roomCode = gameSessionData.roomCode ?? string.Empty;
        hasPassword = gameSessionData.hasPassword;
        lobbyPassword = gameSessionData.lobbyPassword ?? string.Empty;
        gameModeType = gameSessionData.gameModeType;
        hasRule = gameSessionData.hasRule;
        ruleType = gameSessionData.ruleType;
        patternTypes = gameSessionData.patternTypes != null
            ? new List<BingoPatternType>(gameSessionData.patternTypes)
            : new List<BingoPatternType>();
        usesDefaultPatterns = gameSessionData.usesDefaultPatterns;
        ballCountType = gameSessionData.ballCountType;
        useFreeCell = gameSessionData.useFreeCell;
        hasRiskMatchDurationOverride = gameSessionData.hasRiskMatchDurationOverride;
        riskMatchDurationMinutes = gameSessionData.riskMatchDurationMinutes;
        hasCachedScoreValues = gameSessionData.hasCachedScoreValues;
        cachedLossPoints = gameSessionData.cachedLossPoints;
        cachedDeathWinPoints = gameSessionData.cachedDeathWinPoints;
        lastRiskPatternScanBallCallCount = gameSessionData.lastRiskPatternScanBallCallCount;
        lastRiskSubmitCutoffBallCallCount = gameSessionData.lastRiskSubmitCutoffBallCallCount;

        if (gameSessionData.players == null)
        {
            return;
        }

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            players.Add(new GamePlayerData(gameSessionData.players[i]));
        }
    }

    public GamePlayerData GetPlayer(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || players == null)
        {
            return null;
        }

        for (int i = 0; i < players.Count; i++)
        {
            GamePlayerData playerData = players[i];

            if (playerData != null && string.Equals(playerData.userId, userId, StringComparison.Ordinal))
            {
                return playerData;
            }
        }

        return null;
    }

    public bool RemovePlayer(string userId)
    {
        GamePlayerData playerData = GetPlayer(userId);
        return playerData != null && players.Remove(playerData);
    }

    public int GetPlayerCountWithStatus(GamePlayerStatus status)
    {
        if (players == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] != null && players[i].gameStatus == status)
            {
                count++;
            }
        }

        return count;
    }

    public int GetEligiblePlayerCount()
    {
        if (players == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0; i < players.Count; i++)
        {
            if (IsPlayerEligibleForCount(players[i]))
            {
                count++;
            }
        }

        return count;
    }

    public bool IsPlayerEligibleForCount(GamePlayerData playerData)
    {
        return playerData != null &&
               playerData.gameStatus == GamePlayerStatus.Eligible &&
               playerData.isConnected;
    }

    public void QueueBingoCheckPresentation(GameBingoCheckResolvedData resolvedData)
    {
        if (resolvedData == null)
        {
            return;
        }

        pendingBingoCheckPresentations ??= new List<GameBingoCheckResolvedData>();
        pendingBingoCheckPresentations.Add(resolvedData);
    }

    public List<GameBingoCheckResolvedData> DrainBingoCheckPresentations()
    {
        pendingBingoCheckPresentations ??= new List<GameBingoCheckResolvedData>();
        List<GameBingoCheckResolvedData> presentations =
            new List<GameBingoCheckResolvedData>(pendingBingoCheckPresentations);
        pendingBingoCheckPresentations.Clear();
        return presentations;
    }

    public void EnsureScoreValuesCached()
    {
        if (hasCachedScoreValues ||
            GameScoreManager.instance == null ||
            !GameScoreManager.instance.IsReady)
        {
            return;
        }

        cachedLossPoints = GameScoreManager.instance.GetLossPoints();
        cachedDeathWinPoints = GameScoreManager.instance.GetDeathWinPoints();
        hasCachedScoreValues = true;
    }

    private static float ResolveRiskMatchDurationMinutes(GameSessionSetupData setupData)
    {
        if (setupData == null)
        {
            return GameSettings.DefaultRiskMatchDurationMinutes;
        }

        if (setupData.hasRiskMatchDurationOverride)
        {
            return Math.Max(0f, setupData.riskMatchDurationMinutes);
        }

        return GameSettings.instance != null
            ? GameSettings.instance.GetRiskMatchDurationMinutes(setupData.ballCountType)
            : GameSettings.DefaultRiskMatchDurationMinutes;
    }

}

[Serializable]
public class GamePlayStateChangedData
{
    public string gameId;
    public long revision;
    public GameSessionState gameState;
    public GamePlayPhase phase;
    public GameEndReason endReason;
    public int ballCallRequestCount;
    public bool isFinalBallCountdown;
    public bool isRuleCompletionAwaitingChecks;
    public bool isBallPoolExhaustedAwaitingChecks;
    public bool isRiskTimerExpiredAwaitingChecks;
    public bool deathFinalChecksWereRequired;
    public bool isBallTimerActive;
    public double ballTimerEndTime;
    public bool isRiskTimerActive;
    public double riskTimerEndTime;
    public List<int> calledNumbers;
    public List<GamePlayerMatchStateData> playerStates;

    public GamePlayStateChangedData()
    {
        gameId = string.Empty;
        revision = 0;
        gameState = GameSessionState.Created;
        calledNumbers = new List<int>();
        playerStates = new List<GamePlayerMatchStateData>();
    }

    public GamePlayStateChangedData(GameSessionData gameSessionData) : this()
    {
        if (gameSessionData == null)
        {
            return;
        }

        gameId = gameSessionData.gameId ?? string.Empty;
        revision = gameSessionData.revision;
        gameState = gameSessionData.gameState;

        GamePlayController playController = gameSessionData.gamePlayController;

        if (playController != null)
        {
            phase = playController.Phase;
            endReason = playController.EndReason;
            ballCallRequestCount = playController.BallCallRequestCount;
            isFinalBallCountdown = playController.IsFinalBallCountdown;
            isRuleCompletionAwaitingChecks = playController.IsRuleCompletionAwaitingChecks;
            isBallPoolExhaustedAwaitingChecks = playController.IsBallPoolExhaustedAwaitingChecks;
            isRiskTimerExpiredAwaitingChecks = playController.IsRiskTimerExpiredAwaitingChecks;
            deathFinalChecksWereRequired = playController.DeathFinalChecksWereRequired;
            isBallTimerActive = playController.BallTimer?.IsActive == true;
            ballTimerEndTime = playController.BallTimer?.EndTime ?? 0d;
            isRiskTimerActive = playController.RiskTimer?.IsActive == true;
            riskTimerEndTime = playController.RiskTimer?.EndTime ?? 0d;
            calledNumbers = playController.BallController?.GetCalledNumbersSnapshot() ?? new List<int>();
        }

        if (gameSessionData.players == null)
        {
            return;
        }

        for (int i = 0; i < gameSessionData.players.Count; i++)
        {
            GamePlayerData playerData = gameSessionData.players[i];

            if (playerData != null)
            {
                playerStates.Add(new GamePlayerMatchStateData(playerData));
            }
        }
    }
}

[Serializable]
public class GamePlayerMatchStateData
{
    public string userId;
    public bool isConnected;
    public bool isGameSceneReady;
    public bool canRejoin;
    public GamePlayerStatus gameStatus;
    public int currentMatchScore;
    public bool areStatisticsFinalized;
    public int finalizedScoreDelta;
    public bool isScorePersisted;
    public bool isSubmitTimerActive;
    public double submitTimerEndTime;
    public bool isRiskDecisionPending;
    public List<int> markedCellIndices;

    public GamePlayerMatchStateData()
    {
        userId = string.Empty;
        markedCellIndices = new List<int>();
    }

    public GamePlayerMatchStateData(GamePlayerData playerData) : this()
    {
        if (playerData == null)
        {
            return;
        }

        userId = playerData.userId ?? string.Empty;
        isConnected = playerData.isConnected;
        isGameSceneReady = playerData.isGameSceneReady;
        canRejoin = playerData.canRejoin;
        gameStatus = playerData.gameStatus;
        currentMatchScore = playerData.currentMatchScore;
        areStatisticsFinalized = playerData.areStatisticsFinalized;
        finalizedScoreDelta = playerData.finalizedScoreDelta;
        isScorePersisted = playerData.isScorePersisted;
        isSubmitTimerActive = playerData.isSubmitTimerActive;
        submitTimerEndTime = playerData.submitTimerEndTime;
        isRiskDecisionPending = playerData.isRiskDecisionPending;
        markedCellIndices = playerData.markedCellIndices != null
            ? new List<int>(playerData.markedCellIndices)
            : new List<int>();
    }
}

[Serializable]
public class GamePlayerStateChangedData
{
    public string gameId;
    public string lobbyId;
    public long revision;
    public string userId;
    public bool isConnected;
    public bool isGameSceneReady;
    public bool canRejoin;
    public GamePlayerStatus gameStatus;
    public int currentMatchScore;
    public bool areStatisticsFinalized;
    public int finalizedScoreDelta;
    public bool isScorePersisted;
    public bool isSubmitTimerActive;
    public double submitTimerEndTime;
    public bool isRiskDecisionPending;
    public List<int> markedCellIndices;
    public List<BingoPatternIdentity> queuedRiskPatterns;
    public List<BingoPatternIdentity> activeRiskSubmitPatterns;
    public List<BingoPatternIdentity> lateRiskPatterns;
    public List<BingoPatternIdentity> pendingRiskCheckPatterns;

    public GamePlayerStateChangedData()
    {
        gameId = string.Empty;
        lobbyId = string.Empty;
        revision = 0;
        userId = string.Empty;
        queuedRiskPatterns = new List<BingoPatternIdentity>();
        activeRiskSubmitPatterns = new List<BingoPatternIdentity>();
        lateRiskPatterns = new List<BingoPatternIdentity>();
        pendingRiskCheckPatterns = new List<BingoPatternIdentity>();
        markedCellIndices = new List<int>();
    }

    public GamePlayerStateChangedData(GameSessionData gameSessionData, GamePlayerData playerData) : this()
    {
        if (gameSessionData == null || playerData == null)
        {
            return;
        }

        gameId = gameSessionData.gameId ?? string.Empty;
        lobbyId = gameSessionData.lobbyId ?? string.Empty;
        revision = gameSessionData.revision;
        userId = playerData.userId ?? string.Empty;
        isConnected = playerData.isConnected;
        isGameSceneReady = playerData.isGameSceneReady;
        canRejoin = playerData.canRejoin;
        gameStatus = playerData.gameStatus;
        currentMatchScore = playerData.currentMatchScore;
        areStatisticsFinalized = playerData.areStatisticsFinalized;
        finalizedScoreDelta = playerData.finalizedScoreDelta;
        isScorePersisted = playerData.isScorePersisted;
        isSubmitTimerActive = playerData.isSubmitTimerActive;
        submitTimerEndTime = playerData.submitTimerEndTime;
        isRiskDecisionPending = playerData.isRiskDecisionPending;
        markedCellIndices = playerData.markedCellIndices != null
            ? new List<int>(playerData.markedCellIndices)
            : new List<int>();
        queuedRiskPatterns = BingoPatternIdentityList.Clone(playerData.queuedRiskPatterns);
        activeRiskSubmitPatterns = BingoPatternIdentityList.Clone(playerData.activeRiskSubmitPatterns);
        lateRiskPatterns = BingoPatternIdentityList.Clone(playerData.lateRiskPatterns);
        pendingRiskCheckPatterns = BingoPatternIdentityList.Clone(playerData.pendingRiskCheckPatterns);
    }
}

[Serializable]
public class GamePlayerLeftData
{
    public string gameId;
    public string lobbyId;
    public long revision;
    public string userId;

    public GamePlayerLeftData()
    {
        gameId = string.Empty;
        lobbyId = string.Empty;
        revision = 0;
        userId = string.Empty;
    }

    public GamePlayerLeftData(GameSessionData gameSessionData, string userId) : this()
    {
        if (gameSessionData == null)
        {
            return;
        }

        gameId = gameSessionData.gameId ?? string.Empty;
        lobbyId = gameSessionData.lobbyId ?? string.Empty;
        revision = gameSessionData.revision;
        this.userId = userId ?? string.Empty;
    }
}

[Serializable]
public class GamePlayerMarkedCellChangedData
{
    public string gameId;
    public string userId;
    public int cellIndex;
    public bool isMarked;

    public GamePlayerMarkedCellChangedData()
    {
        gameId = string.Empty;
        userId = string.Empty;
        cellIndex = -1;
        isMarked = false;
    }

    public GamePlayerMarkedCellChangedData(
        string gameId,
        string userId,
        int cellIndex,
        bool isMarked) : this()
    {
        this.gameId = gameId ?? string.Empty;
        this.userId = userId ?? string.Empty;
        this.cellIndex = cellIndex;
        this.isMarked = isMarked;
    }
}

[Serializable]
public class GameBingoCheckRequestData
{
    public LobbyBoardData boardData;
    public List<int> markedCellIndices;

    public GameBingoCheckRequestData()
    {
        boardData = new LobbyBoardData();
        markedCellIndices = new List<int>();
    }

    public GameBingoCheckRequestData(
        LobbyBoardData boardData,
        IReadOnlyCollection<int> markedCellIndices) : this()
    {
        this.boardData = new LobbyBoardData(boardData);

        if (markedCellIndices != null)
        {
            this.markedCellIndices.AddRange(markedCellIndices);
            this.markedCellIndices.Sort();
        }
    }
}

[Serializable]
public class GameBingoCheckResolvedData
{
    public string gameId;
    public string userId;
    public long revision;
    public bool wasAccepted;
    public string failureMessage;
    public BingoCheckResult checkResult;
    public GamePlayerStatus playerStatus;
    public int currentCheckScore;
    public int currentMatchScore;
    public bool matchCompleted;
    public bool isAutomaticCheck;
    public bool requiresRiskDecision;
    public int latePatternCount;
    public List<BingoPatternType> availablePatternTypes;

    public GameBingoCheckResolvedData()
    {
        gameId = string.Empty;
        userId = string.Empty;
        failureMessage = string.Empty;
        playerStatus = GamePlayerStatus.Eligible;
        availablePatternTypes = new List<BingoPatternType>();
    }

    public static GameBingoCheckResolvedData Rejected(
        string gameId,
        string userId,
        long revision,
        string failureMessage)
    {
        return new GameBingoCheckResolvedData
        {
            gameId = gameId ?? string.Empty,
            userId = userId ?? string.Empty,
            revision = revision,
            wasAccepted = false,
            failureMessage = failureMessage ?? string.Empty
        };
    }
}
