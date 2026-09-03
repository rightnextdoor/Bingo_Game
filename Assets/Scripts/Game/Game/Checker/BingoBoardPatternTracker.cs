using System;
using System.Collections.Generic;

[Serializable]
public class BingoBoardPatternTracker
{
    private readonly List<BingoPatternType> availablePatternTypes =
        new List<BingoPatternType>();

    public IReadOnlyList<BingoPatternType> AvailablePatternTypes => availablePatternTypes;

    public void Setup(IReadOnlyCollection<BingoPatternType> configuredPatternTypes)
    {
        availablePatternTypes.Clear();

        if (configuredPatternTypes == null)
        {
            return;
        }

        foreach (BingoPatternType patternType in configuredPatternTypes)
        {
            if (!availablePatternTypes.Contains(patternType))
            {
                availablePatternTypes.Add(patternType);
            }
        }
    }

    public void ApplyAvailablePatterns(IReadOnlyCollection<BingoPatternType> patternTypes)
    {
        Setup(patternTypes);
    }

    public bool HasAvailablePattern(BingoPatternType patternType)
    {
        return availablePatternTypes.Contains(patternType);
    }

    public void Clear()
    {
        availablePatternTypes.Clear();
    }
}
