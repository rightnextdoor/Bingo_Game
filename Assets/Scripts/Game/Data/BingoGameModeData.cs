using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BingoGameModeData", menuName = "Bingo Game/Data/Game Mode Data")]
public class BingoGameModeData : ScriptableObject
{
    [Header("Mode")]
    [SerializeField] private string gameName;
    [SerializeField] private BingoGameModeType gameModeType;

    [TextArea(3, 8)]
    [SerializeField] private string description;

    [Header("Rule")]
    [SerializeField] private BingoGameRuleData ruleData;

    [Header("Patterns")]
    [SerializeField] private BingoPatternData defaultPattern;
    [SerializeField] private List<BingoPatternData> specialPatterns = new List<BingoPatternData>();

    [Header("Winners")]
    [SerializeField] private bool supportsMultipleWinners;

    public string GameName => gameName;
    public BingoGameModeType GameModeType => gameModeType;
    public string Description => description;
    public BingoGameRuleData RuleData => ruleData;
    public BingoPatternData DefaultPattern => defaultPattern;
    public IReadOnlyList<BingoPatternData> SpecialPatterns => specialPatterns;
    public bool SupportsMultipleWinners => supportsMultipleWinners;

    public List<BingoPatternData> GetAllPatterns()
    {
        List<BingoPatternData> allPatterns = new List<BingoPatternData>();

        if (defaultPattern != null)
        {
            allPatterns.Add(defaultPattern);
        }

        foreach (BingoPatternData pattern in specialPatterns)
        {
            if (pattern != null && !allPatterns.Contains(pattern))
            {
                allPatterns.Add(pattern);
            }
        }

        return allPatterns;
    }
}