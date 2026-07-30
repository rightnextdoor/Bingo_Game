using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class GameSimulationController : MonoBehaviour
{
    #region Fields

    private const int FreeCellIndex = 12;

    [Header("References")]
    [SerializeField] private LobbyBoardSectionController lobbyBoardSectionController;
    [SerializeField] private GameController gameController;
    [SerializeField] private BingoBoardController boardController;
    [SerializeField] private BingoPatternValidator validator;

    [Header("Simulation")]
    [SerializeField] private bool enableSimulation;
    [SerializeField] private List<BingoPatternType> patternTypes = new List<BingoPatternType>();
    [SerializeField] private bool simulateWinning = true;
    [SerializeField] private bool gameOver;

    [Header("Reset")]
    [SerializeField] private bool resetSimulation;

    private bool isSimulationRunning;

    public bool EnableSimulation => enableSimulation;
    public bool IsSimulationRunning => isSimulationRunning;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (lobbyBoardSectionController != null)
            lobbyBoardSectionController.ReadyRequested += OnReadyRequested;

        if (gameController != null)
            gameController.BingoCheckResolved += OnBingoCheckResolved;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (lobbyBoardSectionController != null)
            lobbyBoardSectionController.ReadyRequested -= OnReadyRequested;

        if (gameController != null)
            gameController.BingoCheckResolved -= OnBingoCheckResolved;
#endif
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!resetSimulation)
            return;

        resetSimulation = false;
        ResetSimulation();
#endif
    }

    #endregion

    #region Ready Simulation

    private void OnReadyRequested()
    {
        if (!enableSimulation || isSimulationRunning)
            return;

        RunSimulation();
    }

    private void RunSimulation()
    {
        if (!CanRunSimulation())
            return;

        List<BingoPatternType> simulationPatterns = GetSimulationPatterns();

        if (simulationPatterns.Count == 0)
        {
            Debug.LogWarning("[GameSimulation] Add at least one Bingo pattern.");
            return;
        }

        LobbyBoardData boardData = boardController.CurrentBoardData;

        if (!TryBuildSimulationCells(boardData, simulationPatterns, out List<int> simulationCells))
            return;

        boardController.SetMarkedCells(simulationCells);

        List<int> markedCellIndices = boardController.GetMarkedCellIndicesSnapshot();

        if (!TryBuildCalledNumbers(
                boardData,
                markedCellIndices,
                simulationPatterns,
                out List<int> simulatedCalledNumbers))
        {
            return;
        }

        gameController.SetPatternTypes(simulationPatterns);
        gameController.SetCalledNumbers(simulatedCalledNumbers);

        isSimulationRunning = true;

        if (gameController.RequestBingo())
            return;

        isSimulationRunning = false;
    }

    private bool CanRunSimulation()
    {
        if (gameController == null)
        {
            Debug.LogWarning("[GameSimulation] GameController is missing.");
            return false;
        }

        if (boardController == null || boardController.CurrentBoardData == null)
        {
            Debug.LogWarning("[GameSimulation] Bingo board is not ready.");
            return false;
        }

        if (validator == null)
        {
            Debug.LogWarning("[GameSimulation] BingoPatternValidator is missing.");
            return false;
        }

        return true;
    }

    #endregion

    #region Pattern Setup

    private List<BingoPatternType> GetSimulationPatterns()
    {
        List<BingoPatternType> simulationPatterns = new List<BingoPatternType>();

        for (int i = 0; i < patternTypes.Count; i++)
        {
            BingoPatternType patternType = patternTypes[i];

            if (!simulationPatterns.Contains(patternType))
                simulationPatterns.Add(patternType);
        }

        return simulationPatterns;
    }

    private bool TryBuildSimulationCells(
        LobbyBoardData boardData,
        IReadOnlyCollection<BingoPatternType> simulationPatterns,
        out List<int> simulationCells)
    {
        simulationCells = new List<int>();

        if (boardData == null || simulationPatterns == null)
            return false;

        HashSet<int> uniqueCells = new HashSet<int>();

        foreach (BingoPatternType patternType in simulationPatterns)
        {
            IReadOnlyList<int> patternCells = validator.GetSimulationPatternCells(patternType);

            if (patternCells == null || patternCells.Count == 0)
            {
                Debug.LogWarning($"[GameSimulation] No test layout was found for {patternType}.");
                return false;
            }

            for (int i = 0; i < patternCells.Count; i++)
            {
                int cellIndex = patternCells[i];

                if (!IsValidCellIndex(boardData, cellIndex))
                {
                    Debug.LogWarning($"[GameSimulation] Pattern {patternType} contains invalid cell index {cellIndex}.");
                    return false;
                }

                uniqueCells.Add(cellIndex);
            }
        }

        simulationCells.AddRange(uniqueCells);
        simulationCells.Sort();

        return simulationCells.Count > 0;
    }

    #endregion

    #region Called Number Simulation

    private bool TryBuildCalledNumbers(
        LobbyBoardData boardData,
        IReadOnlyCollection<int> markedCellIndices,
        IReadOnlyCollection<BingoPatternType> simulationPatterns,
        out List<int> simulatedCalledNumbers)
    {
        if (simulateWinning)
        {
            simulatedCalledNumbers = BuildCalledNumbers(boardData, markedCellIndices, null);
            return true;
        }

        return TryBuildFailedCalledNumbers(
            boardData,
            markedCellIndices,
            simulationPatterns,
            out simulatedCalledNumbers);
    }

    private bool TryBuildFailedCalledNumbers(
        LobbyBoardData boardData,
        IReadOnlyCollection<int> markedCellIndices,
        IReadOnlyCollection<BingoPatternType> simulationPatterns,
        out List<int> simulatedCalledNumbers)
    {
        simulatedCalledNumbers = new List<int>();

        List<int> failureCandidates = GetFailureCandidates(boardData, markedCellIndices);

        if (failureCandidates.Count == 0)
        {
            Debug.LogWarning("[GameSimulation] No non-Free cells are available to fail.");
            return false;
        }

        Shuffle(failureCandidates);

        HashSet<int> failedCellIndices = new HashSet<int>();

        int initialFailureCount = UnityEngine.Random.Range(1, failureCandidates.Count + 1);

        for (int i = 0; i < initialFailureCount; i++)
            failedCellIndices.Add(failureCandidates[i]);

        int nextFailureIndex = initialFailureCount;

        simulatedCalledNumbers =
            BuildCalledNumbers(
                boardData,
                markedCellIndices,
                failedCellIndices);

        while (DoesSimulationStillWin(
                   boardData,
                   markedCellIndices,
                   simulatedCalledNumbers,
                   simulationPatterns))
        {
            if (nextFailureIndex >= failureCandidates.Count)
            {
                Debug.LogWarning("[GameSimulation] Could not create a fully failed Bingo simulation.");
                return false;
            }

            failedCellIndices.Add(failureCandidates[nextFailureIndex]);
            nextFailureIndex++;

            simulatedCalledNumbers =
                BuildCalledNumbers(
                    boardData,
                    markedCellIndices,
                    failedCellIndices);
        }

        return true;
    }

    private bool DoesSimulationStillWin(
        LobbyBoardData boardData,
        IReadOnlyCollection<int> markedCellIndices,
        IReadOnlyCollection<int> simulatedCalledNumbers,
        IReadOnlyCollection<BingoPatternType> simulationPatterns)
    {
        BingoCheckResult previewResult =
            validator.Validate(
                "SimulationFailurePreview",
                boardData,
                markedCellIndices,
                simulatedCalledNumbers,
                simulationPatterns,
                Array.Empty<BingoCheckResult>());

        return previewResult != null && previewResult.HasWinningPattern;
    }

    private List<int> BuildCalledNumbers(
        LobbyBoardData boardData,
        IReadOnlyCollection<int> markedCellIndices,
        IReadOnlyCollection<int> failedCellIndices)
    {
        List<int> simulatedCalledNumbers = new List<int>();
        int fakeNumber = -1;

        foreach (int cellIndex in markedCellIndices)
        {
            if (!IsValidCellIndex(boardData, cellIndex))
                continue;

            if (IsFreeCell(boardData, cellIndex))
                continue;

            if (failedCellIndices != null && failedCellIndices.Contains(cellIndex))
            {
                simulatedCalledNumbers.Add(fakeNumber);
                fakeNumber--;
                continue;
            }

            int number = boardData.cellNumbers[cellIndex];

            if (!simulatedCalledNumbers.Contains(number))
                simulatedCalledNumbers.Add(number);
        }

        return simulatedCalledNumbers;
    }

    private List<int> GetFailureCandidates(
        LobbyBoardData boardData,
        IReadOnlyCollection<int> markedCellIndices)
    {
        List<int> failureCandidates = new List<int>();

        foreach (int cellIndex in markedCellIndices)
        {
            if (!IsValidCellIndex(boardData, cellIndex) ||
                IsFreeCell(boardData, cellIndex) ||
                failureCandidates.Contains(cellIndex))
            {
                continue;
            }

            failureCandidates.Add(cellIndex);
        }

        return failureCandidates;
    }

    #endregion

    #region Check Result

    private void OnBingoCheckResolved(BingoCheckResult checkResult)
    {
        if (!enableSimulation || !isSimulationRunning || checkResult == null)
            return;

        isSimulationRunning = false;

        if (!checkResult.HasWinningPattern)
        {
            gameController.FinishPlayerAsLoser();
            return;
        }

        if (gameOver)
        {
            gameController.FinishPlayerAsWinner();
            return;
        }

        gameController.ContinuePlayerAfterBingoCheck();
    }

    #endregion

    #region Reset

    public void ResetSimulation()
    {
        isSimulationRunning = false;

        if (gameController != null)
        {
            gameController.ResetGame();
            return;
        }

        boardController?.ClearCheckHighlights();
        boardController?.ClearMarks();
        boardController?.SetInteractable(true);
    }

    #endregion

    #region Helpers

    private bool IsFreeCell(LobbyBoardData boardData, int cellIndex)
    {
        return boardData != null &&
               boardData.usesFreeCell &&
               cellIndex == FreeCellIndex;
    }

    private bool IsValidCellIndex(LobbyBoardData boardData, int cellIndex)
    {
        return boardData?.cellNumbers != null &&
               cellIndex >= 0 &&
               cellIndex < boardData.cellNumbers.Count;
    }

    private void Shuffle(List<int> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);

            int value = values[i];
            values[i] = values[randomIndex];
            values[randomIndex] = value;
        }
    }

    #endregion
}