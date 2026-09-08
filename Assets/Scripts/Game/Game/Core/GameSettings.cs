using UnityEngine;

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
    [SerializeField, Min(0f)] private float riskMatchDurationMinutes = DefaultRiskMatchDurationMinutes;

    [Header("Score Limits")]
    [SerializeField, Min(0)] private int minimumScore = UserStats.DefaultMinimumScore;
    [SerializeField, Min(0)] private int maximumScore = UserStats.DefaultMaximumScore;

    public float FirstBallCountdownSeconds => Mathf.Max(0f, firstBallCountdownSeconds);
    public float NextBallCountdownSeconds => Mathf.Max(0f, nextBallCountdownSeconds);
    public float BallSlideDurationSeconds => Mathf.Max(0f, ballSlideDurationSeconds);
    public float RiskMatchDurationMinutes => Mathf.Max(0f, riskMatchDurationMinutes);
    public float RiskMatchDurationSeconds => MinutesToSeconds(riskMatchDurationMinutes);
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
}
