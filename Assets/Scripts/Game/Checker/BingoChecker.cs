using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BingoChecker : MonoBehaviour
{
    #region Fields

    [SerializeField] private BingoPatternValidator validator;
    [SerializeField] private BingoCheckAnimationController animationController;

    private readonly Dictionary<string, List<BingoCheckResult>> checkHistoryByPlayerId =
        new Dictionary<string, List<BingoCheckResult>>();

    private BingoCheckResult currentCheckResult;
    private bool isChecking;

    public BingoCheckResult CurrentCheckResult => currentCheckResult;
    public bool IsChecking => isChecking;

    public event Action<BingoCheckResult> CheckCompleted;

    #endregion

    #region Check

    public bool StartCheck(
        string playerId,
        LobbyBoardData boardData,
        IReadOnlyCollection<int> pressedCellIndices,
        IReadOnlyCollection<int> calledNumbers,
        IReadOnlyCollection<BingoPatternType> patternTypes)
    {
        if (isChecking ||
            validator == null ||
            animationController == null ||
            string.IsNullOrWhiteSpace(playerId) ||
            boardData == null)
        {
            return false;
        }

        List<BingoCheckResult> checkHistory = GetOrCreateCheckHistory(playerId);

        BingoCheckResult checkResult =
            validator.Validate(
                playerId,
                boardData,
                pressedCellIndices,
                calledNumbers,
                patternTypes,
                checkHistory);

        if (checkResult == null)
            return false;

        checkResult.checkNumber = checkHistory.Count + 1;
        checkHistory.Add(checkResult);

        currentCheckResult = checkResult;
        isChecking = true;

        PlayCheckAnimation(checkResult);

        return true;
    }

    private void PlayCheckAnimation(BingoCheckResult checkResult)
    {
        animationController.PlayCheckAnimation(
            checkResult,
            CompleteCheckAnimation);
    }

    private void CompleteCheckAnimation()
    {
        if (!isChecking || currentCheckResult == null)
            return;

        BingoCheckResult completedResult = currentCheckResult;

        currentCheckResult = null;
        isChecking = false;

        CheckCompleted?.Invoke(completedResult);
    }

    #endregion

    #region Result Animations

    public void ContinuePlaying(BingoCheckResult checkResult)
    {
        if (checkResult == null)
            return;

        animationController?.ContinuePlaying();
    }

    public void PlayPlayerWonAnimation(BingoCheckResult checkResult)
    {
        if (checkResult == null)
            return;

        List<BingoPatternCheckResult> winningPatterns =
            GetWinningPatternsFromHistory(checkResult.playerId);

        animationController?.PlayWinnerAnimation(winningPatterns);
    }

    public void PlayPlayerLostAnimation(BingoCheckResult checkResult)
    {
        if (checkResult == null)
            return;

        List<BingoPatternCheckResult> failedPatterns =
            GetFailedPatterns(checkResult);

        animationController?.PlayLoserAnimation(failedPatterns);
    }

    #endregion

    #region Check History

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
        if (string.IsNullOrWhiteSpace(playerId))
            return;

        checkHistoryByPlayerId.Remove(playerId);
    }

    public void ClearAllCheckHistory()
    {
        checkHistoryByPlayerId.Clear();
        currentCheckResult = null;
        isChecking = false;

        animationController?.StopAndClear();
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

    #endregion

    #region Pattern History

    private List<BingoPatternCheckResult> GetWinningPatternsFromHistory(string playerId)
    {
        List<BingoPatternCheckResult> winningPatterns = new List<BingoPatternCheckResult>();

        IReadOnlyList<BingoCheckResult> checkHistory = GetCheckHistory(playerId);

        for (int checkIndex = 0; checkIndex < checkHistory.Count; checkIndex++)
        {
            BingoCheckResult checkResult = checkHistory[checkIndex];

            if (checkResult?.patterns == null)
                continue;

            for (int patternIndex = 0; patternIndex < checkResult.patterns.Count; patternIndex++)
            {
                BingoPatternCheckResult patternResult = checkResult.patterns[patternIndex];

                if (patternResult != null && patternResult.isWinningPattern)
                    winningPatterns.Add(patternResult);
            }
        }

        return winningPatterns;
    }

    private List<BingoPatternCheckResult> GetFailedPatterns(BingoCheckResult checkResult)
    {
        List<BingoPatternCheckResult> failedPatterns = new List<BingoPatternCheckResult>();

        if (checkResult?.patterns == null)
            return failedPatterns;

        for (int i = 0; i < checkResult.patterns.Count; i++)
        {
            BingoPatternCheckResult patternResult = checkResult.patterns[i];

            if (patternResult != null && !patternResult.isWinningPattern)
                failedPatterns.Add(patternResult);
        }

        return failedPatterns;
    }

    #endregion
}