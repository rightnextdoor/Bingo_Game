using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

// MPPM can leave an unparented "Failed to load" placeholder in the main Editor.
// Unity checks every EditorWindow when maximizing, including these invisible objects.
// Recover only persistent orphan placeholders; never rebuild or save a layout.
[InitializeOnLoad]
internal static class MultiplayerPlayModeWindowRecovery
{
    private const double ScanIntervalSeconds = 1d;

    private static readonly Type FallbackWindowType =
        typeof(EditorWindow).Assembly.GetType("UnityEditor.FallbackEditorWindow");

    private static readonly FieldInfo ParentField = typeof(EditorWindow).GetField(
        "m_Parent", BindingFlags.Instance | BindingFlags.NonPublic);

    private static HashSet<EditorWindow> previousOrphans = new();
    private static HashSet<EditorWindow> currentOrphans = new();
    private static double nextScanTime;

    static MultiplayerPlayModeWindowRecovery()
    {
        // These are internal Unity APIs. If they change, leave windows untouched.
        if (FallbackWindowType == null || ParentField == null ||
            !typeof(UnityEngine.Object).IsAssignableFrom(ParentField.FieldType))
        {
            return;
        }

        EditorApplication.update += Update;
        AssemblyReloadEvents.beforeAssemblyReload += Unregister;
        EditorApplication.quitting += Unregister;
    }

    private static void Update()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying)
        {
            previousOrphans.Clear();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now < nextScanTime)
        {
            return;
        }

        nextScanTime = now + ScanIntervalSeconds;

        try
        {
            if (!CurrentPlayer.IsMainEditor)
            {
                previousOrphans.Clear();
                return;
            }

            currentOrphans.Clear();
            int recoveredCount = 0;

            foreach (UnityEngine.Object candidate in Resources.FindObjectsOfTypeAll(FallbackWindowType))
            {
                if (!(candidate is EditorWindow window) || window == null ||
                    window.GetType() != FallbackWindowType ||
                    (UnityEngine.Object)ParentField.GetValue(window) != null)
                {
                    continue;
                }

                // Give window construction/reparenting another scan to finish.
                // Docked or floating failed tabs still have parents and are never removed.
                if (!previousOrphans.Contains(window))
                {
                    currentOrphans.Add(window);
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(window);
                recoveredCount++;
            }

            (previousOrphans, currentOrphans) = (currentOrphans, previousOrphans);
            currentOrphans.Clear();

            if (recoveredCount > 0)
            {
                Debug.Log($"[MultiplayerPlayModeWindowRecovery] Removed {recoveredCount} detached failed Editor window(s). Existing window layouts were preserved.");
            }
        }
        catch (Exception exception)
        {
            // A recovery helper must not introduce its own repeating Editor errors.
            Unregister();
            Debug.LogWarning($"[MultiplayerPlayModeWindowRecovery] Recovery disabled: {exception.Message}");
        }
    }

    private static void Unregister()
    {
        EditorApplication.update -= Update;
        AssemblyReloadEvents.beforeAssemblyReload -= Unregister;
        EditorApplication.quitting -= Unregister;
        previousOrphans.Clear();
        currentOrphans.Clear();
    }
}
