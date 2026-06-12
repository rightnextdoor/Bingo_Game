using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BotManager : MonoBehaviour
{
    public static BotManager instance;

    [Header("Bot Data")]
    [SerializeField] private BotUserListData botUserListData;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"Duplicate BotManager found on {gameObject.name}. Removing duplicate BotManager component.");
            Destroy(this);
            return;
        }

        instance = this;
    }

    private IEnumerator Start()
    {
        yield return null;

        SyncBotUsers();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void SyncBotUsers()
    {
        if (botUserListData == null)
        {
            Debug.LogWarning("Cannot sync bot users because BotUserListData is missing.");
            return;
        }

        if (UserManager.instance == null)
        {
            Debug.LogWarning("Cannot sync bot users because UserManager instance was not found.");
            return;
        }

        botUserListData.EnsureBotUsersAreValid();

        List<UserData> savedBotUsers = UserManager.instance.GetBotUsers();

        HashSet<string> botTemplateIds = BuildBotTemplateIdSet();

        AddOrUpdateBotUsers(savedBotUsers);
        RemoveDeletedBotUsers(savedBotUsers, botTemplateIds);
    }

    private HashSet<string> BuildBotTemplateIdSet()
    {
        HashSet<string> botTemplateIds = new();

        for (int i = 0; i < botUserListData.BotUsers.Count; i++)
        {
            BotUserEntry botEntry = botUserListData.BotUsers[i];

            if (botEntry == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(botEntry.UserId))
            {
                continue;
            }

            botTemplateIds.Add(botEntry.UserId);
        }

        return botTemplateIds;
    }

    private void AddOrUpdateBotUsers(List<UserData> savedBotUsers)
    {
        for (int i = 0; i < botUserListData.BotUsers.Count; i++)
        {
            BotUserEntry botEntry = botUserListData.BotUsers[i];

            if (botEntry == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(botEntry.UserId))
            {
                continue;
            }

            UserData savedBotUser = FindSavedBotUser(savedBotUsers, botEntry.UserId);

            if (savedBotUser == null)
            {
                string randomIconId = GetRandomPlayerIconId();

                UserManager.instance.CreateBotUser(botEntry, randomIconId);
                continue;
            }

            if (savedBotUser.playerName != botEntry.PlayerName)
            {
                UserManager.instance.UpdateUserName(botEntry.UserId, botEntry.PlayerName);
            }
        }
    }

    private void RemoveDeletedBotUsers(List<UserData> savedBotUsers, HashSet<string> botTemplateIds)
    {
        for (int i = 0; i < savedBotUsers.Count; i++)
        {
            UserData savedBotUser = savedBotUsers[i];

            if (savedBotUser == null)
            {
                continue;
            }

            if (savedBotUser.userTag != UserTag.Bot)
            {
                continue;
            }

            if (botTemplateIds.Contains(savedBotUser.userId))
            {
                continue;
            }

            UserManager.instance.RemoveUser(savedBotUser.userId);
        }
    }

    private UserData FindSavedBotUser(List<UserData> savedBotUsers, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        for (int i = 0; i < savedBotUsers.Count; i++)
        {
            UserData savedBotUser = savedBotUsers[i];

            if (savedBotUser == null)
            {
                continue;
            }

            if (savedBotUser.userId == userId)
            {
                return savedBotUser;
            }
        }

        return null;
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

        int randomIndex = Random.Range(0, validIconIds.Count);

        return validIconIds[randomIndex];
    }
}