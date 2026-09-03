using System;
using UnityEngine;

[Serializable]
public class GamePlayController
{
    [SerializeField] private GamePlayPhase phase;
    [SerializeField] private GameEndReason endReason;

    [Header("Setup")]
    [SerializeField] private BingoGameModeType gameModeType;
    [SerializeField] private BingoBallCountType ballCountType;
    [SerializeField] private bool useFreeCell;
    [SerializeField] private GameBallController ballController = new GameBallController();

    [Header("Timers")]
    [SerializeField] private GamePlayTimer ballTimer = new GamePlayTimer();
    [SerializeField] private GamePlayTimer riskTimer = new GamePlayTimer();
    [SerializeField] private float firstBallCountdownSeconds;
    [SerializeField] private float nextBallCountdownSeconds;

    [SerializeField] private int ballCallRequestCount;

    public GamePlayPhase Phase => phase;
    public GameEndReason EndReason => endReason;
    public BingoGameModeType GameModeType => gameModeType;
    public BingoBallCountType BallCountType => ballCountType;
    public bool UseFreeCell => useFreeCell;
    public GameBallController BallController => ballController;
    public GamePlayTimer BallTimer => ballTimer;
    public GamePlayTimer RiskTimer => riskTimer;
    public int BallCallRequestCount => ballCallRequestCount;
    public bool IsRunning => phase == GamePlayPhase.FirstBallCountdown || phase == GamePlayPhase.NextBallCountdown;

    public GamePlayController()
    {
        Initialize(
            BingoGameModeType.Traditional,
            BingoBallCountType.Ball75,
            true,
            GameSettings.DefaultFirstBallCountdownSeconds,
            GameSettings.DefaultNextBallCountdownSeconds);
    }

    public GamePlayController(GamePlayController controller)
    {
        if (controller == null)
        {
            Initialize(
                BingoGameModeType.Traditional,
                BingoBallCountType.Ball75,
                true,
                GameSettings.DefaultFirstBallCountdownSeconds,
                GameSettings.DefaultNextBallCountdownSeconds);
            return;
        }

        phase = controller.phase;
        endReason = controller.endReason;
        gameModeType = controller.gameModeType;
        ballCountType = controller.ballCountType;
        useFreeCell = controller.useFreeCell;
        ballController = new GameBallController(controller.ballController);
        ballTimer = new GamePlayTimer(controller.ballTimer);
        riskTimer = new GamePlayTimer(controller.riskTimer);
        firstBallCountdownSeconds = controller.firstBallCountdownSeconds;
        nextBallCountdownSeconds = controller.nextBallCountdownSeconds;
        ballCallRequestCount = controller.ballCallRequestCount;
    }

    public void Initialize(
        BingoGameModeType requestedGameModeType,
        BingoBallCountType requestedBallCountType,
        bool requestedUseFreeCell,
        float requestedFirstBallCountdownSeconds,
        float requestedNextBallCountdownSeconds)
    {
        gameModeType = requestedGameModeType;
        ballCountType = requestedBallCountType;
        useFreeCell = requestedUseFreeCell;
        firstBallCountdownSeconds = Mathf.Max(0f, requestedFirstBallCountdownSeconds);
        nextBallCountdownSeconds = Mathf.Max(0f, requestedNextBallCountdownSeconds);
        phase = GamePlayPhase.WaitingForFirstPlayer;
        endReason = GameEndReason.None;
        ballCallRequestCount = 0;

        ballController ??= new GameBallController();
        ballController.Setup(ballCountType);

        ballTimer ??= new GamePlayTimer();
        riskTimer ??= new GamePlayTimer();
        ballTimer.Stop();
        riskTimer.Stop();
    }

    public bool TryStartFirstBallCountdown()
    {
        if (phase != GamePlayPhase.WaitingForFirstPlayer)
        {
            return false;
        }

        phase = GamePlayPhase.FirstBallCountdown;
        ballTimer.Start(firstBallCountdownSeconds);
        return true;
    }

    public bool UpdateBallCallLoop()
    {
        if (!IsRunning || ballTimer == null || !ballTimer.HasExpired())
        {
            return false;
        }

        CallNextBall();

        if (phase == GamePlayPhase.Ended)
        {
            return true;
        }

        phase = GamePlayPhase.NextBallCountdown;
        ballTimer.Start(nextBallCountdownSeconds);
        return true;
    }

    public bool CallNextBall()
    {
        if (!IsRunning)
        {
            return false;
        }

        ballController ??= new GameBallController();

        if (!ballController.IsInitialized || ballController.BallCountType != ballCountType)
        {
            ballController.Setup(ballCountType);
        }

        if (!ballController.TryGetNextNumber(out int calledNumber))
        {
            EndGame(GameEndReason.BallPoolExhausted);
            return false;
        }

        ballCallRequestCount++;
        return true;
    }

    public bool EndGame(GameEndReason reason)
    {
        if (phase == GamePlayPhase.Ended)
        {
            return false;
        }

        phase = GamePlayPhase.Ended;
        endReason = reason;
        ballTimer?.Stop();
        riskTimer?.Stop();
        Debug.Log("[GamePlayController] Game is over.");
        return true;
    }
}
