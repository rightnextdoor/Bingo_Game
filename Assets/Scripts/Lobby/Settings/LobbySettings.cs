using UnityEngine;

[DisallowMultipleComponent]
public class LobbySettings : MonoBehaviour
{
    #region Singleton / Settings

    public static LobbySettings instance;

    [Header("Lobby Players")]
    [SerializeField, Range(3, 10)] private int minimumPlayers = 6;
    [SerializeField, Range(10, 500)] private int maxPlayerCount = 500;

    [Header("Lobby Timers")]
    [SerializeField, Min(0f)] private float onlineTimerMinutes = 5f;
    [SerializeField, Min(0f)] private float finalCountdownSeconds = 10f;
    [SerializeField, Min(0f)] private float joinLockSeconds = 10f;
    [SerializeField, Min(1f)] private float pendingJoinTimeoutSeconds = 60f;

    [Header("Online Bots")]
    [SerializeField, Min(0)] private int maxOnlineBots = 30;

    public int MinimumPlayers => Mathf.Max(1, minimumPlayers);
    public int MaxPlayerCount => Mathf.Max(MinimumPlayers, maxPlayerCount);

    public float OnlineTimerMinutes => onlineTimerMinutes;
    public float OnlineTimerSeconds => MinutesToSeconds(onlineTimerMinutes);
    public float FinalCountdownSeconds => finalCountdownSeconds;
    public float JoinLockSeconds => Mathf.Max(0f, joinLockSeconds);
    public float PendingJoinTimeoutSeconds => Mathf.Max(1f, pendingJoinTimeoutSeconds);
    public int MaxOnlineBots => Mathf.Max(0, maxOnlineBots);

    #endregion

    #region Unity Methods

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

    #endregion

    #region Helpers

    public static float MinutesToSeconds(float minutes)
    {
        return Mathf.Max(0f, minutes) * 60f;
    }

    #endregion
}
