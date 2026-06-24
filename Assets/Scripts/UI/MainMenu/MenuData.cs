using System;

[Serializable]
public class MenuData
{
    public int menuVersion = 1;

    public SoloMenuData soloMenuData = new SoloMenuData();

    public MenuData()
    {
        menuVersion = 1;
        soloMenuData = new SoloMenuData();
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