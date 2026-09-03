using System;
using Unity.Netcode;
using UnityEngine;

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
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager != null && networkManager.IsListening)
        {
            return networkManager.ServerTime.Time;
        }

        return Time.unscaledTimeAsDouble;
    }
}
