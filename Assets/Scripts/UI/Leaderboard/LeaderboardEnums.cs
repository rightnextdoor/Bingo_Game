using System;
using TMPro;

public enum LeaderboardGameModeType
{
    Overall
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
    public UserData userData;
}