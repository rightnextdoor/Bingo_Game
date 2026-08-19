using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ChatSettingsManager : MonoBehaviour, ISaveManager
{
    public static ChatSettingsManager instance;

    #region Fields

    private ChatSettingsData chatSettingsData;
    private bool hasLoadedData;

    public bool IsReady => hasLoadedData;
    public ChatSettingsData CurrentSettings => (chatSettingsData ?? new ChatSettingsData()).Clone();
    public bool IsChatEnabled => chatSettingsData == null || chatSettingsData.chatEnabled;

    public event Action<ChatSettingsData> ChatSettingsChanged;

    #endregion

    #region Unity Methods

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
    }

    private void Start()
    {
        TryLoadFromCurrentSaveData();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region ISaveManager

    public void LoadData(GameData data)
    {
        if (data == null)
        {
            return;
        }

        data.chatSettingsData ??= new ChatSettingsData();
        chatSettingsData = data.chatSettingsData.Clone();
        hasLoadedData = true;

        ChatSettingsChanged?.Invoke(chatSettingsData.Clone());
    }

    public void SaveData(ref GameData data)
    {
        if (data == null)
        {
            data = new GameData();
        }

        EnsureChatSettingsData();
        data.chatSettingsData = chatSettingsData.Clone();
    }

    bool ISaveManager.IsReady()
    {
        return hasLoadedData;
    }

    #endregion

    #region Settings

    public bool UpdateChatSettings(ChatSettingsData newSettings)
    {
        if (!hasLoadedData)
        {
            return false;
        }

        chatSettingsData = newSettings?.Clone() ?? new ChatSettingsData();
        ChatSettingsChanged?.Invoke(chatSettingsData.Clone());

        return SaveChatSettings();
    }

    public bool SaveChatSettings()
    {
        if (!hasLoadedData || SaveManager.instance == null)
        {
            return false;
        }

        SaveManager.instance.SaveGame();
        return true;
    }

    #endregion

    #region Load

    private void TryLoadFromCurrentSaveData()
    {
        if (hasLoadedData || SaveManager.instance == null || SaveManager.instance.Data == null)
        {
            return;
        }

        LoadData(SaveManager.instance.Data);
    }

    #endregion

    #region Helpers

    private void EnsureChatSettingsData()
    {
        chatSettingsData ??= new ChatSettingsData();
    }

    #endregion
}
