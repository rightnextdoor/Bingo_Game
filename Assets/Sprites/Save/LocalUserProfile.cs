using System;
using UnityEngine;

public static class LocalUserProfile
{
    private const string PlayerNameKey = "Bingo_PlayerName";

    public static event Action UserProfileChanged;

    public static string PlayerName => PlayerPrefs.GetString(PlayerNameKey, string.Empty);

    public static bool HasUser => !string.IsNullOrWhiteSpace(PlayerName);

    public static void SavePlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            Debug.LogWarning("Cannot save an empty player name.");
            return;
        }

        PlayerPrefs.SetString(PlayerNameKey, playerName.Trim());
        PlayerPrefs.Save();

        UserProfileChanged?.Invoke();
    }

    public static void ClearUser()
    {
        PlayerPrefs.DeleteKey(PlayerNameKey);
        PlayerPrefs.Save();

        UserProfileChanged?.Invoke();
    }
}