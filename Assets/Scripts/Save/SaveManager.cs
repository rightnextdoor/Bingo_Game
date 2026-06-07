using System;
using System.Collections.Generic;
using System.Linq;
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

    public GameData Data => gameData;

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
            Debug.LogWarning($"Duplicate SaveManager found on {gameObject.name}. Removing duplicate SaveManager component.");
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

        dataHandler.Delete();

        gameData = new GameData();

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

        gameData = dataHandler.Load();

        if (gameData == null)
        {
            Debug.Log("No saved data found. Creating default save data.");
            NewGame();
        }

        LoadAllSaveManagers();

        SaveDataChanged?.Invoke();
    }

    public void SaveGame()
    {
        if (gameData == null)
        {
            gameData = new GameData();
        }

        SaveAllSaveManagers();

        EnsureDataHandler();

        dataHandler.Save(gameData);

        SaveDataChanged?.Invoke();
    }

    public bool HasSavedData()
    {
        EnsureDataHandler();

        return dataHandler.Load() != null;
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
            SaveGame();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Save failed on quit: {exception.Message}");
        }
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
}