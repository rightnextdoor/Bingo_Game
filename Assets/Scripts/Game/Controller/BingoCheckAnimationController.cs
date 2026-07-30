using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BingoCheckAnimationController : MonoBehaviour
{
    #region Fields

    [SerializeField] private BingoBoardController boardController;

    [Header("Check Timing")]
    [SerializeField, Min(0f)] private float cellCheckSeconds = 0.3f;
    [SerializeField, Min(0f)] private float patternResultSeconds = 1f;

    [Header("Final Pattern Timing")]
    [SerializeField, Min(0f)] private float finalPatternSeconds = 2f;

    [Header("Failure")]
    [SerializeField] private Color failureColor = Color.red;

    private Coroutine animationRoutine;

    public bool IsPlaying => animationRoutine != null;

    #endregion

    #region Check Animation

    public void PlayCheckAnimation(BingoCheckResult checkResult, Action onComplete)
    {
        StopAnimation();
        boardController?.ClearCheckHighlights();

        animationRoutine = StartCoroutine(PlayCheckRoutine(checkResult, onComplete));
    }

    private IEnumerator PlayCheckRoutine(BingoCheckResult checkResult, Action onComplete)
    {
        if (checkResult?.patterns != null)
        {
            for (int patternIndex = 0; patternIndex < checkResult.patterns.Count; patternIndex++)
            {
                BingoPatternCheckResult patternResult = checkResult.patterns[patternIndex];

                if (patternResult == null)
                    continue;

                Color patternColor = GetPatternColor(patternResult.patternType);

                yield return PlayPatternCellCheck(patternResult, patternColor);

                Color finalColor =
                    patternResult.isWinningPattern
                        ? patternColor
                        : failureColor;

                ShowPattern(patternResult, finalColor);

                if (patternResultSeconds > 0f)
                    yield return new WaitForSecondsRealtime(patternResultSeconds);

                if (HasNextPattern(checkResult.patterns, patternIndex))
                    boardController?.ClearCheckHighlights();
            }
        }

        animationRoutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator PlayPatternCellCheck(BingoPatternCheckResult patternResult, Color patternColor)
    {
        if (patternResult?.cells == null)
            yield break;

        for (int i = 0; i < patternResult.cells.Count; i++)
        {
            BingoCellCheckResult cellResult = patternResult.cells[i];

            if (cellResult == null)
                continue;

            Color cellColor = cellResult.IsValid ? patternColor : failureColor;

            boardController?.ShowCheckHighlight(cellResult.cellIndex, cellColor);

            if (cellCheckSeconds > 0f)
                yield return new WaitForSecondsRealtime(cellCheckSeconds);

            boardController?.ClearCheckHighlight(cellResult.cellIndex);
        }
    }

    #endregion

    #region Continue

    public void ContinuePlaying()
    {
        StopAnimation();
        boardController?.ClearCheckHighlights();
    }

    #endregion

    #region Final Winner

    public void PlayWinnerAnimation(IReadOnlyList<BingoPatternCheckResult> winningPatterns)
    {
        StopAnimation();
        boardController?.ClearCheckHighlights();

        List<BingoPatternCheckResult> validPatterns = GetValidPatterns(winningPatterns);

        if (validPatterns.Count == 0)
            return;

        animationRoutine = StartCoroutine(PlayFinalPatternRoutine(validPatterns, true));
    }

    #endregion

    #region Final Loser

    public void PlayLoserAnimation(IReadOnlyList<BingoPatternCheckResult> failedPatterns)
    {
        StopAnimation();
        boardController?.ClearCheckHighlights();

        List<BingoPatternCheckResult> validPatterns = GetValidPatterns(failedPatterns);

        if (validPatterns.Count == 0)
            return;

        animationRoutine = StartCoroutine(PlayFinalPatternRoutine(validPatterns, false));
    }

    #endregion

    #region Final Pattern Animation

    private IEnumerator PlayFinalPatternRoutine(
        IReadOnlyList<BingoPatternCheckResult> patterns,
        bool usePatternColors)
    {
        if (patterns.Count == 1)
        {
            BingoPatternCheckResult patternResult = patterns[0];

            Color color =
                usePatternColors
                    ? GetPatternColor(patternResult.patternType)
                    : failureColor;

            ShowPattern(patternResult, color);

            animationRoutine = null;
            yield break;
        }

        while (true)
        {
            for (int i = 0; i < patterns.Count; i++)
            {
                BingoPatternCheckResult patternResult = patterns[i];

                boardController?.ClearCheckHighlights();

                Color color =
                    usePatternColors
                        ? GetPatternColor(patternResult.patternType)
                        : failureColor;

                ShowPattern(patternResult, color);

                if (finalPatternSeconds > 0f)
                    yield return new WaitForSecondsRealtime(finalPatternSeconds);
                else
                    yield return null;
            }
        }
    }

    #endregion

    #region Pattern Display

    private void ShowPattern(BingoPatternCheckResult patternResult, Color color)
    {
        if (patternResult?.cells == null)
            return;

        for (int i = 0; i < patternResult.cells.Count; i++)
        {
            BingoCellCheckResult cellResult = patternResult.cells[i];

            if (cellResult != null)
                boardController?.ShowCheckHighlight(cellResult.cellIndex, color);
        }
    }

    private Color GetPatternColor(BingoPatternType patternType)
    {
        GameModeManager gameModeManager = GameModeManager.instance;

        if (gameModeManager == null)
            return Color.white;

        return gameModeManager.GetBingoPatternHighlightColor(patternType);
    }

    #endregion

    #region Animation Control

    public void StopAndClear()
    {
        StopAnimation();
        boardController?.ClearCheckHighlights();
    }

    private void StopAnimation()
    {
        if (animationRoutine == null)
            return;

        StopCoroutine(animationRoutine);
        animationRoutine = null;
    }

    #endregion

    #region Helpers

    private bool HasNextPattern(IReadOnlyList<BingoPatternCheckResult> patterns, int currentIndex)
    {
        for (int i = currentIndex + 1; i < patterns.Count; i++)
        {
            if (patterns[i] != null)
                return true;
        }

        return false;
    }

    private List<BingoPatternCheckResult> GetValidPatterns(
        IReadOnlyList<BingoPatternCheckResult> patterns)
    {
        List<BingoPatternCheckResult> validPatterns = new List<BingoPatternCheckResult>();

        if (patterns == null)
            return validPatterns;

        for (int i = 0; i < patterns.Count; i++)
        {
            BingoPatternCheckResult patternResult = patterns[i];

            if (patternResult != null &&
                patternResult.cells != null &&
                patternResult.cells.Count > 0)
            {
                validPatterns.Add(patternResult);
            }
        }

        return validPatterns;
    }

    #endregion
}