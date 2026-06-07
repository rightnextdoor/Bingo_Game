using System;
using System.Collections.Generic;

[Serializable]
public class GameData
{
    public int saveVersion = 1;

    public UserData userData;
    public SettingsData settings = new SettingsData();
    public LeaderboardData leaderboard = new LeaderboardData();

    public GameData()
    {
        saveVersion = 1;
        userData = new UserData();
        settings = new SettingsData();
        leaderboard = new LeaderboardData();
    }
}


[Serializable]
public class SettingsData
{
    public float musicVolume = 1f;
    public float sfxVolume = 1f;
    public bool voiceCalloutEnabled = true;
}

[Serializable]
public class LeaderboardData
{
    public List<LeaderboardEntryData> localEntries = new List<LeaderboardEntryData>();
}

[Serializable]
public class LeaderboardEntryData
{
    public string playerName;
    public int score;
    public string gameMode;
    public string savedAtUtc;
}