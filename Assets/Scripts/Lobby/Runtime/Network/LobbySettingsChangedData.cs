using System;
using System.Collections.Generic;

[Serializable]
public class LobbySettingsChangedData
{
    #region Fields

    public string lobbyId;
    public long revision;

    public string lobbyName;
    public string roomCode;
    public bool hasPassword;
    public string lobbyPassword;

    public BingoGameModeType gameModeType;
    public string gameModeName;

    public bool hasRule;
    public BingoRuleType ruleType;

    public List<BingoPatternType> patternTypes;
    public bool usesDefaultPatterns;

    public BingoBallCountType ballCountType;
    public bool useFreeCell;

    public int playerCount;
    public int maxPlayer;
    public bool maxPlayers;

    public bool addBots;
    public int botCount;

    #endregion

    #region Constructors

    public LobbySettingsChangedData()
    {
        lobbyId = string.Empty;
        revision = 0;
        lobbyName = string.Empty;
        roomCode = string.Empty;
        hasPassword = false;
        lobbyPassword = string.Empty;
        gameModeType = BingoGameModeType.Traditional;
        gameModeName = string.Empty;
        hasRule = false;
        ruleType = BingoRuleType.Traditional;
        patternTypes = new List<BingoPatternType>();
        usesDefaultPatterns = true;
        ballCountType = BingoBallCountType.Ball75;
        useFreeCell = true;
        playerCount = 0;
        maxPlayer = 0;
        maxPlayers = false;
        addBots = false;
        botCount = 0;
    }

    public LobbySettingsChangedData(string lobbyId, long revision, LobbyViewData lobbyViewData) : this()
    {
        this.lobbyId = lobbyId ?? string.Empty;
        this.revision = revision;

        if (lobbyViewData == null)
        {
            return;
        }

        lobbyName = lobbyViewData.lobbyName ?? string.Empty;
        roomCode = lobbyViewData.roomCode ?? string.Empty;
        hasPassword = lobbyViewData.hasPassword;
        lobbyPassword = lobbyViewData.lobbyPassword ?? string.Empty;
        gameModeType = lobbyViewData.gameModeType;
        gameModeName = lobbyViewData.gameModeName ?? string.Empty;
        hasRule = lobbyViewData.hasRule;
        ruleType = lobbyViewData.ruleType;
        patternTypes = lobbyViewData.patternTypes != null ? new List<BingoPatternType>(lobbyViewData.patternTypes) : new List<BingoPatternType>();
        usesDefaultPatterns = lobbyViewData.usesDefaultPatterns;
        ballCountType = lobbyViewData.ballCountType;
        useFreeCell = lobbyViewData.useFreeCell;
        playerCount = lobbyViewData.playerCount;
        maxPlayer = lobbyViewData.maxPlayer;
        maxPlayers = lobbyViewData.maxPlayers;
        addBots = lobbyViewData.addBots;
        botCount = lobbyViewData.botCount;
    }

    #endregion
}
