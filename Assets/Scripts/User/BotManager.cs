using System.Collections;
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

        UserManager.instance.SyncBotUsers(botUserListData);
    }

}