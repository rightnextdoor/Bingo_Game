using System;

[Serializable]
public class MenuData
{
    public int menuVersion = 1;

    public SoloMenuData soloMenuData = new SoloMenuData();
    public OnlineMenuData onlineMenuData = new OnlineMenuData();
    public CustomMenuData customMenuData = new CustomMenuData();

    public MenuData()
    {
        menuVersion = 1;

        soloMenuData = new SoloMenuData();
        onlineMenuData = new OnlineMenuData();
        customMenuData = new CustomMenuData();
    }
}

[Serializable]
public class SoloMenuData
{
    public bool maxPlayers = false;
    public int lobbySize = 6;

    public SoloMenuData()
    {
        maxPlayers = false;
        lobbySize = 6;
    }
}

[Serializable]
public class OnlineMenuData
{
    public BingoGameModeType gameModeType = BingoGameModeType.Traditional;
    public OnlineSearchType searchType = OnlineSearchType.QuickPlay;
    public BingoBallCountType ballCountType = BingoBallCountType.Ball75;

    public OnlineMenuData()
    {
        gameModeType = BingoGameModeType.Traditional;
        searchType = OnlineSearchType.QuickPlay;
        ballCountType = BingoBallCountType.Ball75;
    }
}

[System.Serializable]
public class CustomMenuData
{
    public CustomLobbyActionType actionType = CustomLobbyActionType.HostLobby;

    public CustomMenuData()
    {
        actionType = CustomLobbyActionType.HostLobby;
    }
}