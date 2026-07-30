using System;
using System.Collections.Generic;

[Serializable]
public class LobbyData
{
    public int lobbyVersion = 1;

    public SoloLobbyData soloLobbyData = new SoloLobbyData();
    public CustomLobbyData customLobbyData = new CustomLobbyData();

    public LobbyData()
    {
        lobbyVersion = 1;
        soloLobbyData = new SoloLobbyData();
        customLobbyData = new CustomLobbyData();
    }
}

[Serializable]
public class SoloLobbyData
{
    public BingoGameModeType gameModeType = BingoGameModeType.Traditional;
    public BingoBallCountType ballCountType = BingoBallCountType.Ball75;
    public bool useFreeCell = true;
    public bool usesDefaultPatterns = true;
    public List<BingoPatternType> patternTypes = new List<BingoPatternType>();

    public SoloLobbyData()
    {
        gameModeType = BingoGameModeType.Traditional;
        ballCountType = BingoBallCountType.Ball75;
        useFreeCell = true;
        usesDefaultPatterns = true;
        patternTypes = new List<BingoPatternType>();
    }
}

[Serializable]
public class CustomLobbyData
{
    public BingoGameModeType gameModeType = BingoGameModeType.Traditional;
    public BingoBallCountType ballCountType = BingoBallCountType.Ball75;
    public bool useFreeCell = true;
    public bool usesDefaultPatterns = true;
    public List<BingoPatternType> patternTypes = new List<BingoPatternType>();

    public CustomLobbyData()
    {
        gameModeType = BingoGameModeType.Traditional;
        ballCountType = BingoBallCountType.Ball75;
        useFreeCell = true;
        usesDefaultPatterns = true;
        patternTypes = new List<BingoPatternType>();
    }
}
