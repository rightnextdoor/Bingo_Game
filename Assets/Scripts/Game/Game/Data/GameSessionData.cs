using System;
using System.Collections.Generic;

[Serializable]
public class GameSessionData
{
    public int dataVersion;
    public string gameId;
    public string lobbyId;
    public SessionRuntimeType runtimeType;
    public MainMenuPlayMode playMode;
    public GameSessionState gameState;

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
        dataVersion = 1;
        gameId = string.Empty;
        lobbyId = string.Empty;
        runtimeType = SessionRuntimeType.Local;
        playMode = MainMenuPlayMode.None;
        gameState = GameSessionState.Created;
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
        gameModeType = setupData.gameModeType;
        hasRule = setupData.hasRule;
        ruleType = setupData.ruleType;
        patternTypes = setupData.patternTypes != null
            ? new List<BingoPatternType>(setupData.patternTypes)
            : new List<BingoPatternType>();
        usesDefaultPatterns = setupData.usesDefaultPatterns;
        ballCountType = setupData.ballCountType;
        useFreeCell = setupData.useFreeCell;

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
        gameId = gameSessionData.gameId ?? string.Empty;
        lobbyId = gameSessionData.lobbyId ?? string.Empty;
        runtimeType = gameSessionData.runtimeType;
        playMode = gameSessionData.playMode;
        gameState = gameSessionData.gameState;
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
}
