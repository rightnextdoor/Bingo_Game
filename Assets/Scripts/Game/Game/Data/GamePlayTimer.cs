using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class SessionPauseManager
{
    private const double ClockResyncToleranceSeconds = 0.01d;

    private static bool isGameplayPaused;
    private static bool isEditorPaused;
    private static bool isClockRecoveringFromPause;
    private static double pauseStartedWallTime;
    private static double frozenCurrentTime;
    private static double resumeWallTime;
    private static double resumeCurrentTime;
    private static double accumulatedPausedSeconds;
    private static float timeScaleBeforeGameplayPause = 1f;

    public static event Action<bool> PauseChanged;

    public static bool IsPaused => isGameplayPaused || isEditorPaused;
    public static bool IsGameplayPaused => isGameplayPaused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        if (isGameplayPaused && Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = Mathf.Approximately(timeScaleBeforeGameplayPause, 0f)
                ? 1f
                : timeScaleBeforeGameplayPause;
        }

        isGameplayPaused = false;
        isEditorPaused = false;
        isClockRecoveringFromPause = false;
        pauseStartedWallTime = 0d;
        frozenCurrentTime = 0d;
        resumeWallTime = 0d;
        resumeCurrentTime = 0d;
        accumulatedPausedSeconds = 0d;
        timeScaleBeforeGameplayPause = 1f;
        PauseChanged = null;
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void RegisterEditorPauseHandler()
    {
        EditorApplication.pauseStateChanged -= HandleEditorPauseStateChanged;
        EditorApplication.pauseStateChanged += HandleEditorPauseStateChanged;
    }

    private static void HandleEditorPauseStateChanged(PauseState pauseState)
    {
        SetEditorPaused(
            EditorApplication.isPlaying && pauseState == PauseState.Paused);
    }
#endif

    public static void SetGameplayPaused(bool paused)
    {
        if (isGameplayPaused == paused)
        {
            return;
        }

        if (paused)
        {
            timeScaleBeforeGameplayPause = Time.timeScale;
            SetPauseState(ref isGameplayPaused, true);
            Time.timeScale = 0f;
            return;
        }

        SetPauseState(ref isGameplayPaused, false);
        Time.timeScale = Mathf.Approximately(timeScaleBeforeGameplayPause, 0f)
            ? 1f
            : timeScaleBeforeGameplayPause;
    }

    public static double GetCurrentTime()
    {
        if (IsPaused)
        {
            return frozenCurrentTime;
        }

        double adjustedTime = GetRawTime() - accumulatedPausedSeconds;

        if (!isClockRecoveringFromPause)
        {
            return adjustedTime;
        }

        double smoothResumeTime =
            resumeCurrentTime + Math.Max(0d, GetWallTime() - resumeWallTime);

        if (Math.Abs(adjustedTime - smoothResumeTime) <= ClockResyncToleranceSeconds)
        {
            isClockRecoveringFromPause = false;
            return adjustedTime;
        }

        return smoothResumeTime;
    }

    public static IEnumerator WaitForSeconds(float durationSeconds)
    {
        double endTime = GetCurrentTime() + Mathf.Max(0f, durationSeconds);

        while (GetCurrentTime() < endTime)
        {
            yield return null;
        }
    }

    private static void SetEditorPaused(bool paused)
    {
        SetPauseState(ref isEditorPaused, paused);
    }

    private static void SetPauseState(ref bool pauseField, bool paused)
    {
        if (pauseField == paused)
        {
            return;
        }

        bool wasPaused = IsPaused;
        double currentTime = GetCurrentTime();
        double wallTime = GetWallTime();
        pauseField = paused;
        bool isPaused = IsPaused;

        if (!wasPaused && isPaused)
        {
            frozenCurrentTime = currentTime;
            pauseStartedWallTime = wallTime;
            isClockRecoveringFromPause = false;
        }
        else if (wasPaused && !isPaused)
        {
            accumulatedPausedSeconds += Math.Max(0d, wallTime - pauseStartedWallTime);
            resumeCurrentTime = frozenCurrentTime;
            resumeWallTime = wallTime;
            pauseStartedWallTime = 0d;
            isClockRecoveringFromPause = true;
        }

        if (wasPaused != isPaused)
        {
            PauseChanged?.Invoke(isPaused);
        }
    }

    private static double GetRawTime()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager != null && networkManager.IsListening)
        {
            return networkManager.ServerTime.Time;
        }

        return Time.unscaledTimeAsDouble;
    }

    private static double GetWallTime()
    {
#if UNITY_EDITOR
        return EditorApplication.timeSinceStartup;
#else
        return Time.realtimeSinceStartupAsDouble;
#endif
    }
}

[Serializable]
public class GamePlayTimer
{
    [SerializeField] private bool isActive;
    [SerializeField] private double endTime;

    public bool IsActive => isActive;
    public double EndTime => endTime;

    public GamePlayTimer()
    {
        Stop();
    }

    public GamePlayTimer(GamePlayTimer timer) : this()
    {
        if (timer == null)
        {
            return;
        }

        isActive = timer.isActive;
        endTime = timer.endTime;
    }

    public void Start(float durationSeconds)
    {
        isActive = true;
        endTime = GetCurrentTime() + Mathf.Max(0f, durationSeconds);
    }

    public void Stop()
    {
        isActive = false;
        endTime = 0d;
    }

    public void ApplyNetworkState(bool active, double authoritativeEndTime)
    {
        isActive = active;
        endTime = active ? authoritativeEndTime : 0d;
    }

    public float GetRemainingSeconds()
    {
        if (!isActive)
        {
            return 0f;
        }

        return Mathf.Max(0f, (float)(endTime - GetCurrentTime()));
    }

    public bool HasExpired()
    {
        return isActive && GetRemainingSeconds() <= 0f;
    }

    public static double GetCurrentTime()
    {
        return SessionPauseManager.GetCurrentTime();
    }
}
