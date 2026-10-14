using System.Collections.Generic;
using UnityEngine;

public static class BingoBoardGenerator
{
    #region Constants

    private const int ColumnCount = BingoNumberRangeUtility.ColumnCount;
    private const int RowCount = 5;
    private const int CellCount = ColumnCount * RowCount;
    private const int FreeCellIndex = 12;

    #endregion

    #region Generate

    public static LobbyBoardData Generate(BingoBallCountType ballCountType, bool useFreeCell)
    {
        int totalBallCount = (int)ballCountType;
        List<int> cellNumbers = new List<int>(new int[CellCount]);

        for (int column = 0; column < ColumnCount; column++)
        {
            GenerateColumn(cellNumbers, column, totalBallCount, useFreeCell);
        }

        if (useFreeCell)
        {
            cellNumbers[FreeCellIndex] = 0;
        }

        return new LobbyBoardData(ballCountType, useFreeCell, cellNumbers);
    }

    private static void GenerateColumn(List<int> cellNumbers, int column, int totalBallCount, bool useFreeCell)
    {
        BingoNumberRangeUtility.GetColumnRange(
            column,
            totalBallCount,
            out int minimum,
            out int maximum);

        List<int> availableNumbers = BuildNumberPool(minimum, maximum);

        for (int row = 0; row < RowCount; row++)
        {
            int cellIndex = row * ColumnCount + column;

            if (useFreeCell && cellIndex == FreeCellIndex)
            {
                continue;
            }

            cellNumbers[cellIndex] = TakeRandomNumber(availableNumbers);
        }
    }

    #endregion

    #region Number Range

    private static List<int> BuildNumberPool(int minimum, int maximum)
    {
        List<int> numbers = new List<int>();

        for (int number = minimum; number <= maximum; number++)
        {
            numbers.Add(number);
        }

        return numbers;
    }

    private static int TakeRandomNumber(List<int> availableNumbers)
    {
        int randomIndex = Random.Range(0, availableNumbers.Count);
        int number = availableNumbers[randomIndex];

        availableNumbers.RemoveAt(randomIndex);

        return number;
    }

    #endregion
}
