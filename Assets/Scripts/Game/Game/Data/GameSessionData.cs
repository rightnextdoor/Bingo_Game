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

    public List<GamePlayerData> players;

    public GameSessionData()
    {
        dataVersion = 5;
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
        players = new List<GamePlayerData>();
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

        GameSettings settings = GameSettings.instance;
        gamePlayController.Initialize(
            gameModeType,
            ballCountType,
            useFreeCell,
            settings != null ? settings.FirstBallCountdownSeconds : GameSettings.DefaultFirstBallCountdownSeconds,
            settings != null ? settings.NextBallCountdownSeconds : GameSettings.DefaultNextBallCountdownSeconds,
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

    public bool FinalizeEligiblePlayers(GamePlayerStatus finalStatus)
    {
        if (players == null || finalStatus == GamePlayerStatus.Eligible)
        {
            return false;
        }

        bool changed = false;

        for (int i = 0; i < players.Count; i++)
        {
            GamePlayerData playerData = players[i];

            if (playerData == null || playerData.gameStatus != GamePlayerStatus.Eligible)
            {
                continue;
            }

            playerData.gameStatus = finalStatus;
            changed = true;
        }

        return changed;
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
    public bool isSubmitTimerActive;
    public double submitTimerEndTime;

    public GamePlayerStateChangedData()
    {
        gameId = string.Empty;
        lobbyId = string.Empty;
        revision = 0;
        userId = string.Empty;
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
        isSubmitTimerActive = playerData.isSubmitTimerActive;
        submitTimerEndTime = playerData.submitTimerEndTime;
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
    public bool matchCompleted;
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
