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
    public const float DefaultAutomaticMarkDelayMinimumSeconds = 0.35f;
    public const float DefaultAutomaticMarkDelayMaximumSeconds = 1.25f;
    public const float DefaultAutomaticBingoDelayMinimumSeconds = 0.25f;
    public const float DefaultAutomaticBingoDelayMaximumSeconds = 0.75f;
    public const int DefaultAutomaticMarkPulseCount = 2;
    public const float DefaultAutomaticMarkPulseSeconds = 0.15f;
    public const int DefaultDeathFrozenPlayerOutEligibleCount = 1;
    public const float DefaultRiskMatchDurationMinutes = 5f;
    public const float DefaultRiskMatchDurationSeconds =
        DefaultRiskMatchDurationMinutes * 60f;

    public static GameSettings instance;

    [Header("Ball Timers")]
    [SerializeField, Min(0f)] private float firstBallCountdownSeconds = DefaultFirstBallCountdownSeconds;
    [SerializeField, Min(0f)] private float nextBallCountdownSeconds = DefaultNextBallCountdownSeconds;

    [Header("Ball Display")]
    [SerializeField, Min(0f)] private float ballSlideDurationSeconds = DefaultBallSlideDurationSeconds;

    [Header("Automatic Board")]
    [SerializeField, Min(0f)] private float automaticMarkDelayMinimumSeconds = DefaultAutomaticMarkDelayMinimumSeconds;
    [SerializeField, Min(0f)] private float automaticMarkDelayMaximumSeconds = DefaultAutomaticMarkDelayMaximumSeconds;
    [SerializeField, Min(0f)] private float automaticBingoDelayMinimumSeconds = DefaultAutomaticBingoDelayMinimumSeconds;
    [SerializeField, Min(0f)] private float automaticBingoDelayMaximumSeconds = DefaultAutomaticBingoDelayMaximumSeconds;
    [SerializeField, Min(1)] private int automaticMarkPulseCount = DefaultAutomaticMarkPulseCount;
    [SerializeField, Min(0f)] private float automaticMarkPulseSeconds = DefaultAutomaticMarkPulseSeconds;

    [Header("Death")]
    [SerializeField, Min(0)] private int deathFrozenPlayerOutEligibleCount = DefaultDeathFrozenPlayerOutEligibleCount;

    [Header("Risk")]
    [SerializeField] private List<RiskMatchDurationSetting> riskMatchDurations = new List<RiskMatchDurationSetting>();

    [Header("Score Limits")]
    [SerializeField, Min(0)] private int minimumScore = UserStats.DefaultMinimumScore;
    [SerializeField, Min(0)] private int maximumScore = UserStats.DefaultMaximumScore;

    public float FirstBallCountdownSeconds => Mathf.Max(0f, firstBallCountdownSeconds);
    public float NextBallCountdownSeconds => Mathf.Max(0f, nextBallCountdownSeconds);
    public float BallSlideDurationSeconds => Mathf.Max(0f, ballSlideDurationSeconds);
    public float AutomaticMarkDelayMinimumSeconds => Mathf.Max(0f, automaticMarkDelayMinimumSeconds);
    public float AutomaticMarkDelayMaximumSeconds => Mathf.Max(AutomaticMarkDelayMinimumSeconds, automaticMarkDelayMaximumSeconds);
    public float AutomaticBingoDelayMinimumSeconds => Mathf.Max(0f, automaticBingoDelayMinimumSeconds);
    public float AutomaticBingoDelayMaximumSeconds => Mathf.Max(AutomaticBingoDelayMinimumSeconds, automaticBingoDelayMaximumSeconds);
    public int AutomaticMarkPulseCount => Mathf.Max(1, automaticMarkPulseCount);
    public float AutomaticMarkPulseSeconds => Mathf.Max(0f, automaticMarkPulseSeconds);
    public int DeathFrozenPlayerOutEligibleCount => Mathf.Max(0, deathFrozenPlayerOutEligibleCount);
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
