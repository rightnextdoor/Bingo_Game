using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MainMenuGameInfoController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Game Info")]
    [SerializeField] private GameInfoController gameInfoController;

    [Header("Manual Messages")]
    [TextArea(3, 8)]
    [SerializeField] private string soloInfoMessage;

    [TextArea(3, 8)]
    [SerializeField] private string customInfoMessage;

    #endregion

    #region Private Fields

    private BingoBallCountType currentOnlineBallCountType = BingoBallCountType.Ball75;
    private readonly List<BingoPatternType> onlinePatternTypes = new List<BingoPatternType>();

    #endregion

    #region Public Methods

    public void ShowSoloInfo()
    {
        gameInfoController?.ShowBasicInfo("Solo", soloInfoMessage);
    }

    public void ShowCustomInfo()
    {
        gameInfoController?.ShowBasicInfo("Custom", customInfoMessage);
    }

    public void ShowOnlineInfo(BingoGameModeType gameModeType)
    {
        ShowOnlineInfo(gameModeType, currentOnlineBallCountType);
    }

    public void ShowOnlineInfo(BingoGameModeType gameModeType, BingoBallCountType ballCountType)
    {
        currentOnlineBallCountType = ballCountType;
        onlinePatternTypes.Clear();

        GameModeManager gameModeManager = GameModeManager.instance;

        if (gameModeManager == null)
        {
            ShowFallbackOnlineInfo(gameModeType, ballCountType);
            return;
        }

        BingoGameModeData gameModeData = gameModeManager.GetGameModeData(gameModeType);

        if (gameModeData == null)
        {
            ShowFallbackOnlineInfo(gameModeType, ballCountType);
            return;
        }

        bool hasRule = gameModeData.RuleData != null;
        string ruleDescription = hasRule ? GetRuleDescription(gameModeData.RuleData) : string.Empty;

        BuildDefaultPatternTypes(gameModeData);

        gameInfoController?.ShowGameInfo(
            GetGameName(gameModeData, gameModeType),
            gameModeData.Description,
            ballCountType,
            hasRule,
            ruleDescription,
            onlinePatternTypes);
    }

    public void ClearInfo()
    {
        gameInfoController?.ClearInfo();
    }

    #endregion

    #region Online Info

    private void ShowFallbackOnlineInfo(BingoGameModeType gameModeType, BingoBallCountType ballCountType)
    {
        gameInfoController?.ShowGameInfo(
            gameModeType.ToString(),
            "No game information is available for this game mode.",
            ballCountType,
            false,
            string.Empty,
            null);
    }

    private void BuildDefaultPatternTypes(BingoGameModeData gameModeData)
    {
        onlinePatternTypes.Clear();

        if (gameModeData == null)
        {
            return;
        }

        List<BingoPatternData> patterns = gameModeData.GetAllPatterns();

        for (int i = 0; i < patterns.Count; i++)
        {
            BingoPatternData patternData = patterns[i];

            if (patternData == null || onlinePatternTypes.Contains(patternData.PatternType))
            {
                continue;
            }

            onlinePatternTypes.Add(patternData.PatternType);
        }
    }

    private string GetGameName(BingoGameModeData gameModeData, BingoGameModeType fallbackType)
    {
        if (gameModeData == null || string.IsNullOrWhiteSpace(gameModeData.GameName))
        {
            return fallbackType.ToString();
        }

        return gameModeData.GameName;
    }

    private string GetRuleDescription(BingoGameRuleData ruleData)
    {
        if (ruleData == null || string.IsNullOrWhiteSpace(ruleData.Description))
        {
            return "No rule description is available for this game mode.";
        }

        return ruleData.Description;
    }

    #endregion
}