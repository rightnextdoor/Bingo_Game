using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;

    public static event Action SaveDataChanged;

    [Header("Save Settings")]
    [SerializeField] private string fileName = "bingo_save.json";
    [SerializeField] private bool encryptData = true;
    [SerializeField] private bool saveOnQuit = true;

    private GameData gameData;
    private FileDataHandler dataHandler;
    private List<ISaveManager> saveManagers = new List<ISaveManager>();

    private Coroutine activeLoadRoutine;
    private Coroutine activeSaveRoutine;

    private bool isLoading;
    private bool hasLoadedData;

    private bool isSaving;
    private bool hasPendingSave;
    private bool saveRequestedDuringLoad;

    private string pendingSaveJson;

    public GameData Data => gameData;

    public bool IsLoading => isLoading;
    public bool IsSaving => isSaving;
    public bool HasLoadedData => hasLoadedData;
    public bool HasPendingSave => hasPendingSave;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
        SaveDataChanged = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;

        EnsureDataHandler();
    }

    private void Start()
    {
        LoadGame();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    [ContextMenu("Delete save file")]
    public void DeleteSavedData()
    {
        EnsureDataHandler();

        hasPendingSave = false;
        pendingSaveJson = null;
        saveRequestedDuringLoad = false;

        if (activeLoadRoutine != null)
        {
            StopCoroutine(activeLoadRoutine);
            activeLoadRoutine = null;
        }

        isLoading = false;

        dataHandler.Delete();

        gameData = new GameData();
        hasLoadedData = true;

        LoadAllSaveManagers();

        SaveDataChanged?.Invoke();

        Debug.Log("Deleted save file and reset runtime data back to default.");
    }

    public void NewGame()
    {
        gameData = new GameData();

        Debug.Log("Created new default Bingo save data.");
    }

    public void LoadGame()
    {
        EnsureDataHandler();

        if (activeLoadRoutine != null)
        {
            StopCoroutine(activeLoadRoutine);
        }

        activeLoadRoutine = StartCoroutine(LoadGameRoutine());
    }

    public void SaveGame()
    {
        if (isLoading && !hasLoadedData)
        {
            saveRequestedDuringLoad = true;
            return;
        }

        SaveGameInternal(false);
    }

    public void SaveGameImmediate()
    {
        if (isLoading && !hasLoadedData)
        {
            return;
        }

        SaveGameInternal(true);
    }

    public bool HasSavedData()
    {
        EnsureDataHandler();

        return dataHandler.Exists();
    }

    private void OnApplicationQuit()
    {
        if (!saveOnQuit)
        {
            return;
        }

        if (gameData == null)
        {
            return;
        }

        try
        {
            SaveGameImmediate();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Save failed on quit: {exception.Message}");
        }
    }

    private IEnumerator LoadGameRoutine()
    {
        isLoading = true;
        hasLoadedData = false;

        Task<string> loadTask = dataHandler.LoadJsonAsync();

        while (!loadTask.IsCompleted)
        {
            yield return null;
        }

        string loadedJson = null;

        if (loadTask.IsFaulted)
        {
            LogTaskException("Background load", loadTask);
        }
        else
        {
            loadedJson = loadTask.Result;
        }

        if (string.IsNullOrEmpty(loadedJson))
        {
            Debug.Log("No saved data found. Creating default save data.");
            NewGame();
        }
        else
        {
            try
            {
                gameData = JsonUtility.FromJson<GameData>(loadedJson);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Save file could not be parsed. Creating default save data. {exception.Message}");
                gameData = null;
            }

            if (gameData == null)
            {
                NewGame();
            }
        }

        LoadAllSaveManagers();

        isLoading = false;
        hasLoadedData = true;
        activeLoadRoutine = null;

        SaveDataChanged?.Invoke();

        if (saveRequestedDuringLoad)
        {
            saveRequestedDuringLoad = false;
            SaveGame();
        }
    }

    private void SaveGameInternal(bool immediate)
    {
        if (gameData == null)
        {
            gameData = new GameData();
        }

        SaveAllSaveManagers();

        EnsureDataHandler();

        string jsonSnapshot = JsonUtility.ToJson(gameData, true);

        if (immediate)
        {
            hasPendingSave = false;
            pendingSaveJson = null;

            dataHandler.SaveJson(jsonSnapshot);
        }
        else
        {
            QueueBackgroundSave(jsonSnapshot);
        }

        SaveDataChanged?.Invoke();
    }

    private void QueueBackgroundSave(string jsonSnapshot)
    {
        if (!Application.isPlaying)
        {
            dataHandler.SaveJson(jsonSnapshot);
            return;
        }

        if (isSaving)
        {
            pendingSaveJson = jsonSnapshot;
            hasPendingSave = true;
            return;
        }

        activeSaveRoutine = StartCoroutine(SaveJsonQueueRoutine(jsonSnapshot));
    }

    private IEnumerator SaveJsonQueueRoutine(string jsonToSave)
    {
        isSaving = true;

        while (true)
        {
            Task saveTask = dataHandler.SaveJsonAsync(jsonToSave);

            while (!saveTask.IsCompleted)
            {
                yield return null;
            }

            if (saveTask.IsFaulted)
            {
                LogTaskException("Background save", saveTask);
            }

            if (!hasPendingSave)
            {
                break;
            }

            jsonToSave = pendingSaveJson;
            pendingSaveJson = null;
            hasPendingSave = false;
        }

        isSaving = false;
        activeSaveRoutine = null;
    }

    private void LoadAllSaveManagers()
    {
        saveManagers = FindAllSaveManagers();

        foreach (ISaveManager saveManager in saveManagers)
        {
            try
            {
                saveManager?.LoadData(gameData);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"LoadData failed for {saveManager?.GetType().Name}: {exception.Message}");
            }
        }
    }

    private void SaveAllSaveManagers()
    {
        saveManagers = FindAllSaveManagers();

        foreach (ISaveManager saveManager in saveManagers)
        {
            try
            {
                if (saveManager != null && saveManager.IsReady())
                {
                    saveManager.SaveData(ref gameData);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"SaveData failed for {saveManager?.GetType().Name}: {exception.Message}");
            }
        }
    }

    private void EnsureDataHandler()
    {
        if (dataHandler != null)
        {
            return;
        }

        dataHandler = new FileDataHandler(Application.persistentDataPath, fileName, encryptData);
    }

    private List<ISaveManager> FindAllSaveManagers()
    {
#if UNITY_2023_1_OR_NEWER
        return FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OfType<ISaveManager>()
            .ToList();
#else
        return FindObjectsOfType<MonoBehaviour>(true)
            .OfType<ISaveManager>()
            .ToList();
#endif
    }

    private void LogTaskException(string taskName, Task task)
    {
        if (task == null || task.Exception == null)
        {
            return;
        }

        Exception exception = task.Exception.GetBaseException();

        Debug.LogWarning($"{taskName} failed: {exception.Message}");
    }
}