using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GameModeManager : MonoBehaviour
{
    #region Singleton / Data

    public static GameModeManager instance;

    [Header("Game Modes")]
    [SerializeField] private List<BingoGameModeData> gameModes = new List<BingoGameModeData>();
    [Header("Rules")]
    [SerializeField] private List<BingoGameRuleData> gameRules = new List<BingoGameRuleData>();

    [Header("Patterns")]
    [SerializeField] private List<BingoPatternData> bingoPatterns = new List<BingoPatternData>();

    private bool isReady;

    public IReadOnlyList<BingoGameModeData> GameModes => gameModes;
    public IReadOnlyList<BingoGameRuleData> GameRules => gameRules;
    public IReadOnlyList<BingoPatternData> BingoPatterns => bingoPatterns;
    public bool IsReady => isReady;

    #endregion

    #region Unity Lifecycle

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
        isReady = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            isReady = false;
            instance = null;
        }
    }

    #endregion

    #region Game Mode Lookup

    public BingoGameModeData GetGameModeData(BingoGameModeType gameModeType)
    {
        for (int i = 0; i < gameModes.Count; i++)
        {
            BingoGameModeData gameModeData = gameModes[i];

            if (gameModeData == null)
            {
                continue;
            }

            if (gameModeData.GameModeType == gameModeType)
            {
                return gameModeData;
            }
        }

        Debug.LogWarning($"GameModeManager could not find game mode data for: {gameModeType}");
        return null;
    }

    public List<BingoGameRuleData> GetGameRuleDataList()
    {
        List<BingoGameRuleData> validRules = new List<BingoGameRuleData>();

        for (int i = 0; i < gameRules.Count; i++)
        {
            if (gameRules[i] != null)
            {
                validRules.Add(gameRules[i]);
            }
        }

        return validRules;
    }

    public BingoGameRuleData GetGameRuleData(BingoRuleType ruleType)
    {
        for (int i = 0; i < gameRules.Count; i++)
        {
            BingoGameRuleData ruleData = gameRules[i];

            if (ruleData == null)
            {
                continue;
            }

            if (ruleData.RuleType == ruleType)
            {
                return ruleData;
            }
        }

        Debug.LogWarning($"GameModeManager could not find rule data for: {ruleType}");
        return null;
    }

    public string GetGameModeName(BingoGameModeType gameModeType)
    {
        BingoGameModeData gameModeData = GetGameModeData(gameModeType);

        if (gameModeData == null)
        {
            return gameModeType.ToString();
        }

        if (string.IsNullOrWhiteSpace(gameModeData.GameName))
        {
            return gameModeType.ToString();
        }

        return gameModeData.GameName;
    }

    public bool TryGetGameRuleData(BingoRuleType ruleType, out BingoGameRuleData ruleData)
    {
        ruleData = GetGameRuleData(ruleType);
        return ruleData != null;
    }

    public bool TryGetGameModeData(BingoGameModeType gameModeType, out BingoGameModeData gameModeData)
    {
        gameModeData = GetGameModeData(gameModeType);
        return gameModeData != null;
    }

    public List<BingoGameModeData> GetGameModeDataList()
    {
        List<BingoGameModeData> validGameModes = new List<BingoGameModeData>();

        for (int i = 0; i < gameModes.Count; i++)
        {
            if (gameModes[i] != null)
            {
                validGameModes.Add(gameModes[i]);
            }
        }

        return validGameModes;
    }

    public bool HasGameMode(BingoGameModeType gameModeType)
    {
        for (int i = 0; i < gameModes.Count; i++)
        {
            BingoGameModeData gameModeData = gameModes[i];

            if (gameModeData == null)
            {
                continue;
            }

            if (gameModeData.GameModeType == gameModeType)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Pattern Lookup

    public List<BingoPatternData> GetBingoPatternDataList()
    {
        List<BingoPatternData> validPatterns = new List<BingoPatternData>();

        for (int i = 0; i < bingoPatterns.Count; i++)
        {
            if (bingoPatterns[i] != null)
            {
                validPatterns.Add(bingoPatterns[i]);
            }
        }

        return validPatterns;
    }

    public BingoPatternData GetBingoPatternData(BingoPatternType patternType)
    {
        for (int i = 0; i < bingoPatterns.Count; i++)
        {
            BingoPatternData patternData = bingoPatterns[i];

            if (patternData == null)
            {
                continue;
            }

            if (patternData.PatternType == patternType)
            {
                return patternData;
            }
        }

        Debug.LogWarning($"GameModeManager could not find pattern data for: {patternType}");
        return null;
    }

    public bool TryGetBingoPatternData(BingoPatternType patternType, out BingoPatternData patternData)
    {
        patternData = GetBingoPatternData(patternType);
        return patternData != null;
    }

    public Color GetBingoPatternHighlightColor(BingoPatternType patternType)
    {
        BingoPatternData patternData = GetBingoPatternData(patternType);

        if (patternData == null)
            return Color.white;

        return patternData.WinningHighlightColor;
    }

    public bool TryGetBingoPatternHighlightColor(BingoPatternType patternType, out Color highlightColor)
    {
        highlightColor = Color.white;

        if (!TryGetBingoPatternData(patternType, out BingoPatternData patternData))
            return false;

        highlightColor = patternData.WinningHighlightColor;
        return true;
    }

    #endregion

    #region Rule Lookup

    public BingoGameModeData GetGameModeDataByRule(BingoRuleType ruleType)
    {
        for (int i = 0; i < gameModes.Count; i++)
        {
            BingoGameModeData gameModeData = gameModes[i];

            if (gameModeData == null || gameModeData.RuleData == null)
            {
                continue;
            }

            if (gameModeData.GameModeType == BingoGameModeType.Custom)
            {
                continue;
            }

            if (gameModeData.RuleData.RuleType == ruleType)
            {
                return gameModeData;
            }
        }

        Debug.LogWarning($"GameModeManager could not find game mode data for rule: {ruleType}");
        return null;
    }

    public bool TryGetGameModeDataByRule(BingoRuleType ruleType, out BingoGameModeData gameModeData)
    {
        gameModeData = GetGameModeDataByRule(ruleType);
        return gameModeData != null;
    }

    #endregion
}