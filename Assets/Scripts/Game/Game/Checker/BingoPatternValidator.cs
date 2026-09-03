using System;
using System.Collections.Generic;
using UnityEngine;

public enum BingoLineType
{
    None,

    Row0,
    Row1,
    Row2,
    Row3,
    Row4,

    Column0,
    Column1,
    Column2,
    Column3,
    Column4,

    DiagonalDown,
    DiagonalUp
}

[Serializable]
public class BingoCellCheckResult
{
    public int cellIndex;
    public int number;
    public bool isFree;
    public bool isPressed;
    public bool isNumberValid;

    public bool IsValid => isPressed && isNumberValid;

    public BingoCellCheckResult(int cellIndex, int number, bool isFree, bool isPressed, bool isNumberValid)
    {
        this.cellIndex = cellIndex;
        this.number = number;
        this.isFree = isFree;
        this.isPressed = isPressed;
        this.isNumberValid = isNumberValid;
    }
}

[Serializable]
public class BingoPatternCheckResult
{
    public BingoPatternType patternType;
    public BingoLineType primaryLine = BingoLineType.None;
    public BingoLineType secondaryLine = BingoLineType.None;
    public List<BingoCellCheckResult> cells = new List<BingoCellCheckResult>();
    public bool isWinningPattern;

    public BingoPatternCheckResult(
        BingoPatternType patternType,
        BingoLineType primaryLine,
        BingoLineType secondaryLine)
    {
        this.patternType = patternType;
        this.primaryLine = primaryLine;
        this.secondaryLine = secondaryLine;
    }
}

[Serializable]
public class BingoCheckResult
{
    public string playerId = string.Empty;
    public int checkNumber;
    public List<BingoPatternCheckResult> patterns = new List<BingoPatternCheckResult>();

    public bool HasCheckedPatterns => patterns != null && patterns.Count > 0;

    public bool HasWinningPattern
    {
        get
        {
            if (patterns == null)
                return false;

            for (int i = 0; i < patterns.Count; i++)
            {
                if (patterns[i] != null && patterns[i].isWinningPattern)
                    return true;
            }

            return false;
        }
    }

    public bool HasFailedPattern
    {
        get
        {
            if (patterns == null)
                return false;

            for (int i = 0; i < patterns.Count; i++)
            {
                if (patterns[i] != null && !patterns[i].isWinningPattern)
                    return true;
            }

            return false;
        }
    }

    public List<BingoPatternCheckResult> GetWinningPatterns()
    {
        List<BingoPatternCheckResult> winningPatterns = new List<BingoPatternCheckResult>();

        if (patterns == null)
            return winningPatterns;

        for (int i = 0; i < patterns.Count; i++)
        {
            BingoPatternCheckResult patternResult = patterns[i];

            if (patternResult != null && patternResult.isWinningPattern)
                winningPatterns.Add(patternResult);
        }

        return winningPatterns;
    }

    public List<BingoPatternCheckResult> GetFailedPatterns()
    {
        List<BingoPatternCheckResult> failedPatterns =
            new List<BingoPatternCheckResult>();

        if (patterns == null)
        {
            return failedPatterns;
        }

        for (int i = 0; i < patterns.Count; i++)
        {
            BingoPatternCheckResult patternResult = patterns[i];

            if (patternResult != null && !patternResult.isWinningPattern)
            {
                failedPatterns.Add(patternResult);
            }
        }

        return failedPatterns;
    }
}

public class BingoPatternValidator
{
    #region Fields

    private const int CellCount = 25;
    private const int FreeCellIndex = 12;

    private static readonly int[] Row0 = { 0, 1, 2, 3, 4 };
    private static readonly int[] Row1 = { 5, 6, 7, 8, 9 };
    private static readonly int[] Row2 = { 10, 11, 12, 13, 14 };
    private static readonly int[] Row3 = { 15, 16, 17, 18, 19 };
    private static readonly int[] Row4 = { 20, 21, 22, 23, 24 };

    private static readonly int[] Column0 = { 0, 5, 10, 15, 20 };
    private static readonly int[] Column1 = { 1, 6, 11, 16, 21 };
    private static readonly int[] Column2 = { 2, 7, 12, 17, 22 };
    private static readonly int[] Column3 = { 3, 8, 13, 18, 23 };
    private static readonly int[] Column4 = { 4, 9, 14, 19, 24 };

    private static readonly int[] DiagonalDown = { 0, 6, 12, 18, 24 };
    private static readonly int[] DiagonalUp = { 4, 8, 12, 16, 20 };

    private static readonly int[] FourCorners = { 0, 4, 20, 24 };

    private static readonly int[] Cross =
    {
        10, 11, 12, 13, 14,
        2, 7, 17, 22
    };

    private static readonly int[] XPattern =
    {
        0, 6, 12, 18, 24,
        4, 8, 16, 20
    };

    private static readonly int[] Diamond =
    {
        2,
        6, 7, 8,
        10, 11, 12, 13, 14,
        16, 17, 18,
        22
    };

    private static readonly int[] Star =
    {
        2,
        6, 8,
        10, 11, 12, 13, 14,
        16, 18,
        22
    };

    private static readonly int[] Blackout =
    {
        0, 1, 2, 3, 4,
        5, 6, 7, 8, 9,
        10, 11, 12, 13, 14,
        15, 16, 17, 18, 19,
        20, 21, 22, 23, 24
    };

    #endregion

    #region Validation

    public BingoCheckResult Validate(
        string playerId,
        LobbyBoardData boardData,
        IReadOnlyCollection<int> pressedCellIndices,
        IReadOnlyCollection<int> calledNumbers,
        IReadOnlyCollection<BingoPatternType> configuredPatterns,
        IReadOnlyList<BingoCheckResult> checkHistory)
    {
        BingoCheckResult result = new BingoCheckResult
        {
            playerId = playerId
        };

        if (!IsValidBoard(boardData) || configuredPatterns == null)
            return result;

        HashSet<int> pressedCells = BuildSet(pressedCellIndices);
        HashSet<int> calledNumberSet = BuildSet(calledNumbers);

        foreach (BingoPatternType patternType in Enum.GetValues(typeof(BingoPatternType)))
        {
            if (!ContainsPattern(configuredPatterns, patternType))
                continue;

            switch (patternType)
            {
                case BingoPatternType.SingleLine:
                    FindSingleLines(result, boardData, pressedCells, calledNumberSet, checkHistory);
                    break;

                case BingoPatternType.TwoLines:
                    FindTwoLines(
                        result,
                        boardData,
                        pressedCells,
                        calledNumberSet,
                        configuredPatterns,
                        checkHistory);
                    break;

                case BingoPatternType.FourCorners:
                    FindFixedPattern(
                        result,
                        BingoPatternType.FourCorners,
                        FourCorners,
                        boardData,
                        pressedCells,
                        calledNumberSet,
                        checkHistory);
                    break;

                case BingoPatternType.Cross:
                    FindFixedPattern(
                        result,
                        BingoPatternType.Cross,
                        Cross,
                        boardData,
                        pressedCells,
                        calledNumberSet,
                        checkHistory);
                    break;

                case BingoPatternType.XPattern:
                    FindFixedPattern(
                        result,
                        BingoPatternType.XPattern,
                        XPattern,
                        boardData,
                        pressedCells,
                        calledNumberSet,
                        checkHistory);
                    break;

                case BingoPatternType.Diamond:
                    FindFixedPattern(
                        result,
                        BingoPatternType.Diamond,
                        Diamond,
                        boardData,
                        pressedCells,
                        calledNumberSet,
                        checkHistory);
                    break;

                case BingoPatternType.Star:
                    FindFixedPattern(
                        result,
                        BingoPatternType.Star,
                        Star,
                        boardData,
                        pressedCells,
                        calledNumberSet,
                        checkHistory);
                    break;

                case BingoPatternType.Blackout:
                    FindFixedPattern(
                        result,
                        BingoPatternType.Blackout,
                        Blackout,
                        boardData,
                        pressedCells,
                        calledNumberSet,
                        checkHistory);
                    break;
            }
        }

        if (!result.HasCheckedPatterns)
        {
            AddFailedSubmissionResult(
                result,
                boardData,
                pressedCells,
                calledNumberSet,
                configuredPatterns);
        }

        return result;
    }

    #endregion

    #region Pattern Layout

    public IReadOnlyList<int> GetSimulationPatternCells(BingoPatternType patternType)
    {
        switch (patternType)
        {
            case BingoPatternType.SingleLine:
                return Row0;

            case BingoPatternType.TwoLines:
                return CombineLineCells(BingoLineType.Row0, BingoLineType.Row1);

            case BingoPatternType.FourCorners:
                return FourCorners;

            case BingoPatternType.Cross:
                return Cross;

            case BingoPatternType.XPattern:
                return XPattern;

            case BingoPatternType.Diamond:
                return Diamond;

            case BingoPatternType.Star:
                return Star;

            case BingoPatternType.Blackout:
                return Blackout;

            default:
                return Array.Empty<int>();
        }
    }

    #endregion

    #region Single Line

    private void FindSingleLines(
        BingoCheckResult result,
        LobbyBoardData boardData,
        HashSet<int> pressedCells,
        HashSet<int> calledNumbers,
        IReadOnlyList<BingoCheckResult> checkHistory)
    {
        BingoLineType[] lines =
        {
            BingoLineType.Row0,
            BingoLineType.Row1,
            BingoLineType.Row2,
            BingoLineType.Row3,
            BingoLineType.Row4,

            BingoLineType.Column0,
            BingoLineType.Column1,
            BingoLineType.Column2,
            BingoLineType.Column3,
            BingoLineType.Column4
        };

        for (int i = 0; i < lines.Length; i++)
        {
            BingoLineType lineType = lines[i];
            IReadOnlyList<int> cells = GetLineCells(lineType);

            if (WasSingleLineChecked(checkHistory, lineType) ||
                !AreAllCellsPressed(cells, pressedCells))
            {
                continue;
            }

            result.patterns.Add(
                BuildPatternResult(
                    BingoPatternType.SingleLine,
                    lineType,
                    BingoLineType.None,
                    cells,
                    boardData,
                    pressedCells,
                    calledNumbers));
        }
    }

    #endregion

    #region Two Lines

    private void FindTwoLines(
        BingoCheckResult result,
        LobbyBoardData boardData,
        HashSet<int> pressedCells,
        HashSet<int> calledNumbers,
        IReadOnlyCollection<BingoPatternType> configuredPatterns,
        IReadOnlyList<BingoCheckResult> checkHistory)
    {
        if (WasPatternChecked(checkHistory, BingoPatternType.TwoLines))
            return;

        List<BingoLineType> completedLines = GetCompletedLines(pressedCells);

        for (int firstIndex = 0; firstIndex < completedLines.Count; firstIndex++)
        {
            for (int secondIndex = firstIndex + 1; secondIndex < completedLines.Count; secondIndex++)
            {
                BingoLineType firstLine = completedLines[firstIndex];
                BingoLineType secondLine = completedLines[secondIndex];

                if (IsReservedTwoLinePair(firstLine, secondLine, configuredPatterns))
                    continue;

                List<int> cells = CombineLineCells(firstLine, secondLine);

                result.patterns.Add(
                    BuildPatternResult(
                        BingoPatternType.TwoLines,
                        firstLine,
                        secondLine,
                        cells,
                        boardData,
                        pressedCells,
                        calledNumbers));

                return;
            }
        }
    }

    private List<BingoLineType> GetCompletedLines(HashSet<int> pressedCells)
    {
        BingoLineType[] lines =
        {
            BingoLineType.Row0,
            BingoLineType.Row1,
            BingoLineType.Row2,
            BingoLineType.Row3,
            BingoLineType.Row4,

            BingoLineType.Column0,
            BingoLineType.Column1,
            BingoLineType.Column2,
            BingoLineType.Column3,
            BingoLineType.Column4,

            BingoLineType.DiagonalDown,
            BingoLineType.DiagonalUp
        };

        List<BingoLineType> completedLines = new List<BingoLineType>();

        for (int i = 0; i < lines.Length; i++)
        {
            BingoLineType lineType = lines[i];

            if (AreAllCellsPressed(GetLineCells(lineType), pressedCells))
                completedLines.Add(lineType);
        }

        return completedLines;
    }

    private bool IsReservedTwoLinePair(
        BingoLineType firstLine,
        BingoLineType secondLine,
        IReadOnlyCollection<BingoPatternType> configuredPatterns)
    {
        bool crossConfigured = ContainsPattern(configuredPatterns, BingoPatternType.Cross);
        bool xConfigured = ContainsPattern(configuredPatterns, BingoPatternType.XPattern);

        if (crossConfigured &&
            IsLinePair(
                firstLine,
                secondLine,
                BingoLineType.Row2,
                BingoLineType.Column2))
        {
            return true;
        }

        if (xConfigured &&
            IsLinePair(
                firstLine,
                secondLine,
                BingoLineType.DiagonalDown,
                BingoLineType.DiagonalUp))
        {
            return true;
        }

        return false;
    }

    #endregion

    #region Fixed Patterns

    private void FindFixedPattern(
        BingoCheckResult result,
        BingoPatternType patternType,
        IReadOnlyList<int> cells,
        LobbyBoardData boardData,
        HashSet<int> pressedCells,
        HashSet<int> calledNumbers,
        IReadOnlyList<BingoCheckResult> checkHistory)
    {
        if (WasPatternChecked(checkHistory, patternType) ||
            !HasUsableCells(cells) ||
            !AreAllCellsPressed(cells, pressedCells))
        {
            return;
        }

        result.patterns.Add(
            BuildPatternResult(
                patternType,
                BingoLineType.None,
                BingoLineType.None,
                cells,
                boardData,
                pressedCells,
                calledNumbers));
    }

    #endregion

    #region Pattern Result

    private void AddFailedSubmissionResult(
        BingoCheckResult result,
        LobbyBoardData boardData,
        HashSet<int> pressedCells,
        HashSet<int> calledNumbers,
        IReadOnlyCollection<BingoPatternType> configuredPatterns)
    {
        if (result == null || boardData?.cellNumbers == null)
        {
            return;
        }

        BingoPatternType patternType = BingoPatternType.SingleLine;

        foreach (BingoPatternType configuredPattern in configuredPatterns)
        {
            patternType = configuredPattern;
            break;
        }

        BingoPatternCheckResult failedResult =
            new BingoPatternCheckResult(
                patternType,
                BingoLineType.None,
                BingoLineType.None)
            {
                isWinningPattern = false
            };

        List<int> sortedPressedCells = new List<int>(pressedCells);
        sortedPressedCells.Sort();

        for (int i = 0; i < sortedPressedCells.Count; i++)
        {
            int cellIndex = sortedPressedCells[i];

            if (cellIndex < 0 || cellIndex >= boardData.cellNumbers.Count)
            {
                continue;
            }

            int number = boardData.cellNumbers[cellIndex];
            bool isFree = boardData.usesFreeCell && cellIndex == FreeCellIndex;

            failedResult.cells.Add(
                new BingoCellCheckResult(
                    cellIndex,
                    number,
                    isFree,
                    true,
                    isFree || calledNumbers.Contains(number)));
        }

        result.patterns.Add(failedResult);
    }

    private BingoPatternCheckResult BuildPatternResult(
        BingoPatternType patternType,
        BingoLineType primaryLine,
        BingoLineType secondaryLine,
        IReadOnlyList<int> cellIndices,
        LobbyBoardData boardData,
        HashSet<int> pressedCells,
        HashSet<int> calledNumbers)
    {
        BingoPatternCheckResult result =
            new BingoPatternCheckResult(
                patternType,
                primaryLine,
                secondaryLine);

        result.isWinningPattern = true;

        for (int i = 0; i < cellIndices.Count; i++)
        {
            int cellIndex = cellIndices[i];
            int number = boardData.cellNumbers[cellIndex];

            bool isPressed = pressedCells.Contains(cellIndex);
            bool isFree = boardData.usesFreeCell && cellIndex == FreeCellIndex;
            bool isNumberValid = isFree || calledNumbers.Contains(number);

            BingoCellCheckResult cellResult =
                new BingoCellCheckResult(
                    cellIndex,
                    number,
                    isFree,
                    isPressed,
                    isNumberValid);

            result.cells.Add(cellResult);

            if (!cellResult.IsValid)
                result.isWinningPattern = false;
        }

        return result;
    }

    #endregion

    #region Check History

    private bool WasSingleLineChecked(IReadOnlyList<BingoCheckResult> checkHistory, BingoLineType lineType)
    {
        if (checkHistory == null)
            return false;

        for (int checkIndex = 0; checkIndex < checkHistory.Count; checkIndex++)
        {
            BingoCheckResult checkResult = checkHistory[checkIndex];

            if (checkResult?.patterns == null)
                continue;

            for (int patternIndex = 0; patternIndex < checkResult.patterns.Count; patternIndex++)
            {
                BingoPatternCheckResult patternResult = checkResult.patterns[patternIndex];

                if (patternResult != null &&
                    patternResult.patternType == BingoPatternType.SingleLine &&
                    patternResult.primaryLine == lineType)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool WasPatternChecked(IReadOnlyList<BingoCheckResult> checkHistory, BingoPatternType patternType)
    {
        if (checkHistory == null)
            return false;

        for (int checkIndex = 0; checkIndex < checkHistory.Count; checkIndex++)
        {
            BingoCheckResult checkResult = checkHistory[checkIndex];

            if (checkResult?.patterns == null)
                continue;

            for (int patternIndex = 0; patternIndex < checkResult.patterns.Count; patternIndex++)
            {
                BingoPatternCheckResult patternResult = checkResult.patterns[patternIndex];

                if (patternResult != null && patternResult.patternType == patternType)
                    return true;
            }
        }

        return false;
    }

    #endregion

    #region Line Helpers

    private IReadOnlyList<int> GetLineCells(BingoLineType lineType)
    {
        switch (lineType)
        {
            case BingoLineType.Row0:
                return Row0;

            case BingoLineType.Row1:
                return Row1;

            case BingoLineType.Row2:
                return Row2;

            case BingoLineType.Row3:
                return Row3;

            case BingoLineType.Row4:
                return Row4;

            case BingoLineType.Column0:
                return Column0;

            case BingoLineType.Column1:
                return Column1;

            case BingoLineType.Column2:
                return Column2;

            case BingoLineType.Column3:
                return Column3;

            case BingoLineType.Column4:
                return Column4;

            case BingoLineType.DiagonalDown:
                return DiagonalDown;

            case BingoLineType.DiagonalUp:
                return DiagonalUp;

            default:
                return Array.Empty<int>();
        }
    }

    private List<int> CombineLineCells(BingoLineType firstLine, BingoLineType secondLine)
    {
        List<int> cells = new List<int>();

        AddUniqueCells(cells, GetLineCells(firstLine));
        AddUniqueCells(cells, GetLineCells(secondLine));

        return cells;
    }

    private void AddUniqueCells(List<int> destination, IReadOnlyList<int> source)
    {
        for (int i = 0; i < source.Count; i++)
        {
            if (!destination.Contains(source[i]))
                destination.Add(source[i]);
        }
    }

    private bool IsLinePair(
        BingoLineType firstLine,
        BingoLineType secondLine,
        BingoLineType requiredFirst,
        BingoLineType requiredSecond)
    {
        return
            (firstLine == requiredFirst && secondLine == requiredSecond) ||
            (firstLine == requiredSecond && secondLine == requiredFirst);
    }

    #endregion

    #region Helpers

    private bool IsValidBoard(LobbyBoardData boardData)
    {
        return boardData != null &&
               boardData.cellNumbers != null &&
               boardData.cellNumbers.Count == CellCount;
    }

    private bool HasUsableCells(IReadOnlyList<int> cells)
    {
        if (cells == null || cells.Count == 0)
            return false;

        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] < 0 || cells[i] >= CellCount)
                return false;
        }

        return true;
    }

    private bool AreAllCellsPressed(IReadOnlyList<int> cells, HashSet<int> pressedCells)
    {
        if (!HasUsableCells(cells))
            return false;

        for (int i = 0; i < cells.Count; i++)
        {
            if (!pressedCells.Contains(cells[i]))
                return false;
        }

        return true;
    }

    private bool ContainsPattern(IReadOnlyCollection<BingoPatternType> patterns, BingoPatternType patternType)
    {
        if (patterns == null)
            return false;

        foreach (BingoPatternType currentPatternType in patterns)
        {
            if (currentPatternType == patternType)
                return true;
        }

        return false;
    }

    private HashSet<int> BuildSet(IReadOnlyCollection<int> values)
    {
        HashSet<int> set = new HashSet<int>();

        if (values == null)
            return set;

        foreach (int value in values)
            set.Add(value);

        return set;
    }

    #endregion
}
