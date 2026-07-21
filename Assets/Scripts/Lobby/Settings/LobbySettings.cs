using UnityEngine;

[DisallowMultipleComponent]
public class LobbySettings : MonoBehaviour
{
    #region Singleton / Settings

    public static LobbySettings instance;

    [Header("Lobby Players")]
    [SerializeField, Min(1)] private int minimumPlayers = 6;
    [SerializeField, Min(1)] private int unlimitedPlayerCount = 100000;

    [Header("Lobby Timers")]
    [SerializeField, Min(0f)] private float onlineTimerMinutes = 5f;
    [SerializeField, Min(0f)] private float finalCountdownSeconds = 10f;

    public int MinimumPlayers => Mathf.Max(1, minimumPlayers);
    public int UnlimitedPlayerCount => Mathf.Max(MinimumPlayers, unlimitedPlayerCount);

    public float OnlineTimerMinutes => onlineTimerMinutes;
    public float OnlineTimerSeconds => MinutesToSeconds(onlineTimerMinutes);
    public float FinalCountdownSeconds => finalCountdownSeconds;

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
