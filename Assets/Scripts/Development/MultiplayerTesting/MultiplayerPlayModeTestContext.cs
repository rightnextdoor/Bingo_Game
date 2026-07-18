using UnityEngine;

#if UNITY_EDITOR
using Unity.Multiplayer.PlayMode;
#endif


#if UNITY_EDITOR

#endif

public static class MultiplayerPlayModeTestContext
{
    private const string Player1Tag = "BingoTestPlayer1";
    private const string Player2Tag = "BingoTestPlayer2";
    private const string Player3Tag = "BingoTestPlayer3";
    private const string Player4Tag = "BingoTestPlayer4";

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

    public static string UserId =>
        IsActive
            ? $"mppm-test-user-{PlayerNumber}"
            : string.Empty;

    public static string PlayerName =>
        IsActive
            ? $"Test User {PlayerNumber}"
            : string.Empty;

    public static string DirectAddress => "127.0.0.1";

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        isResolved = false;
        playerNumber = 0;
    }

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
}