using System;
using TMPro;

public enum LeaderboardModeFilterType
{
    Overall,
    GameMode
}

[Serializable]
public struct LeaderboardModeFilter
{
    public LeaderboardModeFilterType filterType;
    public BingoGameModeType gameModeType;

    public bool IsOverall => filterType == LeaderboardModeFilterType.Overall;
    public bool IsGameMode => filterType == LeaderboardModeFilterType.GameMode;

    public static LeaderboardModeFilter CreateOverall()
    {
        return new LeaderboardModeFilter
        {
            filterType = LeaderboardModeFilterType.Overall,
            gameModeType = default
        };
    }

    public static LeaderboardModeFilter CreateGameMode(BingoGameModeType gameModeType)
    {
        return new LeaderboardModeFilter
        {
            filterType = LeaderboardModeFilterType.GameMode,
            gameModeType = gameModeType
        };
    }
}

public enum LeaderboardPageSizeType
{
    Show10,
    Show25,
    Show50,
    Show100
}

public enum LeaderboardCellDisplayType
{
    Text,
    Image,
    TextAndImage
}

public enum LeaderboardRowCellValueType
{
    Rank,
    UserIcon,
    PlayerNameWithShortId,
    Score
}

public enum LeaderboardSortType
{
    ScoreHighest
}

[Serializable]
public class LeaderboardRowCellSetup
{
    public LeaderboardCellDisplayType displayType;
    public LeaderboardRowCellValueType valueType;
    public UIThemeTextType textType;
    public float preferredWidth;
    public float flexibleWidth;
    public float fontSize;
    public TextAlignmentOptions alignment;
    public int maxTextCharacters;
    public int maxNumberDigits;
}

[Serializable]
public class LeaderboardUserRankData
{
    public int rank;
    public int score;
    public UserData userData;
}
