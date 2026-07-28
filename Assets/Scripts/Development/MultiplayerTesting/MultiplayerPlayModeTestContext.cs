using UnityEngine;

#if UNITY_EDITOR
using Unity.Multiplayer.PlayMode;
#endif

public static class MultiplayerPlayModeTestContext
{
    #region Fields

    private const string Player1Tag = "BingoTestPlayer1";
    private const string Player2Tag = "BingoTestPlayer2";
    private const string Player3Tag = "BingoTestPlayer3";
    private const string Player4Tag = "BingoTestPlayer4";

    private const int MinimumPlayerNumber = 1;
    private const int MaximumPlayerNumber = 4;

    private static bool isResolved;
    private static int playerNumber;

    public static bool IsActive
    {
        get
        {
            Resolve();
            return playerNumber > 0;
        }
    }

    public static int PlayerNumber
    {
        get
        {
            Resolve();
            return playerNumber;
        }
    }

    public static bool IsHost => PlayerNumber == 1;
    public static string UserId => GetUserId(PlayerNumber);
    public static string PlayerName => GetPlayerName(PlayerNumber);
    public static string DirectAddress => "127.0.0.1";

    #endregion

    #region Runtime

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        isResolved = false;
        playerNumber = 0;
    }

    #endregion

    #region Player Identity

    public static string GetUserId(int targetPlayerNumber)
    {
        if (!IsValidPlayerNumber(targetPlayerNumber))
        {
            return string.Empty;
        }

        return $"mppm-test-user-{targetPlayerNumber}";
    }

    public static string GetPlayerName(int targetPlayerNumber)
    {
        if (!IsValidPlayerNumber(targetPlayerNumber))
        {
            return string.Empty;
        }

        return $"Test User {targetPlayerNumber}";
    }

    private static bool IsValidPlayerNumber(int targetPlayerNumber)
    {
        return targetPlayerNumber >= MinimumPlayerNumber && targetPlayerNumber <= MaximumPlayerNumber;
    }

    #endregion

    #region Resolution

    private static void Resolve()
    {
        if (isResolved)
        {
            return;
        }

        isResolved = true;
        playerNumber = 0;

#if UNITY_EDITOR
        foreach (string tag in CurrentPlayer.Tags)
        {
            switch (tag)
            {
                case Player1Tag:
                    playerNumber = 1;
                    return;

                case Player2Tag:
                    playerNumber = 2;
                    return;

                case Player3Tag:
                    playerNumber = 3;
                    return;

                case Player4Tag:
                    playerNumber = 4;
                    return;
            }
        }
#endif
    }

    #endregion
}