using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LobbyHostPatternToggleItem : MonoBehaviour
{
    #region Fields

    [SerializeField] private Toggle toggle;
    [SerializeField] private TMP_Text patternNameText;

    private BingoPatternType patternType;

    public BingoPatternType PatternType => patternType;
    public bool IsOn => toggle != null && toggle.isOn;

    public event Action<LobbyHostPatternToggleItem, bool> ValueChanged;

    #endregion

    #region Unity Methods

    private void OnEnable()
    {
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }
    }

    private void OnDisable()
    {
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }
    }

    #endregion

    #region Setup

    public void Setup(BingoPatternData patternData)
    {
        if (patternData == null)
        {
            return;
        }

        patternType = patternData.PatternType;

        if (patternNameText != null)
        {
            patternNameText.text = GetPatternLabel(patternType);
        }

        SetIsOnWithoutNotify(false);
        SetInteractable(true);
    }

    public void SetIsOnWithoutNotify(bool isOn)
    {
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(isOn);
        }
    }

    public void SetInteractable(bool interactable)
    {
        if (toggle != null)
        {
            toggle.interactable = interactable;
        }
    }

    #endregion

    #region Events

    private void OnToggleValueChanged(bool isOn)
    {
        ValueChanged?.Invoke(this, isOn);
    }

    #endregion

    #region Helpers

    private string GetPatternLabel(BingoPatternType type)
    {
        switch (type)
        {
            case BingoPatternType.SingleLine:
                return "Single Line";

            case BingoPatternType.TwoLines:
                return "Two Lines";

            case BingoPatternType.FourCorners:
                return "Four Corners";

            case BingoPatternType.XPattern:
                return "X Pattern";

            default:
                return type.ToString();
        }
    }

    #endregion
}
