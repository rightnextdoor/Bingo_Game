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

    public void CompleteRun(int runId, bool success, string summary, string failureReason = "")
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!IsRunActive || runId != activeRunId)
        {
            return;
        }

        StressHealthReporter.instance?.CompleteRun(runId, success ? StressTestResult.Passed : StressTestResult.Failed, summary, failureReason);
        ClearActiveRun();
#endif
    }

    public void CancelRun(int runId, string summary, string reason = "User stopped the simulation.")
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!IsRunActive || runId != activeRunId)
        {
            return;
        }

        string resolvedReason = string.IsNullOrWhiteSpace(reason) ? "User stopped the simulation." : reason.Trim();
        StressHealthReporter.instance?.CompleteRun(runId, StressTestResult.Cancelled, summary, resolvedReason);
        ClearActiveRun();
#endif
    }

    #endregion

    #region Helpers

    private void ClearActiveRun()
    {
        activeRunId = 0;
        activeRunName = string.Empty;
        stopRequested = false;
        stopReason = string.Empty;
    }

    #endregion
}
