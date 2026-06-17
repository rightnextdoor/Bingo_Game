using System.Collections;
using UnityEngine;

public class BotManager : MonoBehaviour, ISceneReadyCheck
{
    public static BotManager instance;
    private bool isReady;

    public string ReadyName => "Bot Manager";
    public bool IsReady => isReady;

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
            Destroy(this);
            return;
        }

        instance = this;
        isReady = false;
    }

    private IEnumerator Start()
    {
        RegisterReadyCheck();

        yield return WaitForBotSyncDependencies();

        SyncBotUsers();

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

    private IEnumerator WaitForBotSyncDependencies()
    {
        while (!CanSyncBotUsers())
        {
            yield return null;
        }

        yield return null;
    }

    private bool CanSyncBotUsers()
    {
        if (botUserListData == null)
        {
            return true;
        }

        if (SaveManager.instance == null)
        {
            return false;
        }

        if (SaveManager.instance.Data == null)
        {
            return false;
        }

        if (UserManager.instance == null)
        {
            return false;
        }

        if (!UserManager.instance.IsReady)
        {
            return false;
        }

        if (UserDatabase.instance == null)
        {
            return false;
        }

        if (UIIconManager.instance == null)
        {
            return false;
        }

        return true;
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

        if (UserDatabase.instance == null)
        {
            Debug.LogWarning("Cannot sync bot users because UserDatabase instance was not found.");
            return;
        }

        UserManager.instance.SyncBotUsers(botUserListData);
    }

}