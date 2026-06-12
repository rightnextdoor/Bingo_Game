using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class UserDatabaseData
{
    public string currentUserId = string.Empty;
    public List<UserData> users = new();
}

public class UserDatabase : MonoBehaviour, ISaveManager
{
    public static UserDatabase instance;

    public static event Action UserDatabaseChanged;

    private UserDatabaseData databaseData = new();

    public IReadOnlyList<UserData> Users => databaseData.users;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
        UserDatabaseChanged = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"Duplicate UserDatabase found on {gameObject.name}. Removing duplicate UserDatabase component.");
            Destroy(this);
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

    public UserData GetCurrentUser()
    {
        EnsureDatabaseData();

        if (string.IsNullOrWhiteSpace(databaseData.currentUserId))
        {
            return null;
        }

        return GetUser(databaseData.currentUserId);
    }

    public void AddOrUpdateCurrentUser(UserData userData)
    {
        if (userData == null)
        {
            return;
        }

        userData.RepairData();

        if (string.IsNullOrWhiteSpace(userData.userId))
        {
            return;
        }

        EnsureDatabaseData();

        databaseData.currentUserId = userData.userId;

        int userIndex = FindUserIndex(userData.userId);

        if (userIndex >= 0)
        {
            databaseData.users[userIndex] = userData;
        }
        else
        {
            databaseData.users.Add(userData);
        }

        UserDatabaseChanged?.Invoke();
        SaveDatabase();
    }

    public void SetCurrentUser(string userId)
    {
        EnsureDatabaseData();

        if (string.IsNullOrWhiteSpace(userId))
        {
            databaseData.currentUserId = string.Empty;
        }
        else
        {
            databaseData.currentUserId = userId.Trim();
        }

        UserDatabaseChanged?.Invoke();
        SaveDatabase();
    }

    public void AddOrUpdateUser(UserData userData)
    {
        AddOrUpdateUser(userData, true);
    }

    public void AddOrUpdateUser(UserData userData, bool saveAfterChange)
    {
        if (userData == null)
        {
            Debug.LogWarning("Cannot add or update a null user.");
            return;
        }

        userData.RepairData();

        if (string.IsNullOrWhiteSpace(userData.userId))
        {
            Debug.LogWarning("Cannot add or update user because userId is empty.");
            return;
        }

        EnsureDatabaseData();

        int userIndex = FindUserIndex(userData.userId);

        if (userIndex >= 0)
        {
            databaseData.users[userIndex] = userData;
        }
        else
        {
            databaseData.users.Add(userData);
        }

        UserDatabaseChanged?.Invoke();

        if (saveAfterChange)
        {
            SaveDatabase();
        }
    }

    public void RemoveUser(string userId)
    {
        RemoveUser(userId, true);
    }

    public void RemoveUser(string userId, bool saveAfterChange)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        EnsureDatabaseData();

        int userIndex = FindUserIndex(userId);

        if (userIndex < 0)
        {
            return;
        }

        databaseData.users.RemoveAt(userIndex);

        if (databaseData.currentUserId == userId)
        {
            databaseData.currentUserId = string.Empty;
        }

        UserDatabaseChanged?.Invoke();

        if (saveAfterChange)
        {
            SaveDatabase();
        }
    }

    public UserData GetUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        EnsureDatabaseData();

        for (int i = 0; i < databaseData.users.Count; i++)
        {
            UserData user = databaseData.users[i];

            if (user == null)
            {
                continue;
            }

            if (user.userId == userId)
            {
                return user;
            }
        }

        return null;
    }

    public List<UserData> GetUsersByTag(UserTag userTag)
    {
        EnsureDatabaseData();

        List<UserData> usersWithTag = new();

        for (int i = 0; i < databaseData.users.Count; i++)
        {
            UserData user = databaseData.users[i];

            if (user == null)
            {
                continue;
            }

            if (user.userTag == userTag)
            {
                usersWithTag.Add(user);
            }
        }

        return usersWithTag;
    }

    public List<UserData> GetAllUsers()
    {
        EnsureDatabaseData();

        List<UserData> users = new();

        for (int i = 0; i < databaseData.users.Count; i++)
        {
            UserData user = databaseData.users[i];

            if (user == null)
            {
                continue;
            }

            users.Add(user);
        }

        return users;
    }

    public void SaveDatabase()
    {
        if (SaveManager.instance == null)
        {
            Debug.LogWarning("Cannot save user database because SaveManager instance was not found.");
            return;
        }

        SaveManager.instance.SaveGame();
    }

    public void LoadData(GameData data)
    {
        if (data.userDatabase == null)
        {
            data.userDatabase = new UserDatabaseData();
        }

        databaseData = data.userDatabase;

        RepairDatabaseData();

        UserDatabaseChanged?.Invoke();
    }

    public void SaveData(ref GameData data)
    {
        if (data.userDatabase == null)
        {
            data.userDatabase = new UserDatabaseData();
        }

        EnsureDatabaseData();
        RepairDatabaseData();

        data.userDatabase = databaseData;
    }

    private void EnsureDatabaseData()
    {
        if (databaseData == null)
        {
            databaseData = new UserDatabaseData();
        }

        databaseData.users ??= new List<UserData>();
    }

    private void RepairDatabaseData()
    {
        EnsureDatabaseData();

        for (int i = databaseData.users.Count - 1; i >= 0; i--)
        {
            UserData user = databaseData.users[i];

            if (user == null)
            {
                databaseData.users.RemoveAt(i);
                continue;
            }

            user.RepairData();

            if (string.IsNullOrWhiteSpace(user.userId))
            {
                databaseData.users.RemoveAt(i);
            }
        }

        RemoveDuplicateUsers();
    }

    private void RemoveDuplicateUsers()
    {
        HashSet<string> usedUserIds = new();

        for (int i = databaseData.users.Count - 1; i >= 0; i--)
        {
            UserData user = databaseData.users[i];

            if (user == null || string.IsNullOrWhiteSpace(user.userId))
            {
                databaseData.users.RemoveAt(i);
                continue;
            }

            if (usedUserIds.Contains(user.userId))
            {
                databaseData.users.RemoveAt(i);
                continue;
            }

            usedUserIds.Add(user.userId);
        }
    }

    private int FindUserIndex(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return -1;
        }

        EnsureDatabaseData();

        for (int i = 0; i < databaseData.users.Count; i++)
        {
            UserData user = databaseData.users[i];

            if (user == null)
            {
                continue;
            }

            if (user.userId == userId)
            {
                return i;
            }
        }

        return -1;
    }


}