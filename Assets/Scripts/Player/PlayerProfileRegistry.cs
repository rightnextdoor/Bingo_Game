using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerProfileRegistry : MonoBehaviour
{
    public static PlayerProfileRegistry instance;

    private readonly Dictionary<string, PlayerProfileData> profilesByUserId = new Dictionary<string, PlayerProfileData>(StringComparer.Ordinal);
    private readonly HashSet<string> lobbyProfileUserIds = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, PlayerProfileData> Profiles => profilesByUserId;

    public event Action<PlayerProfileData> ProfileChanged;
    public event Action ProfilesChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnEnable()
    {
        UserManager.UserChanged += OnLocalUserChanged;
        NetworkPlayerProfileConnection.LocalProfileUpdateReceived += OnNetworkProfileUpdateReceived;
    }

    private void Start()
    {
        SyncCurrentLocalUser();
    }

    private void OnDisable()
    {
        UserManager.UserChanged -= OnLocalUserChanged;
        NetworkPlayerProfileConnection.LocalProfileUpdateReceived -= OnNetworkProfileUpdateReceived;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public bool TryGetProfile(string userId, out PlayerProfileData profile)
    {
        profile = null;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return profilesByUserId.TryGetValue(userId.Trim(), out profile);
    }

    public PlayerProfileData GetProfile(string userId)
    {
        return TryGetProfile(userId, out PlayerProfileData profile) ? profile : null;
    }

    public bool SetProfile(PlayerProfileData profile)
    {
        if (profile == null || !profile.IsValid)
        {
            return false;
        }

        PlayerProfileData normalizedProfile = new PlayerProfileData(profile.userId, profile.playerName, profile.iconId);

        if (profilesByUserId.TryGetValue(normalizedProfile.userId, out PlayerProfileData existingProfile) && existingProfile.Matches(normalizedProfile))
        {
            return false;
        }

        profilesByUserId[normalizedProfile.userId] = normalizedProfile;
        ProfileChanged?.Invoke(normalizedProfile.Clone());
        ProfilesChanged?.Invoke();
        return true;
    }

    public void SyncFromLobbyView(LobbyViewData lobbyViewData)
    {
        HashSet<string> currentLobbyUserIds = new HashSet<string>(StringComparer.Ordinal);
        bool collectionChanged = false;

        if (lobbyViewData?.players != null)
        {
            for (int i = 0; i < lobbyViewData.players.Count; i++)
            {
                LobbyPlayerViewData playerData = lobbyViewData.players[i];

                if (playerData == null || string.IsNullOrWhiteSpace(playerData.userId))
                {
                    continue;
                }

                string userId = playerData.userId.Trim();
                currentLobbyUserIds.Add(userId);

                PlayerProfileData profile = GetProfileFromLobbyPlayer(playerData);

                if (SetProfileWithoutCollectionEvent(profile))
                {
                    collectionChanged = true;
                }
            }
        }

        List<string> removedUserIds = new List<string>();

        foreach (string previousUserId in lobbyProfileUserIds)
        {
            if (!currentLobbyUserIds.Contains(previousUserId))
            {
                removedUserIds.Add(previousUserId);
            }
        }

        for (int i = 0; i < removedUserIds.Count; i++)
        {
            string userId = removedUserIds[i];

            if (lobbyProfileUserIds.Remove(userId))
            {
                collectionChanged = true;
            }

            if (!IsCurrentLocalUser(userId) && profilesByUserId.Remove(userId))
            {
                collectionChanged = true;
            }
        }

        foreach (string userId in currentLobbyUserIds)
        {
            if (lobbyProfileUserIds.Add(userId))
            {
                collectionChanged = true;
            }
        }

        if (collectionChanged)
        {
            ProfilesChanged?.Invoke();
        }
    }

    public void ClearLobbyProfiles()
    {
        bool collectionChanged = false;
        List<string> lobbyUserIds = new List<string>(lobbyProfileUserIds);

        for (int i = 0; i < lobbyUserIds.Count; i++)
        {
            string userId = lobbyUserIds[i];

            if (!IsCurrentLocalUser(userId) && profilesByUserId.Remove(userId))
            {
                collectionChanged = true;
            }
        }

        if (lobbyProfileUserIds.Count > 0)
        {
            lobbyProfileUserIds.Clear();
            collectionChanged = true;
        }

        if (collectionChanged)
        {
            ProfilesChanged?.Invoke();
        }
    }

    public List<PlayerProfileData> GetLobbyProfiles()
    {
        List<PlayerProfileData> profiles = new List<PlayerProfileData>(lobbyProfileUserIds.Count);

        foreach (string userId in lobbyProfileUserIds)
        {
            if (profilesByUserId.TryGetValue(userId, out PlayerProfileData profile) && profile != null)
            {
                profiles.Add(profile);
            }
        }

        return profiles;
    }

    public List<PlayerProfileData> GetProfiles(IReadOnlyList<string> userIds)
    {
        List<PlayerProfileData> profiles = new List<PlayerProfileData>();

        if (userIds == null)
        {
            return profiles;
        }

        for (int i = 0; i < userIds.Count; i++)
        {
            if (TryGetProfile(userIds[i], out PlayerProfileData profile))
            {
                profiles.Add(profile);
            }
        }

        return profiles;
    }

    private void OnLocalUserChanged()
    {
        SyncCurrentLocalUser();
    }

    private void SyncCurrentLocalUser()
    {
        UserData userData = UserManager.instance?.CurrentUser;

        if (userData == null || !userData.HasUser || userData.userTag == UserTag.Bot)
        {
            return;
        }

        SetProfile(new PlayerProfileData(userData));
    }

    private void OnNetworkProfileUpdateReceived(PlayerProfileData profile)
    {
        SetProfile(profile);
    }

    private PlayerProfileData GetProfileFromLobbyPlayer(LobbyPlayerViewData playerData)
    {
        if (playerData == null)
        {
            return null;
        }

        if (IsCurrentLocalUser(playerData.userId))
        {
            UserData currentUser = UserManager.instance?.CurrentUser;

            if (currentUser != null && currentUser.HasUser)
            {
                return new PlayerProfileData(currentUser);
            }
        }

        return new PlayerProfileData(playerData);
    }

    private bool SetProfileWithoutCollectionEvent(PlayerProfileData profile)
    {
        if (profile == null || !profile.IsValid)
        {
            return false;
        }

        PlayerProfileData normalizedProfile = new PlayerProfileData(profile.userId, profile.playerName, profile.iconId);

        if (profilesByUserId.TryGetValue(normalizedProfile.userId, out PlayerProfileData existingProfile) && existingProfile.Matches(normalizedProfile))
        {
            return false;
        }

        profilesByUserId[normalizedProfile.userId] = normalizedProfile;
        ProfileChanged?.Invoke(normalizedProfile.Clone());
        return true;
    }

    private bool IsCurrentLocalUser(string userId)
    {
        return !string.IsNullOrWhiteSpace(userId) &&
               UserManager.instance != null &&
               UserManager.instance.HasUser &&
               string.Equals(UserManager.instance.UserId, userId, StringComparison.Ordinal);
    }
}
