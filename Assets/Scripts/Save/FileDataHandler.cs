using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class FileDataHandler
{
    private readonly string dataDirPath;
    private readonly string dataFileName;

    private readonly bool encryptData;
    private readonly string codeWord = "bingo-save-key-2026";

    private readonly object fileLock = new object();

    public FileDataHandler(string dataDirPath, string dataFileName, bool encryptData)
    {
        this.dataDirPath = dataDirPath;
        this.dataFileName = dataFileName;
        this.encryptData = encryptData;
    }

    public void Save(GameData data)
    {
        try
        {
            string jsonData = JsonUtility.ToJson(data, true);
            SaveJson(jsonData);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Error trying to save data.\n{exception}");
        }
    }

    public Task SaveJsonAsync(string jsonData)
    {
        return Task.Run(() => SaveJson(jsonData));
    }

    public void SaveJson(string jsonData)
    {
        string fullPath = GetFullPath();
        string tempPath = $"{fullPath}.tmp";

        lock (fileLock)
        {
            Directory.CreateDirectory(dataDirPath);

            string dataToStore = jsonData;

            if (encryptData)
            {
                dataToStore = EncryptDecrypt(dataToStore);
            }

            File.WriteAllText(tempPath, dataToStore);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            File.Move(tempPath, fullPath);
        }
    }

    public GameData Load()
    {
        try
        {
            string jsonData = LoadJson();

            if (string.IsNullOrEmpty(jsonData))
            {
                return null;
            }

            return JsonUtility.FromJson<GameData>(jsonData);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Error trying to load data.\n{exception}");
            return null;
        }
    }

    public Task<string> LoadJsonAsync()
    {
        return Task.Run(LoadJson);
    }

    public string LoadJson()
    {
        string fullPath = GetFullPath();

        lock (fileLock)
        {
            if (!File.Exists(fullPath))
            {
                return null;
            }

            string dataToLoad = File.ReadAllText(fullPath);

            if (encryptData)
            {
                dataToLoad = EncryptDecrypt(dataToLoad);
            }

            return dataToLoad;
        }
    }

    public bool Exists()
    {
        string fullPath = GetFullPath();

        lock (fileLock)
        {
            return File.Exists(fullPath);
        }
    }

    public void Delete()
    {
        string fullPath = GetFullPath();
        string tempPath = $"{fullPath}.tmp";

        lock (fileLock)
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }

    private string GetFullPath()
    {
        return Path.Combine(dataDirPath, dataFileName);
    }

    private string EncryptDecrypt(string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return data;
        }

        char[] modifiedData = new char[data.Length];

        for (int i = 0; i < data.Length; i++)
        {
            modifiedData[i] = (char)(data[i] ^ codeWord[i % codeWord.Length]);
        }

        return new string(modifiedData);
    }
}