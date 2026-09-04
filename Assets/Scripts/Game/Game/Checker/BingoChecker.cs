using System;
using System.Collections.Generic;

[Serializable]
public class BingoChecker
{
    private readonly BingoPatternValidator validator = new BingoPatternValidator();
    private readonly Dictionary<string, List<BingoCheckResult>> checkHistoryByPlayerId =
        new Dictionary<string, List<BingoCheckResult>>();

    private BingoCheckResult currentCheckResult;

    public BingoCheckResult CurrentCheckResult => currentCheckResult;

    public bool TryCheck(
        string playerId,
        LobbyBoardData boardData,
        IReadOnlyCollection<int> pressedCellIndices,
        IReadOnlyCollection<int> calledNumbers,
        IReadOnlyCollection<BingoPatternType> patternTypes)
    {
        if (string.IsNullOrWhiteSpace(playerId) || boardData == null)
        {
            return false;
        }

        List<BingoCheckResult> checkHistory = GetOrCreateCheckHistory(playerId);
        BingoCheckResult checkResult = validator.Validate(
            playerId,
            boardData,
            pressedCellIndices,
            calledNumbers,
            patternTypes,
            checkHistory);

        if (checkResult == null)
        {
            return false;
        }

        checkResult.checkNumber = checkHistory.Count + 1;
        ApplyCurrentCheckScores(checkResult);
        checkHistory.Add(checkResult);
        currentCheckResult = checkResult;
        return true;
    }

    private static void ApplyCurrentCheckScores(BingoCheckResult checkResult)
    {
        checkResult.currentCheckPatternPoints = 0;

        if (checkResult.patterns == null || GameScoreManager.instance == null)
        {
            return;
        }

        long currentCheckPoints = 0;

        for (int i = 0; i < checkResult.patterns.Count; i++)
        {
            BingoPatternCheckResult patternResult = checkResult.patterns[i];

            if (patternResult == null || !patternResult.isWinningPattern)
            {
                continue;
            }

            patternResult.scorePoints = GameScoreManager.instance.GetPatternPoints(
                patternResult.patternType);
            currentCheckPoints += patternResult.scorePoints;
        }

        int maximumScore = GameSettings.instance != null
            ? GameSettings.instance.MaximumScore
            : UserStats.DefaultMaximumScore;
        checkResult.currentCheckPatternPoints = currentCheckPoints >= maximumScore
            ? maximumScore
            : (int)currentCheckPoints;
    }

    public IReadOnlyList<BingoCheckResult> GetCheckHistory(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId) ||
            !checkHistoryByPlayerId.TryGetValue(playerId, out List<BingoCheckResult> checkHistory))
        {
            return Array.Empty<BingoCheckResult>();
        }

        return checkHistory;
    }

    public void ClearCheckHistory(string playerId)
    {
        if (!string.IsNullOrWhiteSpace(playerId))
        {
            checkHistoryByPlayerId.Remove(playerId);
        }
    }

    public void ClearAllCheckHistory()
    {
        checkHistoryByPlayerId.Clear();
        currentCheckResult = null;
    }

    public List<BingoPatternType> GetCheckedPatternTypes(string playerId)
    {
        List<BingoPatternType> checkedPatternTypes = new List<BingoPatternType>();
        IReadOnlyList<BingoCheckResult> checkHistory = GetCheckHistory(playerId);

        for (int checkIndex = 0; checkIndex < checkHistory.Count; checkIndex++)
        {
            BingoCheckResult checkResult = checkHistory[checkIndex];

            if (checkResult?.patterns == null)
            {
                continue;
            }

            for (int patternIndex = 0; patternIndex < checkResult.patterns.Count; patternIndex++)
            {
                BingoPatternCheckResult patternResult = checkResult.patterns[patternIndex];

                if (patternResult != null && !checkedPatternTypes.Contains(patternResult.patternType))
                {
                    checkedPatternTypes.Add(patternResult.patternType);
                }
            }
        }

        return checkedPatternTypes;
    }

    public List<BingoPatternType> GetAvailablePatternTypes(
        string playerId,
        IReadOnlyCollection<BingoPatternType> configuredPatternTypes)
    {
        List<BingoPatternType> availablePatternTypes = new List<BingoPatternType>();

        if (configuredPatternTypes == null)
        {
            return availablePatternTypes;
        }

        IReadOnlyList<BingoCheckResult> checkHistory = GetCheckHistory(playerId);

        foreach (BingoPatternType patternType in configuredPatternTypes)
        {
            if (!availablePatternTypes.Contains(patternType) &&
                !IsPatternExhausted(checkHistory, patternType))
            {
                availablePatternTypes.Add(patternType);
            }
        }

        return availablePatternTypes;
    }

    public void AddCheckedPatterns(
        string playerId,
        IReadOnlyCollection<BingoPatternCheckResult> patternResults)
    {
        if (string.IsNullOrWhiteSpace(playerId) || patternResults == null)
        {
            return;
        }

        BingoCheckResult checkResult = new BingoCheckResult
        {
            playerId = playerId
        };

        foreach (BingoPatternCheckResult patternResult in patternResults)
        {
            if (patternResult != null)
            {
                checkResult.patterns.Add(patternResult);
            }
        }

        if (checkResult.patterns.Count == 0)
        {
            return;
        }

        List<BingoCheckResult> checkHistory = GetOrCreateCheckHistory(playerId);
        checkResult.checkNumber = checkHistory.Count + 1;
        checkHistory.Add(checkResult);
        currentCheckResult = checkResult;
    }

    public List<BingoPatternCheckResult> GetWinningPatternsFromHistory(string playerId)
    {
        List<BingoPatternCheckResult> winningPatterns = new List<BingoPatternCheckResult>();
        IReadOnlyList<BingoCheckResult> checkHistory = GetCheckHistory(playerId);

        for (int checkIndex = 0; checkIndex < checkHistory.Count; checkIndex++)
        {
            BingoCheckResult checkResult = checkHistory[checkIndex];

            if (checkResult?.patterns == null)
            {
                continue;
            }

            for (int patternIndex = 0; patternIndex < checkResult.patterns.Count; patternIndex++)
            {
                BingoPatternCheckResult patternResult = checkResult.patterns[patternIndex];

                if (patternResult != null && patternResult.isWinningPattern)
                {
                    winningPatterns.Add(patternResult);
                }
            }
        }

        return winningPatterns;
    }

    public List<BingoPatternCheckResult> GetFailedPatterns(BingoCheckResult checkResult)
    {
        List<BingoPatternCheckResult> failedPatterns = new List<BingoPatternCheckResult>();

        if (checkResult?.patterns == null)
        {
            return failedPatterns;
        }

        for (int i = 0; i < checkResult.patterns.Count; i++)
        {
            BingoPatternCheckResult patternResult = checkResult.patterns[i];

            if (patternResult != null && !patternResult.isWinningPattern)
            {
                failedPatterns.Add(patternResult);
            }
        }

        return failedPatterns;
    }

    private List<BingoCheckResult> GetOrCreateCheckHistory(string playerId)
    {
        if (!checkHistoryByPlayerId.TryGetValue(playerId, out List<BingoCheckResult> checkHistory))
        {
            checkHistory = new List<BingoCheckResult>();
            checkHistoryByPlayerId.Add(playerId, checkHistory);
        }

        return checkHistory;
    }

    private bool IsPatternExhausted(
        IReadOnlyList<BingoCheckResult> checkHistory,
        BingoPatternType patternType)
    {
        if (checkHistory == null)
        {
            return false;
        }

        if (patternType == BingoPatternType.SingleLine)
        {
            HashSet<BingoLineType> checkedLines = new HashSet<BingoLineType>();

            for (int checkIndex = 0; checkIndex < checkHistory.Count; checkIndex++)
            {
                BingoCheckResult checkResult = checkHistory[checkIndex];

                if (checkResult?.patterns == null)
                {
                    continue;
                }

                for (int patternIndex = 0; patternIndex < checkResult.patterns.Count; patternIndex++)
                {
                    BingoPatternCheckResult patternResult = checkResult.patterns[patternIndex];

                    if (patternResult != null &&
                        patternResult.patternType == BingoPatternType.SingleLine &&
                        patternResult.primaryLine != BingoLineType.None)
                    {
                        checkedLines.Add(patternResult.primaryLine);
                    }
                }
            }

            return checkedLines.Count >= 10;
        }

        for (int checkIndex = 0; checkIndex < checkHistory.Count; checkIndex++)
        {
            BingoCheckResult checkResult = checkHistory[checkIndex];

            if (checkResult?.patterns == null)
            {
                continue;
            }

            for (int patternIndex = 0; patternIndex < checkResult.patterns.Count; patternIndex++)
            {
                if (checkResult.patterns[patternIndex]?.patternType == patternType)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
