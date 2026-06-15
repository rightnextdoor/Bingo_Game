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

        yield return null;

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