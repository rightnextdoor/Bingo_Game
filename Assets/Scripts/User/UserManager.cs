using System;
using System.Collections.Generic;
using UnityEngine;

public class UserManager : MonoBehaviour
{
    public static UserManager instance;

    public static event Action UserChanged;

    private UserData currentUser = new UserData();

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
        RefreshCurrentUserFromDatabase();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

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

    public void ChangeIcon(string iconId)
    {
        CurrentUser.SetIcon(iconId);

        AddOrUpdateCurrentUser();

        UserChanged?.Invoke();
    }

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

    private void AddOrUpdateCurrentUser()
    {
        if (UserDatabase.instance == null)
        {
            Debug.LogWarning("Cannot save current user because UserDatabase instance was not found.");
            return;
        }

        UserDatabase.instance.AddOrUpdateCurrentUser(CurrentUser);
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

        return UserDatabase.instance.GetUser(userId);
    }

    public List<UserData> GetAllUsers()
    {
        if (UserDatabase.instance == null)
        {
            return new List<UserData>();
        }

        return UserDatabase.instance.GetAllUsers();
    }

    public List<UserData> GetBotUsers()
    {
        if (UserDatabase.instance == null)
        {
            return new List<UserData>();
        }

        return UserDatabase.instance.GetUsersByTag(UserTag.Bot);
    }

    public List<UserData> GetPlayerUsers()
    {
        if (UserDatabase.instance == null)
        {
            return new List<UserData>();
        }

        return UserDatabase.instance.GetUsersByTag(UserTag.Player);
    }

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

        UserChanged?.Invoke();
    }

    #region Bot players

    public void AddOrUpdateUser(UserData userData)
    {
        if (userData == null)
        {
            return;
        }

        userData.RepairData();

        if (UserDatabase.instance == null)
        {
            Debug.LogWarning("Cannot add or update user because UserDatabase instance was not found.");
            return;
        }

        UserDatabase.instance.AddOrUpdateUser(userData);

        UserChanged?.Invoke();
    }

    public void CreateBotUser(BotUserEntry botEntry, string iconId)
    {
        if (botEntry == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(botEntry.UserId))
        {
            Debug.LogWarning("Cannot create bot user because bot entry userId is empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(botEntry.PlayerName))
        {
            Debug.LogWarning("Cannot create bot user because bot entry player name is empty.");
            return;
        }

        UserData botUser = new UserData();

        botUser.CreateUser(botEntry.PlayerName, iconId);
        botUser.userId = botEntry.UserId;
        botUser.userTag = UserTag.Bot;
        botUser.stats = CloneUserStats(botEntry.DefaultStats);

        AddOrUpdateUser(botUser);
    }

    public void UpdateUserName(string userId, string playerName)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(playerName))
        {
            return;
        }

        UserData user = GetUser(userId);

        if (user == null)
        {
            return;
        }

        user.playerName = playerName.Trim();

        AddOrUpdateUser(user);
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

    #endregion
}