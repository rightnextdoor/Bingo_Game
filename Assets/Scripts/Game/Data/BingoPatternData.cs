using UnityEngine;

[CreateAssetMenu(fileName = "BingoPatternData", menuName = "Bingo Game/Data/Bingo Pattern Data")]
public class BingoPatternData : ScriptableObject
{
    [Header("Pattern")]
    [SerializeField] private BingoPatternType patternType;

    [Header("Description")]
    [TextArea(3, 8)]
    [SerializeField] private string description;

    public BingoPatternType PatternType => patternType;
    public string Description => description;
}