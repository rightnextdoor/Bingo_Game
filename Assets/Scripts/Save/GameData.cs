using System;

[Serializable]
public class GameData
{
    public int saveVersion = 1;

    public UserDatabaseData userDatabase = new UserDatabaseData();
    public SettingsData settingsData = new SettingsData();
    public ChatSettingsData chatSettingsData = new ChatSettingsData();
    public MenuData menuData = new MenuData();
    public LobbyData lobbyData = new LobbyData();

    public GameData()
    {
        saveVersion = 1;

        userDatabase = new UserDatabaseData();
        settingsData = new SettingsData();
        chatSettingsData = new ChatSettingsData();
        menuData = new MenuData();
        lobbyData = new LobbyData();
    }
}
