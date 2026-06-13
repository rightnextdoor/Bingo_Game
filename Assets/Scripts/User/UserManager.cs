using System;
using System.Collections.Generic;
using UnityEngine;

public class UserManager : MonoBehaviour, ISceneReadyCheck
{
    public static UserManager instance;
    private bool isReady;

    public static event Action UserChanged;

    private UserData currentUser = new UserData();

    private const int MinBotPlayerNameCharacters = 3;
    private const int MaxBotPlayerNameCharacters = 20;

    public UserData CurrentUser
    {
        get
        {
            if (currentUser == null)
            {
                currentUser = new UserData();
            }

            currentUser.RepairData();
            return currentUser;
        }
    }

    public string ReadyName => "User Manager";
    public bool IsReady => isReady;

    public bool HasUser => CurrentUser.HasUser;

    public string PlayerName => CurrentUser.playerName;

    public string UserId => CurrentUser.userId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
        UserChanged = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"Duplicate UserManager found on {gameObject.name}. Removing duplicate UserManager component.");
            Destroy(this);
            return;
        }

        instance = this;
        isReady = false;
    }

    private void OnEnable()
    {
        UserDatabase.UserDatabaseChanged += RefreshCurrentUserFromDatabase;
    }

    private void OnDisable()
    {
        UserDatabase.UserDatabaseChanged -= RefreshCurrentUserFromDatabase;
    }

    private void Start()
    {
        RegisterReadyCheck();

        RefreshCurrentUserFromDatabase();

        isReady = true;
    }

    private void OnDestroy()
    {
        UnregisterReadyCheck();

        if (instance == this)
        {
            instance = null;
        }
    }

    #region Ready Check

    private void RegisterReadyCheck()
    {
        if (SceneReadyController.instance == null)
        {
            return;
        }

        SceneReadyController.instance.RegisterReadyCheck(this, true);
    }

    private void UnregisterReadyCheck()
    {
        if (SceneReadyController.instance == null)
        {
            return;
        }

        SceneReadyController.instance.UnregisterReadyCheck(this);
    }

    #endregion

    #region Users

    public void CreateUser(string playerName)
    {
        CreateUser(playerName, string.Empty);
    }

    public void CreateUser(string playerName, string iconId)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            Debug.LogWarning("Cannot create user with an empty player name.");
            return;
        }

        CurrentUser.CreateUser(playerName, iconId);
        CurrentUser.userTag = UserTag.Player;

        AddOrUpdateCurrentUser();

        UserChanged?.Invoke();
    }

    public void ChangePlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return;
        }

        CurrentUser.playerName = playerName.Trim();

        AddOrUpdateCurrentUser();

        UserChanged?.Invoke();
    }

    private void AddOrUpdateCurrentUser()
    {
        if (UserDatabase.instance == null)
        {
            Debug.LogWarning("Cannot save current user because UserDatabase instance was not found.");
            return;
        }

        UserDatabase.instance.AddOrUpdateCurrentUser(CurrentUser);
    }

    public void AddOrUpdateUser(UserData userData)
    {
        if (userData == null)
        {
            return;
        }

        userData.RepairData();
        RepairUserIconIfMissing(userData);

        if (userData.userTag == UserTag.Bot)
        {
            userData.playerName = GetValidBotPlayerName(userData.playerName, userData.userId);
        }

        if (UserDatabase.instance == null)
        {
            Debug.LogWarning("Cannot add or update user because UserDatabase instance was not found.");
            return;
        }

        UserDatabase.instance.AddOrUpdateUser(userData);

        UserChanged?.Invoke();
    }

    public void RemoveUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        if (UserDatabase.instance == null)
        {
            Debug.LogWarning("Cannot remove user because UserDatabase instance was not found.");
            return;
        }

        UserDatabase.instance.RemoveUser(userId);

        if (CurrentUser.userId == userId)
        {
            currentUser = new UserData();
        }

        UserChanged?.Invoke();
    }

    public UserData GetUser(string userId)
    {
        if (UserDatabase.instance == null)
        {
            return null;
        }

        UserData user = UserDatabase.instance.GetUser(userId);

        if (RepairUserIconIfMissing(user))
        {
            UserChanged?.Invoke();
        }

        return user;
    }

    public List<UserData> GetAllUsers()
    {
        if (UserDatabase.instance == null)
        {
            return new List<UserData>();
        }

        List<UserData> users = UserDatabase.instance.GetAllUsers();

        RepairUserIconsIfMissing(users);

        return users;
    }

    public List<UserData> GetBotUsers()
    {
        if (UserDatabase.instance == null)
        {
            return new List<UserData>();
        }

        List<UserData> users = UserDatabase.instance.GetUsersByTag(UserTag.Bot);

        RepairUserIconsIfMissing(users);

        return users;
    }

    public List<UserData> GetPlayerUsers()
    {
        if (UserDatabase.instance == null)
        {
            return new List<UserData>();
        }

        List<UserData> users = UserDatabase.instance.GetUsersByTag(UserTag.Player);

        RepairUserIconsIfMissing(users);

        return users;
    }

    #endregion

    #region Icons

    public void ChangeIcon(string iconId)
    {
        CurrentUser.SetIcon(iconId);

        RepairUserIconIfMissing(CurrentUser);

        AddOrUpdateCurrentUser();

        UserChanged?.Invoke();
    }

    public bool RepairUserIconIfMissing(UserData userData)
    {
        return RepairUserIconIfMissing(userData, true);
    }

    private bool RepairUserIconIfMissing(UserData userData, bool saveAfterRepair)
    {
        if (userData == null || UIIconManager.instance == null)
        {
            return false;
        }

        if (UIIconManager.instance.HasValidPlayerIconId(userData.iconId))
        {
            return false;
        }

        string fallbackIconId = UIIconManager.instance.GetFirstPlayerIconId();

        if (string.IsNullOrWhiteSpace(fallbackIconId))
        {
            return false;
        }

        userData.SetIcon(fallbackIconId);

        if (saveAfterRepair && UserDatabase.instance != null)
        {
            UserDatabase.instance.AddOrUpdateUser(userData);
        }

        return true;
    }

    private void RepairUserIconsIfMissing(List<UserData> users)
    {
        if (users == null)
        {
            return;
        }

        bool repairedAnyIcon = false;

        for (int i = 0; i < users.Count; i++)
        {
            if (RepairUserIconIfMissing(users[i]))
            {
                repairedAnyIcon = true;
            }
        }

        if (repairedAnyIcon)
        {
            UserChanged?.Invoke();
        }
    }

    private string GetRandomPlayerIconId()
    {
        if (UIIconManager.instance == null)
        {
            return string.Empty;
        }

        IReadOnlyList<UserIconData> playerIcons = UIIconManager.instance.PlayerIcons;

        if (playerIcons == null || playerIcons.Count == 0)
        {
            return string.Empty;
        }

        List<string> validIconIds = new();

        for (int i = 0; i < playerIcons.Count; i++)
        {
            UserIconData iconData = playerIcons[i];

            if (iconData == null)
            {
                continue;
            }

            if (!iconData.IsValid())
            {
                continue;
            }

            validIconIds.Add(iconData.IconId);
        }

        if (validIconIds.Count == 0)
        {
            return string.Empty;
        }

        int randomIndex = UnityEngine.Random.Range(0, validIconIds.Count);

        return validIconIds[randomIndex];
    }

    #endregion

    #region Game Info
    public void SetLastGameId(string gameId)
    {
        CurrentUser.lastGameId = string.IsNullOrWhiteSpace(gameId) ? string.Empty : gameId.Trim();

        AddOrUpdateCurrentUser();

        UserChanged?.Invoke();
    }

    public void ClearLastGameId()
    {
        CurrentUser.lastGameId = string.Empty;

        AddOrUpdateCurrentUser();

        UserChanged?.Invoke();
    }

    public void AddPoints(int amount)
    {
        CurrentUser.stats.AddPoints(amount);

        AddOrUpdateCurrentUser();

        UserChanged?.Invoke();
    }

    public void RemovePoints(int amount)
    {
        CurrentUser.stats.RemovePoints(amount);

        AddOrUpdateCurrentUser();

        UserChanged?.Invoke();
    }

    #endregion

    #region Database

    private void RefreshCurrentUserFromDatabase()
    {
        if (UserDatabase.instance == null)
        {
            return;
        }

        UserData savedCurrentUser = UserDatabase.instance.GetCurrentUser();

        if (savedCurrentUser == null)
        {
            currentUser = new UserData();
            UserChanged?.Invoke();
            return;
        }

        currentUser = savedCurrentUser;
        currentUser.RepairData();

        RepairUserIconIfMissing(currentUser);

        UserChanged?.Invoke();
    }

    #endregion

    #region Bot players

    public void SyncBotUsers(BotUserListData botUserListData)
    {
        if (botUserListData == null)
        {
            Debug.LogWarning("Cannot sync bot users because BotUserListData is missing.");
            return;
        }

        if (UserDatabase.instance == null)
        {
            Debug.LogWarning("Cannot sync bot users because UserDatabase instance was not found.");
            return;
        }

        botUserListData.EnsureBotUsersAreValid();

        List<UserData> savedBotUsers = UserDatabase.instance.GetUsersByTag(UserTag.Bot);

        Dictionary<string, UserData> savedBotUserLookup = new();
        HashSet<string> botTemplateIdSet = new();

        for (int i = 0; i < savedBotUsers.Count; i++)
        {
            UserData savedBotUser = savedBotUsers[i];

            if (savedBotUser == null || string.IsNullOrWhiteSpace(savedBotUser.userId))
            {
                continue;
            }

            savedBotUserLookup[savedBotUser.userId] = savedBotUser;
        }

        List<UserData> usersToAddOrUpdate = new();
        List<string> userIdsToRemove = new();

        IReadOnlyList<BotUserEntry> botEntries = botUserListData.BotUsers;

        for (int i = 0; i < botEntries.Count; i++)
        {
            BotUserEntry botEntry = botEntries[i];

            if (botEntry == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(botEntry.UserId))
            {
                continue;
            }


            string botUserId = botEntry.UserId.Trim();
            string botPlayerName = GetValidBotPlayerName(botEntry.PlayerName, botUserId);

            botTemplateIdSet.Add(botUserId);

            if (!savedBotUserLookup.TryGetValue(botUserId, out UserData savedBotUser))
            {
                UserData newBotUser = CreateBotUserData(botEntry, botPlayerName, GetRandomPlayerIconId());

                RepairUserIconIfMissing(newBotUser, false);

                usersToAddOrUpdate.Add(newBotUser);
                continue;
            }

            bool botChanged = false;

            savedBotUser.RepairData();

            if (savedBotUser.userTag != UserTag.Bot)
            {
                savedBotUser.userTag = UserTag.Bot;
                botChanged = true;
            }

            if (savedBotUser.playerName != botPlayerName)
            {
                savedBotUser.playerName = botPlayerName;
                botChanged = true;
            }

            if (RepairUserIconIfMissing(savedBotUser, false))
            {
                botChanged = true;
            }

            if (botChanged)
            {
                usersToAddOrUpdate.Add(savedBotUser);
            }
        }

        for (int i = 0; i < savedBotUsers.Count; i++)
        {
            UserData savedBotUser = savedBotUsers[i];

            if (savedBotUser == null || string.IsNullOrWhiteSpace(savedBotUser.userId))
            {
                continue;
            }

            if (botTemplateIdSet.Contains(savedBotUser.userId))
            {
                continue;
            }

            userIdsToRemove.Add(savedBotUser.userId);
        }

        UserDatabase.instance.ApplyBotUserSync(usersToAddOrUpdate, userIdsToRemove);
    }

    private UserStats CloneUserStats(UserStats source)
    {
        if (source == null)
        {
            return new UserStats();
        }

        string json = JsonUtility.ToJson(source);
        UserStats clone = JsonUtility.FromJson<UserStats>(json);

        return clone ?? new UserStats();
    }

    private UserData CreateBotUserData(BotUserEntry botEntry, string botPlayerName, string iconId)
    {
        UserData botUser = new UserData();

        botUser.CreateUser(botPlayerName, iconId);
        botUser.userId = botEntry.UserId;
        botUser.userTag = UserTag.Bot;
        botUser.stats = CloneUserStats(botEntry.DefaultStats);

        botUser.RepairData();
        botUser.playerName = GetValidBotPlayerName(botUser.playerName, botUser.userId);

        return botUser;
    }

    private string GetValidBotPlayerName(string playerName, string userId)
    {
        string validName = string.IsNullOrWhiteSpace(playerName) ? "Bot" : playerName.Trim();

        if (validName.Length > MaxBotPlayerNameCharacters)
        {
            validName = validName.Substring(0, MaxBotPlayerNameCharacters);
        }

        while (validName.Length < MinBotPlayerNameCharacters)
        {
            validName += GetStableBotNameDigit(userId, validName.Length);
        }

        return validName;
    }

    private int GetStableBotNameDigit(string userId, int digitIndex)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return UnityEngine.Random.Range(0, 10);
        }

        userId = userId.Trim();

        int charIndex = Mathf.Abs(digitIndex) % userId.Length;
        int digit = Mathf.Abs(userId[charIndex]) % 10;

        return digit;
    }

    #endregion
}