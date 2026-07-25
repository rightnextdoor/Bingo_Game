using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkBotManager : MonoBehaviour
{
    public static NetworkBotManager instance;

    private readonly List<UserData> botTemplates = new List<UserData>();

    private bool isReady;
    private NetworkRoot networkRoot;

    public bool IsReady => isReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        networkRoot = GetComponentInParent<NetworkRoot>();

        if (networkRoot == null || !networkRoot.IsPrimaryInstance)
        {
            enabled = false;
            return;
        }

        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }

        instance = this;
        isReady = false;
    }

    private IEnumerator Start()
    {
        while (BotManager.instance == null || !BotManager.instance.IsReady)
        {
            yield return null;
        }

        RebuildBotTemplates();

        isReady = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public IReadOnlyList<UserData> CreateBotCandidateCopies()
    {
        List<UserData> copies = new List<UserData>();

        for (int i = 0; i < botTemplates.Count; i++)
        {
            UserData copy = CloneUserData(botTemplates[i]);

            if (copy != null && copy.HasUser)
            {
                copies.Add(copy);
            }
        }

        return copies;
    }

    private void RebuildBotTemplates()
    {
        botTemplates.Clear();

        IReadOnlyList<UserData> localBots = BotManager.instance.GetLocalBotUsers();

        for (int i = 0; i < localBots.Count; i++)
        {
            UserData botUser = localBots[i];

            if (botUser == null || !botUser.HasUser || botUser.userTag != UserTag.Bot)
            {
                continue;
            }

            UserData template = CloneUserData(botUser);

            if (template != null)
            {
                botTemplates.Add(template);
            }
        }
    }

    private UserData CloneUserData(UserData source)
    {
        if (source == null)
        {
            return null;
        }

        string json = JsonUtility.ToJson(source);
        UserData clone = JsonUtility.FromJson<UserData>(json);

        return clone;
    }
}