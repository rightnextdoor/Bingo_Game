using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BotUserListData))]
public class BotUserListDataEditor : Editor
{
    private SerializedProperty botUsersProperty;

    private void OnEnable()
    {
        botUsersProperty = serializedObject.FindProperty("botUsers");

        BotUserListData botUserListData = target as BotUserListData;

        if (botUserListData != null)
        {
            botUserListData.EnsureBotUsersAreValid();
            EditorUtility.SetDirty(botUserListData);
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();

        if (GUILayout.Button("Add Player"))
        {
            AddPlayer();
        }

        EditorGUILayout.Space();

        if (botUsersProperty == null)
        {
            EditorGUILayout.HelpBox("Bot users list was not found.", MessageType.Error);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        for (int i = 0; i < botUsersProperty.arraySize; i++)
        {
            SerializedProperty botUserProperty = botUsersProperty.GetArrayElementAtIndex(i);

            DrawBotUserEntry(botUserProperty, i);

            EditorGUILayout.Space(4);
        }

        bool changed = serializedObject.ApplyModifiedProperties();

        if (changed)
        {
            BotUserListData botUserListData = target as BotUserListData;

            if (botUserListData != null)
            {
                botUserListData.EnsureBotUsersAreValid();
                EditorUtility.SetDirty(botUserListData);
            }
        }
    }

    private void DrawBotUserEntry(SerializedProperty botUserProperty, int index)
    {
        if (botUserProperty == null)
        {
            return;
        }

        SerializedProperty playerNameProperty = botUserProperty.FindPropertyRelative("playerName");

        string playerLabel = $"Player {index + 1}";

        string foldoutKey = GetBotUserFoldoutKey(botUserProperty, index);
        bool expanded = SessionState.GetBool(foldoutKey, false);

        EditorGUILayout.BeginVertical("box");

        bool newExpanded = EditorGUILayout.Foldout(expanded, playerLabel, true);

        if (newExpanded != expanded)
        {
            SessionState.SetBool(foldoutKey, newExpanded);
        }

        if (newExpanded)
        {
            EditorGUI.indentLevel++;

            if (playerNameProperty != null)
            {
                EditorGUILayout.PropertyField(playerNameProperty, new GUIContent("Player Name"));
            }

            DrawStatsFoldout(botUserProperty, index);

            EditorGUILayout.Space();

            if (GUILayout.Button("Remove Player"))
            {
                RemovePlayer(index);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawStatsFoldout(SerializedProperty botUserProperty, int index)
    {
        SerializedProperty statsProperty = botUserProperty.FindPropertyRelative("defaultStats");

        if (statsProperty == null)
        {
            EditorGUILayout.HelpBox("Default stats were not found.", MessageType.Warning);
            return;
        }

        string statsFoldoutKey = GetBotUserFoldoutKey(botUserProperty, index) + "_Stats";
        bool expanded = SessionState.GetBool(statsFoldoutKey, false);

        bool newExpanded = EditorGUILayout.Foldout(expanded, "Stats", true);

        if (newExpanded != expanded)
        {
            SessionState.SetBool(statsFoldoutKey, newExpanded);
        }

        if (!newExpanded)
        {
            return;
        }

        EditorGUI.indentLevel++;

        EditorGUILayout.PropertyField(statsProperty.FindPropertyRelative("points"));
        EditorGUILayout.PropertyField(statsProperty.FindPropertyRelative("gamesPlayed"));
        EditorGUILayout.PropertyField(statsProperty.FindPropertyRelative("wins"));
        EditorGUILayout.PropertyField(statsProperty.FindPropertyRelative("losses"));
        EditorGUILayout.PropertyField(statsProperty.FindPropertyRelative("bingosCalled"));

        EditorGUI.indentLevel--;
    }

    private void AddPlayer()
    {
        serializedObject.Update();

        int newIndex = botUsersProperty.arraySize;

        botUsersProperty.InsertArrayElementAtIndex(newIndex);

        SerializedProperty newPlayerProperty = botUsersProperty.GetArrayElementAtIndex(newIndex);

        ResetNewPlayer(newPlayerProperty, newIndex + 1);

        serializedObject.ApplyModifiedProperties();

        BotUserListData botUserListData = target as BotUserListData;

        if (botUserListData != null)
        {
            botUserListData.EnsureBotUsersAreValid();
            EditorUtility.SetDirty(botUserListData);
        }

        SessionState.SetBool(GetBotUserFoldoutKey(newPlayerProperty, newIndex), true);
    }

    private void RemovePlayer(int index)
    {
        serializedObject.Update();

        if (index < 0 || index >= botUsersProperty.arraySize)
        {
            return;
        }

        botUsersProperty.DeleteArrayElementAtIndex(index);

        serializedObject.ApplyModifiedProperties();

        BotUserListData botUserListData = target as BotUserListData;

        if (botUserListData != null)
        {
            botUserListData.EnsureBotUsersAreValid();
            EditorUtility.SetDirty(botUserListData);
        }
    }

    private void ResetNewPlayer(SerializedProperty playerProperty, int playerNumber)
    {
        if (playerProperty == null)
        {
            return;
        }

        SerializedProperty userIdProperty = playerProperty.FindPropertyRelative("userId");
        SerializedProperty userTagProperty = playerProperty.FindPropertyRelative("userTag");
        SerializedProperty playerNameProperty = playerProperty.FindPropertyRelative("playerName");
        SerializedProperty defaultStatsProperty = playerProperty.FindPropertyRelative("defaultStats");

        if (userIdProperty != null)
        {
            userIdProperty.stringValue = Guid.NewGuid().ToString("N");
        }

        if (userTagProperty != null)
        {
            userTagProperty.enumValueIndex = (int)UserTag.Bot;
        }

        if (playerNameProperty != null)
        {
            playerNameProperty.stringValue = $"Player {playerNumber}";
        }

        ResetStats(defaultStatsProperty);
    }

    private void ResetStats(SerializedProperty statsProperty)
    {
        if (statsProperty == null)
        {
            return;
        }

        SetInt(statsProperty, "points", 0);
        SetInt(statsProperty, "gamesPlayed", 0);
        SetInt(statsProperty, "wins", 0);
        SetInt(statsProperty, "losses", 0);
        SetInt(statsProperty, "bingosCalled", 0);
    }

    private void SetInt(SerializedProperty parentProperty, string propertyName, int value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property == null)
        {
            return;
        }

        property.intValue = value;
    }

    private string GetBotUserFoldoutKey(SerializedProperty botUserProperty, int index)
    {
        SerializedProperty userIdProperty = botUserProperty.FindPropertyRelative("userId");

        string keyId = userIdProperty != null ? userIdProperty.stringValue : string.Empty;

        if (string.IsNullOrWhiteSpace(keyId))
        {
            keyId = $"Index_{index}";
        }

        return $"{target.GetInstanceID()}_BotUserListData_{keyId}";
    }
}