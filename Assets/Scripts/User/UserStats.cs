using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class UserStats
{
    public const int DefaultMinimumScore = 0;
    public const int DefaultMaximumScore = 9000000;

    [SerializeField] private List<UserPlayModeScoreMap> scoreMaps = new();

    public IReadOnlyList<UserPlayModeScoreMap> ScoreMaps => scoreMaps;
    public int MinimumScore => GameSettings.instance != null
        ? GameSettings.instance.MinimumScore
        : DefaultMinimumScore;
    public int MaximumScore => GameSettings.instance != null
        ? GameSettings.instance.MaximumScore
        : DefaultMaximumScore;

    public UserStats()
    {
        RepairData();
    }

    public int GetPoints(ScorePlayMode playMode, BingoGameModeType gameModeType)
    {
        UserGameModeScore scoreEntry = GetScoreEntry(playMode, gameModeType, false);
        return scoreEntry != null ? ClampScore(scoreEntry.points) : MinimumScore;
    }

    public int GetOverallPoints(ScorePlayMode playMode)
    {
        long overallPoints = 0;

        foreach (BingoGameModeType gameModeType in GetScoredGameModes())
        {
            overallPoints += GetPoints(playMode, gameModeType);
        }

        return overallPoints > int.MaxValue ? int.MaxValue : (int)overallPoints;
    }

    public int SetPoints(
        ScorePlayMode playMode,
        BingoGameModeType gameModeType,
        int points)
    {
        UserGameModeScore scoreEntry = GetScoreEntry(playMode, gameModeType, true);

        if (scoreEntry == null)
        {
            return MinimumScore;
        }

        scoreEntry.points = ClampScore(points);
        return scoreEntry.points;
    }

    public int AddPoints(
        ScorePlayMode playMode,
        BingoGameModeType gameModeType,
        int amount)
    {
        int currentPoints = GetPoints(playMode, gameModeType);
        long updatedPoints = (long)currentPoints + Mathf.Max(0, amount);
        return SetPoints(playMode, gameModeType, ClampScore(updatedPoints));
    }

    public int RemovePoints(
        ScorePlayMode playMode,
        BingoGameModeType gameModeType,
        int amount)
    {
        int currentPoints = GetPoints(playMode, gameModeType);
        long updatedPoints = (long)currentPoints - Mathf.Max(0, amount);
        return SetPoints(playMode, gameModeType, ClampScore(updatedPoints));
    }

    public void ResetStats()
    {
        RepairData();

        for (int mapIndex = 0; mapIndex < scoreMaps.Count; mapIndex++)
        {
            UserPlayModeScoreMap scoreMap = scoreMaps[mapIndex];

            if (scoreMap?.gameModeScores == null)
            {
                continue;
            }

            for (int scoreIndex = 0; scoreIndex < scoreMap.gameModeScores.Count; scoreIndex++)
            {
                UserGameModeScore scoreEntry = scoreMap.gameModeScores[scoreIndex];

                if (scoreEntry != null)
                {
                    scoreEntry.points = MinimumScore;
                }
            }
        }
    }

    public void RepairData()
    {
        scoreMaps ??= new List<UserPlayModeScoreMap>();

        RemoveDuplicatePlayModeMaps();

        foreach (ScorePlayMode playMode in Enum.GetValues(typeof(ScorePlayMode)))
        {
            UserPlayModeScoreMap scoreMap = GetScoreMap(playMode, true);
            scoreMap.RepairData(this);
        }
    }

    public int ClampScore(long score)
    {
        int minimumScore = MinimumScore;
        int maximumScore = Mathf.Max(minimumScore, MaximumScore);

        if (score <= minimumScore)
        {
            return minimumScore;
        }

        if (score >= maximumScore)
        {
            return maximumScore;
        }

        return (int)score;
    }

    public static bool IsScoredGameMode(BingoGameModeType gameModeType)
    {
        return gameModeType == BingoGameModeType.Traditional ||
               gameModeType == BingoGameModeType.Blackout ||
               gameModeType == BingoGameModeType.Risk ||
               gameModeType == BingoGameModeType.Death;
    }

    public static IEnumerable<BingoGameModeType> GetScoredGameModes()
    {
        yield return BingoGameModeType.Traditional;
        yield return BingoGameModeType.Blackout;
        yield return BingoGameModeType.Risk;
        yield return BingoGameModeType.Death;
    }

    private UserGameModeScore GetScoreEntry(
        ScorePlayMode playMode,
        BingoGameModeType gameModeType,
        bool createIfMissing)
    {
        if (!IsScoredGameMode(gameModeType))
        {
            return null;
        }

        UserPlayModeScoreMap scoreMap = GetScoreMap(playMode, createIfMissing);
        return scoreMap?.GetScoreEntry(gameModeType, createIfMissing, this);
    }

    private UserPlayModeScoreMap GetScoreMap(
        ScorePlayMode playMode,
        bool createIfMissing)
    {
        for (int i = 0; i < scoreMaps.Count; i++)
        {
            if (scoreMaps[i] != null && scoreMaps[i].playMode == playMode)
            {
                return scoreMaps[i];
            }
        }

        if (!createIfMissing)
        {
            return null;
        }

        UserPlayModeScoreMap scoreMap = new(playMode);
        scoreMaps.Add(scoreMap);
        return scoreMap;
    }

    private void RemoveDuplicatePlayModeMaps()
    {
        HashSet<ScorePlayMode> addedPlayModes = new();

        for (int i = scoreMaps.Count - 1; i >= 0; i--)
        {
            UserPlayModeScoreMap scoreMap = scoreMaps[i];

            if (scoreMap == null || !addedPlayModes.Add(scoreMap.playMode))
            {
                scoreMaps.RemoveAt(i);
            }
        }
    }
}

[Serializable]
public class UserPlayModeScoreMap
{
    public ScorePlayMode playMode;
    public List<UserGameModeScore> gameModeScores = new();

    public UserPlayModeScoreMap(ScorePlayMode playMode)
    {
        this.playMode = playMode;
    }

    public void RepairData(UserStats userStats)
    {
        gameModeScores ??= new List<UserGameModeScore>();

        HashSet<BingoGameModeType> addedGameModes = new();

        for (int i = gameModeScores.Count - 1; i >= 0; i--)
        {
            UserGameModeScore scoreEntry = gameModeScores[i];

            if (scoreEntry == null ||
                !UserStats.IsScoredGameMode(scoreEntry.gameModeType) ||
                !addedGameModes.Add(scoreEntry.gameModeType))
            {
                gameModeScores.RemoveAt(i);
                continue;
            }

            scoreEntry.points = userStats.ClampScore(scoreEntry.points);
        }

        foreach (BingoGameModeType gameModeType in UserStats.GetScoredGameModes())
        {
            GetScoreEntry(gameModeType, true, userStats);
        }
    }

    public UserGameModeScore GetScoreEntry(
        BingoGameModeType gameModeType,
        bool createIfMissing,
        UserStats userStats)
    {
        for (int i = 0; i < gameModeScores.Count; i++)
        {
            if (gameModeScores[i] != null && gameModeScores[i].gameModeType == gameModeType)
            {
                return gameModeScores[i];
            }
        }

        if (!createIfMissing)
        {
            return null;
        }

        UserGameModeScore scoreEntry = new(gameModeType, userStats.MinimumScore);
        gameModeScores.Add(scoreEntry);
        return scoreEntry;
    }
}

[Serializable]
public class UserGameModeScore
{
    public BingoGameModeType gameModeType;
    public int points;

    public UserGameModeScore(BingoGameModeType gameModeType, int points)
    {
        this.gameModeType = gameModeType;
        this.points = points;
    }
}
