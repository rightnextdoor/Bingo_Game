using System.Collections.Generic;
using UnityEngine;

public interface ISceneReadyCheck
{
    string ReadyName { get; }
    bool IsReady { get; }
}

public class SceneReadyController : MonoBehaviour
{
    public static SceneReadyController instance;

    private readonly List<SceneReadyEntry> readyEntries = new List<SceneReadyEntry>();

    #region Unity Methods

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    #endregion

    #region Registration

    public void RegisterReadyCheck(ISceneReadyCheck readyCheck, bool persistentCheck = false)
    {
        if (readyCheck == null)
        {
            return;
        }

        RemoveMissingReadyChecks();

        for (int i = 0; i < readyEntries.Count; i++)
        {
            if (readyEntries[i].readyCheck == readyCheck)
            {
                readyEntries[i].persistentCheck = readyEntries[i].persistentCheck || persistentCheck;
                return;
            }
        }

        readyEntries.Add(new SceneReadyEntry(readyCheck, persistentCheck));
    }

    public void UnregisterReadyCheck(ISceneReadyCheck readyCheck)
    {
        if (readyCheck == null)
        {
            return;
        }

        for (int i = readyEntries.Count - 1; i >= 0; i--)
        {
            if (readyEntries[i].readyCheck == readyCheck)
            {
                readyEntries.RemoveAt(i);
            }
        }
    }

    public void ClearSceneReadyChecks()
    {
        for (int i = readyEntries.Count - 1; i >= 0; i--)
        {
            if (readyEntries[i].persistentCheck)
            {
                continue;
            }

            readyEntries.RemoveAt(i);
        }
    }

    #endregion

    #region Ready Checks

    public bool AreAllReady()
    {
        RemoveMissingReadyChecks();

        for (int i = 0; i < readyEntries.Count; i++)
        {
            if (!readyEntries[i].readyCheck.IsReady)
            {
                return false;
            }
        }

        return true;
    }

    public string GetWaitingReadyName()
    {
        RemoveMissingReadyChecks();

        for (int i = 0; i < readyEntries.Count; i++)
        {
            if (readyEntries[i].readyCheck.IsReady)
            {
                continue;
            }

            return readyEntries[i].readyCheck.ReadyName;
        }

        return string.Empty;
    }

    private void RemoveMissingReadyChecks()
    {
        for (int i = readyEntries.Count - 1; i >= 0; i--)
        {
            ISceneReadyCheck readyCheck = readyEntries[i].readyCheck;

            if (readyCheck == null)
            {
                readyEntries.RemoveAt(i);
                continue;
            }

            Object unityObject = readyCheck as Object;

            if (unityObject == null)
            {
                readyEntries.RemoveAt(i);
            }
        }
    }

    #endregion

    #region Internal Data

    private class SceneReadyEntry
    {
        public ISceneReadyCheck readyCheck;
        public bool persistentCheck;

        public SceneReadyEntry(ISceneReadyCheck readyCheck, bool persistentCheck)
        {
            this.readyCheck = readyCheck;
            this.persistentCheck = persistentCheck;
        }
    }

    #endregion
}