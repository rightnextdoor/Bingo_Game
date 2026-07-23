using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BingoBoardCellController : MonoBehaviour
{
    #region Fields

    private const float NumberFontSize = 50f;
    private const float FreeFontSize = 27f;

    [SerializeField] private Button cellButton;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Image markedHighlight;
    [SerializeField] private Image winningHighlight;

    private int cellIndex = -1;
    private int number;
    private bool isFree;
    private bool isMarked;
    private bool isCheckHighlighted;

    public int CellIndex => cellIndex;
    public int Number => number;
    public bool IsFree => isFree;
    public bool IsMarked => isMarked;

    public event Action<int> CellPressed;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (cellButton != null)
            cellButton.onClick.AddListener(OnCellClicked);
    }

    private void OnDisable()
    {
        if (cellButton != null)
            cellButton.onClick.RemoveListener(OnCellClicked);
    }

    #endregion

    #region Display

    public void DisplayValue(int index, int value, bool free)
    {
        cellIndex = index;
        number = value;
        isFree = free;
        isMarked = isFree;
        isCheckHighlighted = false;

        if (cellButton != null)
            cellButton.interactable = !isFree;

        if (valueText != null)
        {
            valueText.enableAutoSizing = false;
            valueText.fontSize = isFree ? FreeFontSize : NumberFontSize;
            valueText.text = isFree ? "FREE" : number.ToString();
        }

        RefreshHighlights();
    }

    public void Clear()
    {
        cellIndex = -1;
        number = 0;
        isFree = false;
        isMarked = false;
        isCheckHighlighted = false;

        if (cellButton != null)
            cellButton.interactable = false;

        if (valueText != null)
        {
            valueText.fontSize = NumberFontSize;
            valueText.text = string.Empty;
        }

        RefreshHighlights();
    }

    #endregion

    #region Marked State

    public void SetMarked(bool marked)
    {
        isMarked = isFree || marked;
        RefreshHighlights();
    }

    #endregion

    #region Check Highlight

    public void ShowCheckHighlight(Color color)
    {
        if (winningHighlight != null)
            winningHighlight.color = color;

        isCheckHighlighted = true;
        RefreshHighlights();
    }

    public void ClearCheckHighlight()
    {
        isCheckHighlighted = false;
        RefreshHighlights();
    }

    #endregion

    #region Input

    private void OnCellClicked()
    {
        if (isFree || cellIndex < 0)
            return;

        CellPressed?.Invoke(cellIndex);
    }

    #endregion

    #region Highlights

    private void RefreshHighlights()
    {
        if (markedHighlight != null)
            markedHighlight.gameObject.SetActive(isMarked && !isCheckHighlighted);

        if (winningHighlight != null)
            winningHighlight.gameObject.SetActive(isCheckHighlighted);
    }

    #endregion
}