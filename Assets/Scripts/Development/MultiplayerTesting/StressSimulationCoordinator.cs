using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class StressSimulationCoordinator : MonoBehaviour
{
    #region Fields

    public static StressSimulationCoordinator instance;

    private int activeRunId;
    private string activeRunName = string.Empty;
    private bool stopRequested;
    private string stopReason = string.Empty;
    private readonly List<TrackedJoinWave> joinWaves = new List<TrackedJoinWave>();
    private readonly List<Task> activityTasks = new List<Task>();
    private bool isFinishing;
    private StressTestResult pendingResult;
    private string pendingSummary = string.Empty;
    private string pendingReason = string.Empty;

    public bool IsRunActive => activeRunId > 0;
    public int ActiveRunId => activeRunId;
    public string ActiveRunName => activeRunName;
    public bool IsStopRequested => IsRunActive && stopRequested;
    public string StopReason => stopReason;

    #endregion

    #region Unity Methods

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (IsRunActive)
        {
            StressHealthReporter.instance?.CompleteRun(activeRunId, StressTestResult.Cancelled, string.Empty, "The stress simulation coordinator was destroyed before the active test completed.");
        }
#endif

        ClearActiveRun();

        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (isFinishing)
        {
            TryFinishRun();
        }
#endif
    }

    #endregion

    #region Stress Runs

    public bool TryBeginRun(string runName, string setupSummary, out int runId, out string failureReason)
    {
        runId = 0;
        failureReason = string.Empty;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string resolvedRunName = string.IsNullOrWhiteSpace(runName) ? "Stress Test" : runName.Trim();

        if (IsRunActive)
        {
            failureReason = $"Another stress simulation is still running: {activeRunName}.";
            StressHealthReporter.instance?.ReportTestNotStarted(resolvedRunName, activeRunName, failureReason);
            return false;
        }

        if (StressHealthReporter.instance == null)
        {
            failureReason = "The stress health reporter is not ready.";
            return false;
        }

        runId = StressHealthReporter.instance.BeginRun(resolvedRunName, setupSummary);

        if (runId <= 0)
        {
            failureReason = "The stress health reporter could not start the test run.";
            return false;
        }

        activeRunId = runId;
        activeRunName = resolvedRunName;
        stopRequested = false;
        stopReason = string.Empty;
        return true;
#else
        failureReason = "Stress simulations are only available in the Editor or Development builds.";
        return false;
#endif
    }

    public bool RequestStopRun(int runId, string reason = "User stopped the simulation.")
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!IsRunActive || runId != activeRunId)
        {
            return false;
        }

        stopRequested = true;
        stopReason = string.IsNullOrWhiteSpace(reason) ? "User stopped the simulation." : reason.Trim();
        return true;
#else
        return false;
#endif
    }

    public bool IsStopRequestedFor(int runId)
    {
        return IsRunActive && runId == activeRunId && stopRequested;
    }

    public void TrackJoinWave(int _runId, int _operationId)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        StressFakePlayerManager playerManager = StressFakePlayerManager.instance;
        if (!IsRunActive || _runId != activeRunId || isFinishing || _operationId <= 0 || playerManager == null)
        {
            return;
        }

        joinWaves.Add(new TrackedJoinWave { manager = playerManager, operationId = _operationId });
#endif
    }

    public void TrackActivity(int _runId, Task _task)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (IsRunActive && _runId == activeRunId && !isFinishing && _task != null && !_task.IsCompleted)
        {
            activityTasks.Add(_task);
        }
#endif
    }

    public void CompleteRun(int runId, bool success, string summary, string failureReason = "")
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        BeginFinishRun(runId, success ? StressTestResult.Passed : StressTestResult.Failed, summary, failureReason);
#endif
    }

    public void CancelRun(int runId, string summary, string reason = "User stopped the simulation.")
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string resolvedReason = string.IsNullOrWhiteSpace(reason) ? "User stopped the simulation." : reason.Trim();
        BeginFinishRun(runId, StressTestResult.Cancelled, summary, resolvedReason);
#endif
    }

    #endregion

    #region Helpers

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void BeginFinishRun(int _runId, StressTestResult _result, string _summary, string _reason)
    {
        if (!IsRunActive || _runId != activeRunId || isFinishing)
        {
            return;
        }

        isFinishing = true;
        pendingResult = _result;
        pendingSummary = _summary;
        pendingReason = _reason;

        if (_result != StressTestResult.Passed)
        {
            for (int i = 0; i < joinWaves.Count; i++)
            {
                TrackedJoinWave wave = joinWaves[i];
                if (wave.manager != null)
                {
                    wave.manager.CancelJoinWave(wave.operationId, _reason);
                }
            }
        }

        TryFinishRun();
    }

    private void TryFinishRun()
    {
        for (int i = 0; i < joinWaves.Count; i++)
        {
            TrackedJoinWave wave = joinWaves[i];
            if (wave.manager != null && wave.manager.IsJoinWaveRunning(wave.operationId))
            {
                return;
            }
        }

        for (int i = 0; i < activityTasks.Count; i++)
        {
            if (!activityTasks[i].IsCompleted)
            {
                return;
            }
        }

        StressHealthReporter.instance?.CompleteRun(activeRunId, pendingResult, pendingSummary, pendingReason);
        ClearActiveRun();
    }
#endif

    private void ClearActiveRun()
    {
        activeRunId = 0;
        activeRunName = string.Empty;
        stopRequested = false;
        stopReason = string.Empty;
        joinWaves.Clear();
        activityTasks.Clear();
        isFinishing = false;
        pendingSummary = string.Empty;
        pendingReason = string.Empty;
    }

    private struct TrackedJoinWave
    {
        public StressFakePlayerManager manager;
        public int operationId;
    }

    #endregion
}
