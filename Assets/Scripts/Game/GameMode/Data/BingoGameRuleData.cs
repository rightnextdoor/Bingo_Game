using UnityEngine;

[CreateAssetMenu(fileName = "BingoGameRuleData", menuName = "Bingo Game/Data/Game Rule Data")]
public class BingoGameRuleData : ScriptableObject
{
    [Header("Rule")]
    [SerializeField] private BingoRuleType ruleType;
    [SerializeField] private bool useRank;

    [Header("Description")]
    [TextArea(3, 8)]
    [SerializeField] private string description;

    public BingoRuleType RuleType => ruleType;
    public bool UseRank => useRank;
    public string Description => description;
}
