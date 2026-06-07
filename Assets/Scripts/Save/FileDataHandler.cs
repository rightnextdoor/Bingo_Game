using System;
using System.IO;
using UnityEngine;

public class FileDataHandler
{
    private readonly string dataDirPath;
    private readonly string dataFileName;

    private readonly bool encryptData;
    private readonly string codeWord = "bingo-save-key-2026";

    public FileDataHandler(string dataDirPath, string dataFileName, bool encryptData)
    {
        this.dataDirPath = dataDirPath;
        this.dataFileName = dataFileName;
        this.encryptData = encryptData;
    }

    public void Save(GameData data)
    {
        string fullPath = Path.Combine(dataDirPath, dataFileName);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            string dataToStore = JsonUtility.ToJson(data, true);

            if (encryptData)
            {
                dataToStore = EncryptDecrypt(dataToStore);
            }

            using FileStream stream = new FileStream(fullPath, FileMode.Create);
            using StreamWriter writer = new StreamWriter(stream);

            writer.Write(dataToStore);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Error trying to save data to file: {fullPath}\n{exception}");
        }
    }

    public GameData Load()
    {
        string fullPath = Path.Combine(dataDirPath, dataFileName);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            string dataToLoad;

            using FileStream stream = new FileStream(fullPath, FileMode.Open);
            using StreamReader reader = new StreamReader(stream);

            dataToLoad = reader.ReadToEnd();

            if (encryptData)
            {
                dataToLoad = EncryptDecrypt(dataToLoad);
            }

            return JsonUtility.FromJson<GameData>(dataToLoad);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Error trying to load data from file: {fullPath}\n{exception}");
            return null;
        }
    }

    public void Delete()
    {
        string fullPath = Path.Combine(dataDirPath, dataFileName);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            Debug.Log($"Deleted save file: {fullPath}");
        }
        else
        {
            Debug.Log($"No save file found to delete: {fullPath}");
        }
    }

    private string EncryptDecrypt(string data)
    {
        string modifiedData = string.Empty;

        for (int i = 0; i < data.Length; i++)
        {
            modifiedData += (char)(data[i] ^ codeWord[i % codeWord.Length]);
        }

        return modifiedData;
    }
}