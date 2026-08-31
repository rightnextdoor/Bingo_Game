using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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

    #region Active Players

    public static bool TryGetActiveTestPlayerNumbers(List<int> activePlayerNumbers)
    {
        if (activePlayerNumbers == null)
        {
            return false;
        }

        activePlayerNumbers.Clear();

#if UNITY_EDITOR
        try
        {
            Type multiplayerPlaymodeType = FindEditorType("Unity.Multiplayer.PlayMode.Editor.MultiplayerPlaymode");
            PropertyInfo playersProperty = multiplayerPlaymodeType?.GetProperty(
                "Players",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Array players = playersProperty?.GetValue(null) as Array;

            if (players != null)
            {
                for (int index = 0; index < players.Length; index++)
                {
                    object player = players.GetValue(index);

                    if (player == null || !IsEditorPlayerActive(player))
                    {
                        continue;
                    }

                    int activePlayerNumber = ResolveTaggedPlayerNumber(player);

                    if (activePlayerNumber == 0 && index == 0)
                    {
                        activePlayerNumber = 1;
                    }

                    if (IsValidPlayerNumber(activePlayerNumber) && !activePlayerNumbers.Contains(activePlayerNumber))
                    {
                        activePlayerNumbers.Add(activePlayerNumber);
                    }
                }

                activePlayerNumbers.Sort();
                return activePlayerNumbers.Count > 0;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MultiplayerPlayModeTest] Active test-player discovery was unavailable: {exception.Message}");
        }
#endif

        if (IsActive)
        {
            activePlayerNumbers.Add(PlayerNumber);
        }

        return false;
    }

#if UNITY_EDITOR
    private static Type FindEditorType(string fullTypeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int i = 0; i < assemblies.Length; i++)
        {
            Type resolvedType = assemblies[i].GetType(fullTypeName, false);

            if (resolvedType != null)
            {
                return resolvedType;
            }
        }

        try
        {
            return Assembly.Load("UnityEditor.MultiplayerModule").GetType(fullTypeName, false);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsEditorPlayerActive(object player)
    {
        Type playerType = player.GetType();
        FieldInfo stateJsonField = playerType.GetField(
            "m_PlayerStateJson",
            BindingFlags.Instance | BindingFlags.NonPublic);
        object stateJson = stateJsonField?.GetValue(player);
        PropertyInfo activeProperty = stateJson?.GetType().GetProperty(
            "Active",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (activeProperty?.GetValue(stateJson) is bool isActive && isActive)
        {
            return true;
        }

        PropertyInfo playerStateProperty = playerType.GetProperty(
            "PlayerState",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        string playerState = playerStateProperty?.GetValue(player)?.ToString();

        return string.Equals(playerState, "Launching", StringComparison.Ordinal) ||
               string.Equals(playerState, "Launched", StringComparison.Ordinal);
    }

    private static int ResolveTaggedPlayerNumber(object player)
    {
        PropertyInfo tagsProperty = player.GetType().GetProperty(
            "Tags",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (!(tagsProperty?.GetValue(player) is IEnumerable tags))
        {
            return 0;
        }

        foreach (object tagValue in tags)
        {
            switch (tagValue?.ToString())
            {
                case Player1Tag:
                    return 1;

                case Player2Tag:
                    return 2;

                case Player3Tag:
                    return 3;

                case Player4Tag:
                    return 4;
            }
        }

        return 0;
    }
#endif

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
