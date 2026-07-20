using UnityEngine;

[DisallowMultipleComponent]
public class LobbyTimerSettings : MonoBehaviour
{
    public static LobbyTimerSettings instance;

    [Header("Lobby Timers")]
    [SerializeField, Min(0f)] private float onlineTimerMinutes = 5f;
    [SerializeField, Min(0f)] private float finalCountdownSeconds = 10f;

    public float OnlineTimerMinutes => onlineTimerMinutes;
    public float OnlineTimerSeconds => MinutesToSeconds(onlineTimerMinutes);
    public float FinalCountdownSeconds => finalCountdownSeconds;

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