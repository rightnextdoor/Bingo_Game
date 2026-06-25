using System;

[Serializable]
public class LobbySetupData
{
    public MainMenuPlayMode playMode = MainMenuPlayMode.None;

    public SoloLobbySetupData soloSetupData = new SoloLobbySetupData();
    public OnlineLobbySetupData onlineSetupData = new OnlineLobbySetupData();
    public CustomLobbySetupData customSetupData = new CustomLobbySetupData();

    public LobbySetupData()
    {
        playMode = MainMenuPlayMode.None;

        soloSetupData = new SoloLobbySetupData();
        onlineSetupData = new OnlineLobbySetupData();
        customSetupData = new CustomLobbySetupData();
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

[Serializable]
public class CustomLobbySetupData
{
    public CustomLobbyActionType actionType = CustomLobbyActionType.HostLobby;

    public CustomHostLobbySetupData hostSetupData = new CustomHostLobbySetupData();
    public CustomSearchLobbySetupData searchSetupData = new CustomSearchLobbySetupData();

    public CustomLobbySetupData()
    {
        actionType = CustomLobbyActionType.HostLobby;

        hostSetupData = new CustomHostLobbySetupData();
        searchSetupData = new CustomSearchLobbySetupData();
    }
}

[Serializable]
public class CustomHostLobbySetupData
{
    public string lobbyName = string.Empty;
    public string password = string.Empty;

    public bool unlimitedPlayers = false;
    public int maxPlayers = 6;

    public CustomHostLobbySetupData()
    {
        lobbyName = string.Empty;
        password = string.Empty;

        unlimitedPlayers = false;
        maxPlayers = 6;
    }
}

[Serializable]
public class CustomSearchLobbySetupData
{
    public string lobbyCode = string.Empty;
    public string password = string.Empty;

    public CustomSearchLobbySetupData()
    {
        lobbyCode = string.Empty;
        password = string.Empty;
    }
}