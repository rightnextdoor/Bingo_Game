using System;
using System.Collections.Generic;

[Serializable]
public class LobbyViewData
{
    public SessionRuntimeType runtimeType;

    public string lobbyId;
    public MainMenuPlayMode playMode;
    public LobbyState lobbyState;

    public bool isTimerActive;
    public double timerEndTime;

    public string lobbyName;
    public string roomCode;
    public bool hasPassword;

    public BingoGameModeType gameModeType;
    public string gameModeName;

    public bool hasRule;
    public BingoRuleType ruleType;

    public List<BingoPatternType> patternTypes;

    public BingoBallCountType ballCountType;

    public int playerCount;
    public int maxPlayers;

    public List<string> playerNames;

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

        gameModeType = BingoGameModeType.Traditional;
        gameModeName = string.Empty;

        hasRule = false;
        ruleType = BingoRuleType.Traditional;

        patternTypes = new List<BingoPatternType>();

        ballCountType = BingoBallCountType.Ball75;

        playerCount = 0;
        maxPlayers = 1;

        playerNames = new List<string>();
    }
}
