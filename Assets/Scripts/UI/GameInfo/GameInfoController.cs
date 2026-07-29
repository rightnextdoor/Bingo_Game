using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class GameInfoController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Sections")]
    [SerializeField] private GameObject gameInfoSection;
    [SerializeField] private GameObject ballCountSection;
    [SerializeField] private GameObject rulesSection;
    [SerializeField] private GameObject patternsSection;

    [Header("Game Info")]
    [SerializeField] private TMP_Text gameInfoHeaderText;
    [SerializeField] private TMP_Text gameNameText;
    [SerializeField] private TMP_Text gameDescriptionText;

    [Header("Ball Count")]
    [SerializeField] private TMP_Text ballCountHeaderText;
    [SerializeField] private TMP_Text ballCountDescriptionText;

    [Header("Rules")]
    [SerializeField] private TMP_Text rulesHeaderText;
    [SerializeField] private TMP_Text rulesDescriptionText;

    [Header("Patterns")]
    [SerializeField] private TMP_Text patternsHeaderText;
    [SerializeField] private Transform patternContent;
    [SerializeField] private MainMenuPatternInfoItem patternItemPrefab;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        ClearInfo();
    }

    #endregion

    #region Public Methods

    public void ShowBasicInfo(string title, string description)
    {
        ClearPatternItems();

        SetActive(gameInfoSection, true);
        SetActive(ballCountSection, false);
        SetActive(rulesSection, false);
        SetActive(patternsSection, false);

        SetText(gameInfoHeaderText, "Game Info");
        SetText(gameNameText, title);
        SetText(gameDescriptionText, description);

        ClearBallCountText();
        ClearRulesText();
        ClearPatternsText();
    }

    public void ShowGameInfo(
        string gameName,
        string gameDescription,
        BingoBallCountType ballCountType,
        bool hasRule,
        string ruleDescription,
        IReadOnlyList<BingoPatternType> patternTypes)
    {
        ClearPatternItems();

        SetActive(gameInfoSection, true);
        SetActive(ballCountSection, true);
        SetActive(rulesSection, hasRule);

        bool hasPatterns = patternTypes != null && patternTypes.Count > 0;
        SetActive(patternsSection, hasPatterns);

        SetText(gameInfoHeaderText, "Game Info");
        SetText(gameNameText, gameName);
        SetText(gameDescriptionText, gameDescription);

        SetText(ballCountHeaderText, "Ball Count");
        SetText(ballCountDescriptionText, ((int)ballCountType).ToString());

        if (hasRule)
        {
            SetText(rulesHeaderText, "Rules");
            SetText(rulesDescriptionText, ruleDescription);
        }
        else
        {
            ClearRulesText();
        }

        if (hasPatterns)
        {
            SetText(patternsHeaderText, "Patterns");
            BuildPatternList(patternTypes);
        }
        else
        {
            ClearPatternsText();
        }
    }

    public void ClearInfo()
    {
        ClearPatternItems();

        SetActive(gameInfoSection, false);
        SetActive(ballCountSection, false);
        SetActive(rulesSection, false);
        SetActive(patternsSection, false);

        SetText(gameInfoHeaderText, string.Empty);
        SetText(gameNameText, string.Empty);
        SetText(gameDescriptionText, string.Empty);

        ClearBallCountText();
        ClearRulesText();
        ClearPatternsText();
    }

    #endregion

    #region Patterns

    private void BuildPatternList(IReadOnlyList<BingoPatternType> patternTypes)
    {
        if (patternTypes == null || patternContent == null || patternItemPrefab == null)
        {
            return;
        }

        HashSet<BingoPatternType> addedPatterns = new HashSet<BingoPatternType>();

        for (int i = 0; i < patternTypes.Count; i++)
        {
            BingoPatternType patternType = patternTypes[i];

            if (!addedPatterns.Add(patternType))
            {
                continue;
            }

            MainMenuPatternInfoItem patternItem = Instantiate(patternItemPrefab, patternContent);
            patternItem.Setup(patternType);
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

    #region Clear Helpers

    private void ClearBallCountText()
    {
        SetText(ballCountHeaderText, string.Empty);
        SetText(ballCountDescriptionText, string.Empty);
    }

    private void ClearRulesText()
    {
        SetText(rulesHeaderText, string.Empty);
        SetText(rulesDescriptionText, string.Empty);
    }

    private void ClearPatternsText()
    {
        SetText(patternsHeaderText, string.Empty);
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