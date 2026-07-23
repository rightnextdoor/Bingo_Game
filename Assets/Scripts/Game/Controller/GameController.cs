using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameController : MonoBehaviour
{
    #region Fields

    [Header("Bingo")]
    [SerializeField] private BingoChecker bingoChecker;
    [SerializeField] private BingoBoardController boardController;
    [SerializeField] private Button bingoButton;

    [Header("Test Player")]
    [SerializeField] private string playerId = "TestPlayer";

    private readonly List<int> calledNumbers = new List<int>();
    private readonly List<BingoPatternType> patternTypes = new List<BingoPatternType>();

    private BingoCheckResult lastBingoCheckResult;

    public IReadOnlyList<int> CalledNumbers => calledNumbers;
    public IReadOnlyList<BingoPatternType> PatternTypes => patternTypes;
    public BingoCheckResult LastBingoCheckResult => lastBingoCheckResult;

    public event Action<BingoCheckResult> BingoCheckResolved;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (bingoChecker != null)
            bingoChecker.CheckCompleted += OnBingoCheckCompleted;

        if (bingoButton != null)
            bingoButton.onClick.AddListener(OnBingoPressed);
    }

    private void OnDisable()
    {
        if (bingoChecker != null)
            bingoChecker.CheckCompleted -= OnBingoCheckCompleted;

        if (bingoButton != null)
            bingoButton.onClick.RemoveListener(OnBingoPressed);
    }

    #endregion

    #region Bingo

    private void OnBingoPressed()
    {
        RequestBingo();
    }

    public bool RequestBingo()
    {
        if (bingoChecker == null ||
            boardController == null ||
            boardController.CurrentBoardData == null ||
            bingoChecker.IsChecking ||
            string.IsNullOrWhiteSpace(playerId))
        {
            return false;
        }

        boardController.SetInteractable(false);
        SetBingoButtonInteractable(false);

        List<int> markedCellIndices = boardController.GetMarkedCellIndicesSnapshot();

        bool started =
            bingoChecker.StartCheck(
                playerId,
                boardController.CurrentBoardData,
                markedCellIndices,
                calledNumbers,
                patternTypes);

        if (started)
            return true;

        boardController.SetInteractable(true);
        SetBingoButtonInteractable(true);

        return false;
    }

    private void OnBingoCheckCompleted(BingoCheckResult checkResult)
    {
        if (checkResult == null)
            return;

        lastBingoCheckResult = checkResult;

        BingoCheckResolved?.Invoke(checkResult);

        HandleBingoCheckResult(checkResult);
    }

    private void HandleBingoCheckResult(BingoCheckResult checkResult)
    {
    }

    #endregion

    #region Bingo Outcomes

    public void ContinuePlayerAfterBingoCheck()
    {
        if (lastBingoCheckResult == null)
            return;

        bingoChecker?.ContinuePlaying(lastBingoCheckResult);

        boardController?.SetInteractable(true);
        SetBingoButtonInteractable(true);
    }

    public void FinishPlayerAsWinner()
    {
        if (lastBingoCheckResult == null)
            return;

        boardController?.SetInteractable(false);
        SetBingoButtonInteractable(false);

        bingoChecker?.PlayPlayerWonAnimation(lastBingoCheckResult);
    }

    public void FinishPlayerAsLoser()
    {
        if (lastBingoCheckResult == null)
            return;

        boardController?.SetInteractable(false);
        SetBingoButtonInteractable(false);

        bingoChecker?.PlayPlayerLostAnimation(lastBingoCheckResult);
    }

    private void SetBingoButtonInteractable(bool interactable)
    {
        if (bingoButton != null)
            bingoButton.interactable = interactable;
    }

    #endregion

    #region Called Numbers

    public void AddCalledNumber(int number)
    {
        if (!calledNumbers.Contains(number))
            calledNumbers.Add(number);
    }

    public void SetCalledNumbers(IReadOnlyCollection<int> numbers)
    {
        calledNumbers.Clear();

        if (numbers == null)
            return;

        foreach (int number in numbers)
        {
            if (!calledNumbers.Contains(number))
                calledNumbers.Add(number);
        }
    }

    public void ClearCalledNumbers()
    {
        calledNumbers.Clear();
    }

    #endregion

    #region Patterns

    public void SetPatternTypes(IReadOnlyCollection<BingoPatternType> patterns)
    {
        patternTypes.Clear();

        if (patterns == null)
            return;

        foreach (BingoPatternType patternType in patterns)
        {
            if (!patternTypes.Contains(patternType))
                patternTypes.Add(patternType);
        }
    }

    #endregion

    #region Game Reset

    public void ResetGame()
    {
        calledNumbers.Clear();
        patternTypes.Clear();
        lastBingoCheckResult = null;

        bingoChecker?.ClearAllCheckHistory();
        boardController?.ClearCheckHighlights();
        boardController?.ClearMarks();
        boardController?.SetInteractable(true);

        SetBingoButtonInteractable(true);
    }

    #endregion
}