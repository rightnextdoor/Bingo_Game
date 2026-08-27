using UnityEngine;

[CreateAssetMenu(fileName = "BingoGameRuleData", menuName = "Bingo Game/Data/Game Rule Data")]
public class BingoGameRuleData : ScriptableObject
{
    [Header("Rule")]
    [SerializeField] private BingoRuleType ruleType;

    [Header("Description")]
    [TextArea(3, 8)]
    [SerializeField] private string description;

    public BingoRuleType RuleType => ruleType;
    public string Description => description;
}