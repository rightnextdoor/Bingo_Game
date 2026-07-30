using System;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public class LobbyTimer
{
    [SerializeField] private bool isActive;
    [SerializeField] private double endTime;

    [SerializeField] private float onlineTimerSeconds;
    [SerializeField] private float finalCountdownSeconds;

    public bool IsActive => isActive;
    public double EndTime => endTime;

    public float OnlineTimerSeconds => onlineTimerSeconds;
    public float FinalCountdownSeconds => finalCountdownSeconds;

    public LobbyTimer()
    {
        Stop();
    }

    public void Initialize(MainMenuPlayMode playMode, float onlineSeconds, float finalSeconds)
    {
        onlineTimerSeconds = Mathf.Max(0f, onlineSeconds);
        finalCountdownSeconds = Mathf.Max(0f, finalSeconds);

        if (playMode == MainMenuPlayMode.Online)
        {
            StartOnlineTimer();
            return;
        }

        Stop();
    }

    public void StartOnlineTimer()
    {
        StartTimer(onlineTimerSeconds);
    }

    public void StartFinalCountdown()
    {
        StartTimer(finalCountdownSeconds);
    }

    public void StartTimer(float durationSeconds)
    {
        isActive = true;
        endTime = GetCurrentTime() + Mathf.Max(0f, durationSeconds);
    }

    public void Stop()
    {
        isActive = false;
        endTime = 0d;
    }

    public float GetRemainingSeconds()
    {
        if (!isActive)
        {
            return 0f;
        }

        return Mathf.Max(0f, (float)(endTime - GetCurrentTime()));
    }

    public bool HasReachedFinalCountdown()
    {
        return isActive && GetRemainingSeconds() <= finalCountdownSeconds;
    }

    public bool HasExpired()
    {
        return isActive && GetRemainingSeconds() <= 0f;
    }

    public static double GetCurrentTime()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager != null && networkManager.IsListening)
        {
            return networkManager.ServerTime.Time;
        }

        return Time.unscaledTimeAsDouble;
    }
}