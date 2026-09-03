using System;

public static class BingoNumberRangeUtility
{
    public const int ColumnCount = 5;

    private const string ColumnLetters = "BINGO";

    public static void GetColumnRange(
        int columnIndex,
        BingoBallCountType ballCountType,
        out int minimum,
        out int maximum)
    {
        GetColumnRange(columnIndex, (int)ballCountType, out minimum, out maximum);
    }

    public static void GetColumnRange(
        int columnIndex,
        int totalBallCount,
        out int minimum,
        out int maximum)
    {
        if (columnIndex < 0 || columnIndex >= ColumnCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(columnIndex),
                columnIndex,
                $"A Bingo column index must be between 0 and {ColumnCount - 1}.");
        }

        if (totalBallCount < ColumnCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalBallCount),
                totalBallCount,
                $"A Bingo ball count must contain at least {ColumnCount} numbers.");
        }

        int baseColumnSize = totalBallCount / ColumnCount;
        int remainder = totalBallCount % ColumnCount;
        int columnSize = baseColumnSize + (columnIndex < remainder ? 1 : 0);
        int previousExtraValues = Math.Min(columnIndex, remainder);

        minimum = 1 + columnIndex * baseColumnSize + previousExtraValues;
        maximum = minimum + columnSize - 1;
    }

    public static int GetColumnIndex(int number, BingoBallCountType ballCountType)
    {
        return GetColumnIndex(number, (int)ballCountType);
    }

    public static int GetColumnIndex(int number, int totalBallCount)
    {
        if (number < 1 || number > totalBallCount || totalBallCount < ColumnCount)
        {
            return -1;
        }

        for (int columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
        {
            GetColumnRange(columnIndex, totalBallCount, out int minimum, out int maximum);

            if (number >= minimum && number <= maximum)
            {
                return columnIndex;
            }
        }

        return -1;
    }

    public static char GetColumnLetter(int columnIndex)
    {
        return columnIndex >= 0 && columnIndex < ColumnLetters.Length
            ? ColumnLetters[columnIndex]
            : '\0';
    }

    public static char GetLetterForNumber(int number, BingoBallCountType ballCountType)
    {
        return GetColumnLetter(GetColumnIndex(number, ballCountType));
    }
}
