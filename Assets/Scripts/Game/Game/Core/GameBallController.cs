using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameBallController
{
    [SerializeField] private BingoBallCountType ballCountType;
    [NonSerialized] private List<int> remainingNumbers = new List<int>();
    [SerializeField] private List<int> calledNumbers = new List<int>();
    [SerializeField] private int currentNumber;
    [SerializeField] private bool isInitialized;

    public BingoBallCountType BallCountType => ballCountType;
    public int CurrentNumber => currentNumber;
    public int RemainingCount => remainingNumbers?.Count ?? 0;
    public int CalledCount => calledNumbers?.Count ?? 0;
    public bool HasRemainingNumbers => RemainingCount > 0;
    public bool IsInitialized => isInitialized;
    public IReadOnlyList<int> CalledNumbers => calledNumbers;

    public GameBallController()
    {
        Setup(BingoBallCountType.Ball75);
    }

    public GameBallController(GameBallController controller)
    {
        if (controller == null)
        {
            Setup(BingoBallCountType.Ball75);
            return;
        }

        ballCountType = controller.ballCountType;
        remainingNumbers = controller.remainingNumbers != null
            ? new List<int>(controller.remainingNumbers)
            : new List<int>();
        calledNumbers = controller.calledNumbers != null
            ? new List<int>(controller.calledNumbers)
            : new List<int>();
        currentNumber = controller.currentNumber;
        isInitialized = controller.isInitialized;
    }

    public void Setup(BingoBallCountType requestedBallCountType)
    {
        ballCountType = requestedBallCountType;
        currentNumber = 0;

        remainingNumbers ??= new List<int>();
        calledNumbers ??= new List<int>();

        remainingNumbers.Clear();
        calledNumbers.Clear();

        int totalBallCount = Mathf.Max(0, (int)ballCountType);

        for (int number = 1; number <= totalBallCount; number++)
        {
            remainingNumbers.Add(number);
        }

        isInitialized = totalBallCount > 0;
    }

    public bool TryGetNextNumber(out int number)
    {
        number = 0;

        if (!isInitialized || remainingNumbers == null || remainingNumbers.Count == 0)
        {
            return false;
        }

        int randomIndex = UnityEngine.Random.Range(0, remainingNumbers.Count);
        number = remainingNumbers[randomIndex];

        remainingNumbers.RemoveAt(randomIndex);

        calledNumbers ??= new List<int>();
        calledNumbers.Add(number);
        currentNumber = number;
        return true;
    }

    public List<int> GetCalledNumbersSnapshot()
    {
        return calledNumbers != null
            ? new List<int>(calledNumbers)
            : new List<int>();
    }
}
