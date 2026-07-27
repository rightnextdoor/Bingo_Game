using System;
using System.Collections.Generic;

[Serializable]
public class LobbyViewData
{
    #region Fields

    public SessionRuntimeType runtimeType;

    public string lobbyId;
    public MainMenuPlayMode playMode;
    public LobbyState lobbyState;

    public bool isTimerActive;
    public double timerEndTime;

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
    public int maxPlayers;
    public bool unlimitedPlayers;

    public List<LobbyPlayerViewData> players;

    public bool addBots;
    public int botCount;

    #endregion

    #region Constructors

    public LobbyViewData()
    {
        runtimeType = SessionRuntimeType.Local;

        lobbyId = string.Empty;
        playMode = MainMenuPlayMode.None;
        lobbyState = LobbyState.Open;

        isTimerActive = false;
        timerEndTime = 0d;

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
        maxPlayers = LobbySettings.instance != null ? LobbySettings.instance.MinimumPlayers : 6;
        unlimitedPlayers = false;

        players = new List<LobbyPlayerViewData>();

        addBots = false;
        botCount = 0;
    }

    #endregion
}
