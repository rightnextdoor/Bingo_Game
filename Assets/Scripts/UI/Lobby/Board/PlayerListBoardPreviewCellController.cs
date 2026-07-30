using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerListBoardPreviewCellController : MonoBehaviour
{
    #region Fields

    [SerializeField] private TMP_Text valueText;
    [SerializeField] private GameObject markedHighlight;

    private int cellIndex = -1;
    private int number;
    private bool isFree;
    private bool isMarked;

    public int CellIndex => cellIndex;
    public int Number => number;
    public bool IsFree => isFree;
    public bool IsMarked => isMarked;

    #endregion

    #region Display

    private void Awake()
    {
        Clear();
    }

    public void DisplayValue(int index, int value, bool free, bool marked)
    {
        cellIndex = index;
        number = value;
        isFree = free;

        if (valueText != null)
        {
            valueText.text = isFree ? "FREE" : number.ToString();
        }

        SetMarked(marked);
    }

    public void SetMarked(bool marked)
    {
        isMarked = marked;

        if (markedHighlight != null)
        {
            markedHighlight.SetActive(isMarked);
        }
    }

    public void Clear()
    {
        cellIndex = -1;
        number = 0;
        isFree = false;
        isMarked = false;

        if (valueText != null)
        {
            valueText.text = string.Empty;
        }

        if (markedHighlight != null)
        {
            markedHighlight.SetActive(false);
        }
    }

    #endregion
}