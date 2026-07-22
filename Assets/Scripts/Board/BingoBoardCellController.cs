using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class BingoBoardCellController : MonoBehaviour
{
    #region Fields

    private const float NumberFontSize = 50f;
    private const float FreeFontSize = 27f;

    [SerializeField] private TMP_Text valueText;

    private int cellIndex;
    private int number;
    private bool isFree;

    public int CellIndex => cellIndex;
    public int Number => number;
    public bool IsFree => isFree;

    #endregion

    #region Display

    public void DisplayValue(int index, int value, bool free)
    {
        cellIndex = index;
        number = value;
        isFree = free;

        if (valueText == null)
        {
            return;
        }

        valueText.enableAutoSizing = false;
        valueText.fontSize = isFree ? FreeFontSize : NumberFontSize;
        valueText.text = isFree ? "FREE" : number.ToString();
    }

    public void Clear()
    {
        cellIndex = -1;
        number = 0;
        isFree = false;

        if (valueText == null)
        {
            return;
        }

        valueText.fontSize = NumberFontSize;
        valueText.text = string.Empty;
    }

    #endregion
}