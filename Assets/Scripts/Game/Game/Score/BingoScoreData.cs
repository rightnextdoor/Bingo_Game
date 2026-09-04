using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Bingo Game/Game/Bingo Score Data", fileName = "NewBingoScoreData")]
public class BingoScoreData : ScriptableObject
{
    [SerializeField] private List<BingoScoreEntry> scoreEntries = new();

    public IReadOnlyList<BingoScoreEntry> ScoreEntries => scoreEntries;

    public BingoScoreData CreateRuntimeCopy()
    {
        BingoScoreData runtimeCopy = CreateInstance<BingoScoreData>();
        runtimeCopy.hideFlags = HideFlags.DontSave;
        runtimeCopy.scoreEntries = new List<BingoScoreEntry>();

        HashSet<GameScoreType> addedScoreTypes = new();

        for (int i = 0; i < scoreEntries.Count; i++)
        {
            BingoScoreEntry entry = scoreEntries[i];

            if (entry == null || !addedScoreTypes.Add(entry.ScoreType))
            {
                continue;
            }

            runtimeCopy.scoreEntries.Add(new BingoScoreEntry(entry.ScoreType, entry.Points));
        }

        return runtimeCopy;
    }
}

[Serializable]
public class BingoScoreEntry
{
    [SerializeField] private GameScoreType scoreType;
    [SerializeField, Min(0)] private int points;

    public GameScoreType ScoreType => scoreType;
    public int Points => Mathf.Max(0, points);

    public BingoScoreEntry(GameScoreType scoreType, int points)
    {
        this.scoreType = scoreType;
        this.points = Mathf.Max(0, points);
    }
}
