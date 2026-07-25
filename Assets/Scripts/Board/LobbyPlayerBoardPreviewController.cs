using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbyPlayerBoardPreviewController : MonoBehaviour
{
    private const int CellCount = 25;
    private const int FreeCellIndex = 12;

    [SerializeField] private List<TMP_Text> cellTexts = new List<TMP_Text>();

    public void DisplayBoard(LobbyBoardData boardData)
    {
        if (boardData == null ||
            boardData.cellNumbers == null ||
            boardData.cellNumbers.Count != CellCount ||
            cellTexts.Count != CellCount)
        {
            ClearBoard();
            return;
        }

        for (int i = 0; i < CellCount; i++)
        {
            TMP_Text cellText = cellTexts[i];

            if (cellText == null)
            {
                continue;
            }

            bool isFreeCell = boardData.usesFreeCell && i == FreeCellIndex;
            cellText.text = isFreeCell ? "FREE" : boardData.cellNumbers[i].ToString();
        }
    }

    public void ClearBoard()
    {
        for (int i = 0; i < cellTexts.Count; i++)
        {
            if (cellTexts[i] != null)
            {
                cellTexts[i].text = string.Empty;
            }
        }
    }
}