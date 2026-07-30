using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Bingo Game/User/Bot User List", fileName = "NewBotUserListData")]
public class BotUserListData : ScriptableObject
{
    [SerializeField] private List<BotUserEntry> botUsers = new();

    public IReadOnlyList<BotUserEntry> BotUsers => botUsers;

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureBotUsersAreValid();
    }
#endif

    public void EnsureBotUsersAreValid()
    {
        for (int i = 0; i < botUsers.Count; i++)
        {
            if (botUsers[i] == null)
            {
                botUsers[i] = new BotUserEntry();
            }

            botUsers[i].EnsureValid(i + 1);
        }
    }
}

[Serializable]
public class BotUserEntry
{
    [HideInInspector][SerializeField] private string userId;
    [HideInInspector][SerializeField] private UserTag userTag = UserTag.Bot;

    [SerializeField] private string playerName;
    [SerializeField] private UserStats defaultStats = new();

    public string UserId => userId;
    public UserTag UserTag => userTag;
    public string PlayerName => playerName;
    public UserStats DefaultStats => defaultStats;

    public void EnsureValid(int playerNumber)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = Guid.NewGuid().ToString("N");
        }

        userTag = UserTag.Bot;

        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = $"Player {playerNumber}";
        }

        defaultStats ??= new UserStats();
    }
}