using System;

[Serializable]
public class LobbySetupData
{
    public MainMenuPlayMode playMode = MainMenuPlayMode.None;

    public SoloLobbySetupData soloSetupData = new SoloLobbySetupData();

    public LobbySetupData()
    {
        playMode = MainMenuPlayMode.None;
        soloSetupData = new SoloLobbySetupData();
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