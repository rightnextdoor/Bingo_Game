using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class SceneField
{
    [SerializeField] private Object sceneAsset;
    [SerializeField] private string sceneName = "";

    public string SceneName
    {
        get { return sceneName; }
    }

    public static implicit operator string(SceneField sceneField)
    {
        return sceneField.SceneName;
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(SceneField))]
public class SceneFieldPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, GUIContent.none, property);

        SerializedProperty sceneAsst = property.FindPropertyRelative("sceneAsset");
        SerializedProperty sceneName = property.FindPropertyRelative("sceneName");

        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
        if (sceneAsst != null)
        {
            sceneAsst.objectReferenceValue = EditorGUI.ObjectField(position, sceneAsst.objectReferenceValue, typeof(SceneAsset), false);
            if (sceneAsst.objectReferenceValue != null)
            {
                sceneName.stringValue = (sceneAsst.objectReferenceValue as SceneAsset).name;
            }
        }

        EditorGUI.EndProperty();
    }
}
#endif