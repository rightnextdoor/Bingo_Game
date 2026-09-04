using System;
using UnityEngine;

[Serializable]
public class SettingsData
{
    public int settingsVersion = 2;

    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float soundVolume = 1f;

    public int resolutionWidth = 0;
    public int resolutionHeight = 0;
    public FullScreenMode screenMode = FullScreenMode.FullScreenWindow;

    public UIThemeType selectedThemeType = UIThemeType.Default;

    public ScorePlayMode leaderboardScorePlayMode = ScorePlayMode.Solo;
    public LeaderboardModeFilter leaderboardGameModeFilter = LeaderboardModeFilter.CreateOverall();
    public LeaderboardPageSizeType leaderboardPageSize = LeaderboardPageSizeType.Show10;

    public SettingsData()
    {
        settingsVersion = 2;

        masterVolume = 1f;
        musicVolume = 1f;
        soundVolume = 1f;

        resolutionWidth = 0;
        resolutionHeight = 0;
        screenMode = FullScreenMode.FullScreenWindow;

        selectedThemeType = UIThemeType.Default;

        leaderboardScorePlayMode = ScorePlayMode.Solo;
        leaderboardGameModeFilter = LeaderboardModeFilter.CreateOverall();
        leaderboardPageSize = LeaderboardPageSizeType.Show10;
    }
}
