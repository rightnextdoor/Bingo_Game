using System;
using System.Collections.Generic;

public static class PlayerDisplayIdentityResolver
{
    private const int DefaultVisibleUserIdCharacters = 4;

    public static string GetDisplayName(PlayerProfileData profile, IReadOnlyList<PlayerProfileData> relevantProfiles)
    {
        if (profile == null)
        {
            return "Player";
        }

        string playerName = string.IsNullOrWhiteSpace(profile.playerName) ? "Player" : profile.playerName.Trim();
        string userId = profile.userId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return playerName;
        }

        int visibleCharacterCount = GetRequiredUserIdCharacterCount(profile, relevantProfiles);
        string visibleUserId = userId.Length <= visibleCharacterCount ? userId : userId.Substring(0, visibleCharacterCount);
        return $"{playerName} #{visibleUserId}";
    }

    public static int GetRequiredUserIdCharacterCount(PlayerProfileData profile, IReadOnlyList<PlayerProfileData> relevantProfiles)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.userId))
        {
            return 0;
        }

        string userId = profile.userId.Trim();
        int visibleCharacterCount = Math.Min(DefaultVisibleUserIdCharacters, userId.Length);

        if (relevantProfiles == null || relevantProfiles.Count == 0)
        {
            return visibleCharacterCount;
        }

        while (visibleCharacterCount < userId.Length && HasDisplayCollision(profile, relevantProfiles, visibleCharacterCount))
        {
            visibleCharacterCount++;
        }

        return visibleCharacterCount;
    }

    private static bool HasDisplayCollision(PlayerProfileData profile, IReadOnlyList<PlayerProfileData> relevantProfiles, int visibleCharacterCount)
    {
        string profileName = profile.playerName?.Trim() ?? string.Empty;
        string profileUserId = profile.userId?.Trim() ?? string.Empty;
        string profilePrefix = GetUserIdPrefix(profileUserId, visibleCharacterCount);

        for (int i = 0; i < relevantProfiles.Count; i++)
        {
            PlayerProfileData other = relevantProfiles[i];

            if (other == null || string.IsNullOrWhiteSpace(other.userId) || string.Equals(other.userId, profileUserId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(other.playerName?.Trim(), profileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string otherPrefix = GetUserIdPrefix(other.userId.Trim(), visibleCharacterCount);

            if (string.Equals(otherPrefix, profilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetUserIdPrefix(string userId, int characterCount)
    {
        if (string.IsNullOrWhiteSpace(userId) || characterCount <= 0)
        {
            return string.Empty;
        }

        return userId.Length <= characterCount ? userId : userId.Substring(0, characterCount);
    }
}
