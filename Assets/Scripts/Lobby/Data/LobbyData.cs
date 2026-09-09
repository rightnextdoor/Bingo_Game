using System;
using System.Collections.Generic;

[Serializable]
public class LobbyData
{
    public int lobbyVersion = 2;

    public SoloLobbyData soloLobbyData = new SoloLobbyData();
    public CustomLobbyData customLobbyData = new CustomLobbyData();

    public LobbyData()
    {
        lobbyVersion = 2;
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
    public bool hasRiskMatchDurationOverride;
    public float riskMatchDurationMinutes = GameSettings.DefaultRiskMatchDurationMinutes;
    public bool usesDefaultPatterns = true;
    public List<BingoPatternType> patternTypes = new List<BingoPatternType>();

    public SoloLobbyData()
    {
        gameModeType = BingoGameModeType.Traditional;
        ballCountType = BingoBallCountType.Ball75;
        useFreeCell = true;
        hasRiskMatchDurationOverride = false;
        riskMatchDurationMinutes = GameSettings.DefaultRiskMatchDurationMinutes;
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
    public bool hasRiskMatchDurationOverride;
    public float riskMatchDurationMinutes = GameSettings.DefaultRiskMatchDurationMinutes;
    public bool usesDefaultPatterns = true;
    public List<BingoPatternType> patternTypes = new List<BingoPatternType>();

    public CustomLobbyData()
    {
        gameModeType = BingoGameModeType.Traditional;
        ballCountType = BingoBallCountType.Ball75;
        useFreeCell = true;
        hasRiskMatchDurationOverride = false;
        riskMatchDurationMinutes = GameSettings.DefaultRiskMatchDurationMinutes;
        usesDefaultPatterns = true;
        patternTypes = new List<BingoPatternType>();
    }
}
