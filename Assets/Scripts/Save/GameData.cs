using System;

[Serializable]
public class GameData
{
    public int saveVersion = 1;

    public UserDatabaseData userDatabase = new UserDatabaseData();
    public SettingsData settingsData = new SettingsData();

    public GameData()
    {
        saveVersion = 1;

        userDatabase = new UserDatabaseData();
        settingsData = new SettingsData();
    }
}