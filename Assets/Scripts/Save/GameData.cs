using System;

[Serializable]
public class GameData
{
    public int saveVersion = 1;

    public UserDatabaseData userDatabase = new UserDatabaseData();

    public GameData()
    {
        saveVersion = 1;
        userDatabase = new UserDatabaseData();
    }
}