using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GameScoreManager : MonoBehaviour
{
    public static GameScoreManager instance;

    [SerializeField] private BingoScoreData scoreData;

    private readonly Dictionary<GameScoreType, int> scoreLookup = new();
    private BingoScoreData runtimeScoreData;

    public bool IsReady { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        BuildRuntimeLookup();
    }

    private void OnDestroy()
    {
        if (runtimeScoreData != null)
        {
            Destroy(runtimeScoreData);
            runtimeScoreData = null;
        }

        if (instance == this)
        {
            instance = null;
        }
    }

    public int GetPoints(GameScoreType scoreType)
    {
        return scoreLookup.TryGetValue(scoreType, out int points)
            ? points
            : 0;
    }

    public int GetPatternPoints(BingoPatternType patternType)
    {
        return TryGetScoreType(patternType, out GameScoreType scoreType)
            ? GetPoints(scoreType)
            : 0;
    }

    public int GetLossPoints()
    {
        return GetPoints(GameScoreType.Loss);
    }

    public int GetDeathWinPoints()
    {
        return GetPoints(GameScoreType.DeathWin);
    }

    private void BuildRuntimeLookup()
    {
        IsReady = false;
        scoreLookup.Clear();

        if (runtimeScoreData != null)
        {
            Destroy(runtimeScoreData);
            runtimeScoreData = null;
        }

        if (scoreData == null)
        {
            Debug.LogWarning("GameScoreManager needs BingoScoreData assigned.");
            return;
        }

        runtimeScoreData = scoreData.CreateRuntimeCopy();
        IReadOnlyList<BingoScoreEntry> entries = runtimeScoreData.ScoreEntries;

        for (int i = 0; i < entries.Count; i++)
        {
            BingoScoreEntry entry = entries[i];

            if (entry != null)
            {
                scoreLookup[entry.ScoreType] = entry.Points;
            }
        }

        IsReady = true;
    }

    private static bool TryGetScoreType(
        BingoPatternType patternType,
        out GameScoreType scoreType)
    {
        switch (patternType)
        {
            case BingoPatternType.SingleLine:
                scoreType = GameScoreType.SingleLine;
                return true;

            case BingoPatternType.TwoLines:
                scoreType = GameScoreType.TwoLines;
                return true;

            case BingoPatternType.FourCorners:
                scoreType = GameScoreType.FourCorners;
                return true;

            case BingoPatternType.Cross:
                scoreType = GameScoreType.Cross;
                return true;

            case BingoPatternType.XPattern:
                scoreType = GameScoreType.XPattern;
                return true;

            case BingoPatternType.Diamond:
                scoreType = GameScoreType.Diamond;
                return true;

            case BingoPatternType.Star:
                scoreType = GameScoreType.Star;
                return true;

            case BingoPatternType.Blackout:
                scoreType = GameScoreType.Blackout;
                return true;

            default:
                scoreType = default;
                return false;
        }
    }
}
