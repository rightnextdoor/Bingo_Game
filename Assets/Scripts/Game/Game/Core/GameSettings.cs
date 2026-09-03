using UnityEngine;

[DisallowMultipleComponent]
public class GameSettings : MonoBehaviour
{
    public const float DefaultFirstBallCountdownSeconds = 10f;
    public const float DefaultNextBallCountdownSeconds = 10f;

    public static GameSettings instance;

    [Header("Ball Timers")]
    [SerializeField, Min(0f)] private float firstBallCountdownSeconds = DefaultFirstBallCountdownSeconds;
    [SerializeField, Min(0f)] private float nextBallCountdownSeconds = DefaultNextBallCountdownSeconds;

    public float FirstBallCountdownSeconds => Mathf.Max(0f, firstBallCountdownSeconds);
    public float NextBallCountdownSeconds => Mathf.Max(0f, nextBallCountdownSeconds);

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
}
