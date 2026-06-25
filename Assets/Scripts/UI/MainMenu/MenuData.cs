using System;

[Serializable]
public class MenuData
{
    public int menuVersion = 1;

    public SoloMenuData soloMenuData = new SoloMenuData();
    public OnlineMenuData onlineMenuData = new OnlineMenuData();

    public MenuData()
    {
        menuVersion = 1;

        soloMenuData = new SoloMenuData();
        onlineMenuData = new OnlineMenuData();
    }
}

[Serializable]
public class SoloMenuData
{
    public bool unlimitedPlayers = false;
    public int lobbySize = 6;

    public SoloMenuData()
    {
        unlimitedPlayers = false;
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