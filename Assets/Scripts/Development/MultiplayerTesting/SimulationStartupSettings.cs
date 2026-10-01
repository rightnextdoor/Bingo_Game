#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

namespace BingoGame.Development.MultiplayerTesting
{
    internal static class SimulationStartupSettings
    {
        [Serializable]
        private sealed class Snapshot
        {
            public bool useTestPlayers;
            public List<Entry> entries = new List<Entry>();
        }

        [Serializable]
        private sealed class Entry
        {
            public string scenePath;
            public string objectPath;
            public string componentType;
            public bool enabled;
            public string settings;
        }

        private static Snapshot snapshot;
        private static bool hasReadSnapshot;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            snapshot = null;
            hasReadSnapshot = false;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange _state)
        {
            if (_state != PlayModeStateChange.ExitingEditMode || !CurrentPlayer.IsMainEditor)
            {
                return;
            }

            Snapshot nextSnapshot = new Snapshot();
            foreach (string tag in CurrentPlayer.Tags)
            {
                if (tag == "BingoTestPlayer1")
                {
                    nextSnapshot.useTestPlayers = true;
                    break;
                }
            }

            if (nextSnapshot.useTestPlayers)
            {
                Capture<LobbySimulationController>(nextSnapshot);
                Capture<GameSimulationController>(nextSnapshot);
            }

            try
            {
                string path = GetSnapshotPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(nextSnapshot));
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[SimulationStartupSettings] Could not share the simulation settings: {exception.Message}");
            }
        }

        private static void Capture<T>(Snapshot _snapshot) where T : MonoBehaviour
        {
            T[] controllers = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (T controller in controllers)
            {
                if (!controller.gameObject.scene.IsValid() || !controller.gameObject.scene.isLoaded ||
                    EditorUtility.IsPersistent(controller))
                {
                    continue;
                }

                _snapshot.entries.Add(new Entry
                {
                    scenePath = controller.gameObject.scene.path,
                    objectPath = GetObjectPath(controller.transform),
                    componentType = typeof(T).FullName,
                    enabled = controller.enabled,
                    settings = JsonUtility.ToJson(controller)
                });
            }
        }

        internal static void Apply(MonoBehaviour _controller)
        {
            if (CurrentPlayer.IsMainEditor || !MultiplayerPlayModeTestContext.IsActive)
            {
                return;
            }

            if (!hasReadSnapshot)
            {
                hasReadSnapshot = true;
                try
                {
                    string path = GetSnapshotPath();
                    if (File.Exists(path))
                    {
                        snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(path));
                    }
                }
                catch (Exception exception) when (exception is IOException ||
                                                  exception is UnauthorizedAccessException ||
                                                  exception is ArgumentException)
                {
                    Debug.LogWarning($"[SimulationStartupSettings] Could not read the simulation settings: {exception.Message}");
                }
            }

            if (snapshot == null || !snapshot.useTestPlayers)
            {
                return;
            }

            string objectPath = GetObjectPath(_controller.transform);
            string componentType = _controller.GetType().FullName;
            foreach (Entry entry in snapshot.entries)
            {
                if (entry.scenePath != _controller.gameObject.scene.path ||
                    entry.objectPath != objectPath || entry.componentType != componentType)
                {
                    continue;
                }

                JsonUtility.FromJsonOverwrite(entry.settings, _controller);
                _controller.enabled = entry.enabled;
                return;
            }
        }

        private static string GetObjectPath(Transform _transform)
        {
            string path = _transform.name;
            while (_transform.parent != null)
            {
                _transform = _transform.parent;
                path = _transform.name + "/" + path;
            }

            return path;
        }

        private static string GetSnapshotPath()
        {
            string projectPath = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
            int virtualProjectIndex = projectPath.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase);
            if (virtualProjectIndex >= 0)
            {
                projectPath = projectPath.Substring(0, virtualProjectIndex);
            }

            return Path.Combine(projectPath, "Temp", "BingoGame.SimulationStartupSettings.json");
        }
    }
}
#endif
