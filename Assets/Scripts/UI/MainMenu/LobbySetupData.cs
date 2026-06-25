using System;

[Serializable]
public class LobbySetupData
{
    public MainMenuPlayMode playMode = MainMenuPlayMode.None;

    public SoloLobbySetupData soloSetupData = new SoloLobbySetupData();
    public OnlineLobbySetupData onlineSetupData = new OnlineLobbySetupData();

    public LobbySetupData()
    {
        playMode = MainMenuPlayMode.None;

        soloSetupData = new SoloLobbySetupData();
        onlineSetupData = new OnlineLobbySetupData();
    }
}

[Serializable]
public class SoloLobbySetupData
{
    public bool unlimitedPlayers = false;
    public int maxPlayers = 6;

    public SoloLobbySetupData()
    {
        unlimitedPlayers = false;
        maxPlayers = 6;
    }
}

[Serializable]
public class OnlineLobbySetupData
{
    public BingoGameModeType gameModeType = BingoGameModeType.Traditional;
    public OnlineSearchType searchType = OnlineSearchType.QuickPlay;
    public BingoBallCountType ballCountType = BingoBallCountType.Ball75;

    public OnlineLobbySetupData()
    {
        gameModeType = BingoGameModeType.Traditional;
        searchType = OnlineSearchType.QuickPlay;
        ballCountType = BingoBallCountType.Ball75;
    }
}