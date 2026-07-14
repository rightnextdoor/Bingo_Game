using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MainMenuGameInfoController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Manual Messages")]
    [TextArea(3, 8)]
    [SerializeField] private string soloInfoMessage;

    [TextArea(3, 8)]
    [SerializeField] private string customInfoMessage;

    [Header("Sections")]
    [SerializeField] private GameObject gameInfoSection;
    [SerializeField] private GameObject rulesSection;
    [SerializeField] private GameObject patternsSection;

    [Header("Game Info Text")]
    [SerializeField] private TMP_Text gameInfoHeaderText;
    [SerializeField] private TMP_Text gameNameText;
    [SerializeField] private TMP_Text gameDescriptionText;

    [Header("Rules Text")]
    [SerializeField] private TMP_Text rulesHeaderText;
    [SerializeField] private TMP_Text rulesDescriptionText;

    [Header("Patterns")]
    [SerializeField] private TMP_Text patternsHeaderText;
    [SerializeField] private Transform patternContent;
    [SerializeField] private MainMenuPatternInfoItem patternItemPrefab;

    #endregion

    private void Awake()
    {
        ClearPatternItems();
    }

    #region Public Methods

    public void ShowSoloInfo()
    {
        ShowManualInfo("Game Info", "Solo", soloInfoMessage);
    }

    public void ShowCustomInfo()
    {
        ShowManualInfo("Game Info", "Custom", customInfoMessage);
    }

    public void ShowOnlineInfo(BingoGameModeType gameModeType)
    {
        ClearPatternItems();

        if (GameModeManager.instance == null)
        {
            Debug.LogWarning("MainMenuGameInfoController could not show online info because GameModeManager was not found.");
            ShowFallbackOnlineInfo(gameModeType);
            return;
        }

        BingoGameModeData gameModeData = GameModeManager.instance.GetGameModeData(gameModeType);

        if (gameModeData == null)
        {
            ShowFallbackOnlineInfo(gameModeType);
            return;
        }

        SetActive(gameInfoSection, true);
        SetActive(rulesSection, true);
        SetActive(patternsSection, true);

        SetText(gameInfoHeaderText, "Game Info");
        SetText(gameNameText, GetGameName(gameModeData, gameModeType));
        SetText(gameDescriptionText, gameModeData.Description);

        SetText(rulesHeaderText, "Rules");

        SetText(patternsHeaderText, "Patterns");

        if (gameModeData.RuleData != null)
        {
            SetText(rulesDescriptionText, gameModeData.RuleData.Description);
        }
        else
        {
            SetText(rulesDescriptionText, "No rule description is available for this game mode.");
        }

        BuildPatternList(gameModeData);

    }

    public void ClearInfo()
    {
        ClearPatternItems();

        SetText(gameInfoHeaderText, string.Empty);
        SetText(gameNameText, string.Empty);
        SetText(gameDescriptionText, string.Empty);
        SetText(rulesHeaderText, string.Empty);
        SetText(rulesDescriptionText, string.Empty);
        SetText(patternsHeaderText, string.Empty);

        SetActive(gameInfoSection, false);
        SetActive(rulesSection, false);
        SetActive(patternsSection, false);
    }

    #endregion

    #region Manual Info

    private void ShowManualInfo(string header, string title, string message)
    {
        ClearPatternItems();

        SetActive(gameInfoSection, true);
        SetActive(rulesSection, false);
        SetActive(patternsSection, false);

        SetText(gameInfoHeaderText, header);
        SetText(gameNameText, title);
        SetText(gameDescriptionText, message);

        SetText(rulesHeaderText, string.Empty);
        SetText(rulesDescriptionText, string.Empty);
        SetText(patternsHeaderText, string.Empty);
    }

    #endregion

    #region Online Info

    private void ShowFallbackOnlineInfo(BingoGameModeType gameModeType)
    {
        ClearPatternItems();

        SetActive(gameInfoSection, true);
        SetActive(rulesSection, false);
        SetActive(patternsSection, false);

        SetText(gameInfoHeaderText, "Game Info");
        SetText(gameNameText, gameModeType.ToString());
        SetText(gameDescriptionText, "No game information is available for this game mode.");

        SetText(rulesHeaderText, string.Empty);
        SetText(rulesDescriptionText, string.Empty);
        SetText(patternsHeaderText, string.Empty);
    }

    private string GetGameName(BingoGameModeData gameModeData, BingoGameModeType fallbackType)
    {
        if (gameModeData == null)
        {
            return fallbackType.ToString();
        }

        if (string.IsNullOrWhiteSpace(gameModeData.GameName))
        {
            return fallbackType.ToString();
        }

        return gameModeData.GameName;
    }

    private void BuildPatternList(BingoGameModeData gameModeData)
    {
        if (gameModeData == null || patternContent == null || patternItemPrefab == null)
        {
            return;
        }

        List<BingoPatternData> patterns = gameModeData.GetAllPatterns();

        for (int i = 0; i < patterns.Count; i++)
        {
            BingoPatternData patternData = patterns[i];

            if (patternData == null)
            {
                continue;
            }

            MainMenuPatternInfoItem patternItem = Instantiate(patternItemPrefab, patternContent);
            patternItem.Setup(patternData.PatternType);

        }
    }

    private void ClearPatternItems()
    {
        if (ToolTipManager.instance != null)
        {
            ToolTipManager.instance.HideToolTip();
        }

        if (patternContent == null)
        {
            return;
        }

        for (int i = patternContent.childCount - 1; i >= 0; i--)
        {
            Transform child = patternContent.GetChild(i);

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    #endregion

    #region Helpers

    private void SetText(TMP_Text targetText, string value)
    {
        if (targetText != null)
        {
            targetText.text = value;
        }
    }

    private void SetActive(GameObject target, bool isActive)
    {
        if (target != null)
        {
            target.SetActive(isActive);
        }
    }

    #endregion
}