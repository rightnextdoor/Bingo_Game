using System;
using UnityEngine;

public class UserManager : MonoBehaviour, ISaveManager
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

        SaveManager.instance?.SaveGame();

        UserChanged?.Invoke();

        Debug.Log($"Created user: {CurrentUser.playerName} / {CurrentUser.userId} / Icon: {CurrentUser.iconId}");
    }


    public void ChangePlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return;
        }

        CurrentUser.playerName = playerName.Trim();

        SaveManager.instance?.SaveGame();

        UserChanged?.Invoke();
    }


    public void ChangeIcon(string iconId)
    {
        CurrentUser.SetIcon(iconId);

        SaveManager.instance?.SaveGame();

        UserChanged?.Invoke();

        Debug.Log($"Changed user icon to: {CurrentUser.iconId}");
    }

    public void SetLastGameId(string gameId)
    {
        CurrentUser.lastGameId = string.IsNullOrWhiteSpace(gameId) ? string.Empty : gameId.Trim();

        SaveManager.instance?.SaveGame();

        UserChanged?.Invoke();
    }

    public void ClearLastGameId()
    {
        CurrentUser.lastGameId = string.Empty;

        SaveManager.instance?.SaveGame();

        UserChanged?.Invoke();
    }

    public void AddPoints(int amount)
    {
        CurrentUser.stats.AddPoints(amount);

        SaveManager.instance?.SaveGame();

        UserChanged?.Invoke();
    }

    public void RemovePoints(int amount)
    {
        CurrentUser.stats.RemovePoints(amount);

        SaveManager.instance?.SaveGame();

        UserChanged?.Invoke();
    }

    public void LoadData(GameData data)
    {
        if (data.userData == null)
        {
            data.userData = new UserData();
        }

        currentUser = data.userData;
        currentUser.RepairData();

        UserChanged?.Invoke();
    }

    public void SaveData(ref GameData data)
    {
        if (data.userData == null)
        {
            data.userData = new UserData();
        }

        CurrentUser.RepairData();

        data.userData = CurrentUser;
    }
}