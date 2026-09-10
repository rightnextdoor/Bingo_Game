using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RiskMatchDurationSetting
{
    public BingoBallCountType ballCountType = BingoBallCountType.Ball75;
    [Min(0f)] public float durationMinutes = GameSettings.DefaultRiskMatchDurationMinutes;

    public RiskMatchDurationSetting()
    {
    }

    public RiskMatchDurationSetting(
        BingoBallCountType ballCountType,
        float durationMinutes)
    {
        this.ballCountType = ballCountType;
        this.durationMinutes = durationMinutes;
    }
}

[DisallowMultipleComponent]
public class GameSettings : MonoBehaviour
{
    public const float DefaultFirstBallCountdownSeconds = 10f;
    public const float DefaultNextBallCountdownSeconds = 10f;
    public const float DefaultBallSlideDurationSeconds = 1f;
    public const float DefaultRiskMatchDurationMinutes = 5f;
    public const float DefaultRiskMatchDurationSeconds =
        DefaultRiskMatchDurationMinutes * 60f;

    public static GameSettings instance;

    [Header("Ball Timers")]
    [SerializeField, Min(0f)] private float firstBallCountdownSeconds = DefaultFirstBallCountdownSeconds;
    [SerializeField, Min(0f)] private float nextBallCountdownSeconds = DefaultNextBallCountdownSeconds;

    [Header("Ball Display")]
    [SerializeField, Min(0f)] private float ballSlideDurationSeconds = DefaultBallSlideDurationSeconds;

    [Header("Risk")]
    [SerializeField] private List<RiskMatchDurationSetting> riskMatchDurations = new List<RiskMatchDurationSetting>();

    [Header("Score Limits")]
    [SerializeField, Min(0)] private int minimumScore = UserStats.DefaultMinimumScore;
    [SerializeField, Min(0)] private int maximumScore = UserStats.DefaultMaximumScore;

    public float FirstBallCountdownSeconds => Mathf.Max(0f, firstBallCountdownSeconds);
    public float NextBallCountdownSeconds => Mathf.Max(0f, nextBallCountdownSeconds);
    public float BallSlideDurationSeconds => Mathf.Max(0f, ballSlideDurationSeconds);
    public int MinimumScore => Mathf.Max(0, minimumScore);
    public int MaximumScore => Mathf.Max(MinimumScore, maximumScore);

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static float MinutesToSeconds(float minutes)
    {
        return Mathf.Max(0f, minutes) * 60f;
    }

    public float GetRiskMatchDurationMinutes(BingoBallCountType ballCountType)
    {
        if (riskMatchDurations != null)
        {
            for (int i = 0; i < riskMatchDurations.Count; i++)
            {
                RiskMatchDurationSetting setting = riskMatchDurations[i];

                if (setting != null && setting.ballCountType == ballCountType)
                {
                    return Mathf.Max(0f, setting.durationMinutes);
                }
            }
        }

        return DefaultRiskMatchDurationMinutes;
    }

    public float GetRiskMatchDurationSeconds(BingoBallCountType ballCountType)
    {
        return MinutesToSeconds(GetRiskMatchDurationMinutes(ballCountType));
    }
}
