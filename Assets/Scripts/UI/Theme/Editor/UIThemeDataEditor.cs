#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UIThemeData))]
public class UIThemeDataEditor : Editor
{
    private SerializedProperty themeTypeProperty;

    private bool backgroundSectionExpanded = false;
    private bool buttonSectionExpanded = false;
    private bool textSectionExpanded = false;
    private bool inputSectionExpanded = false;
    private bool dropdownSectionExpanded = false;
    private bool scrollSectionExpanded = false;

    private SerializedProperty backgroundStylesProperty;
    private SerializedProperty buttonStylesProperty;
    private SerializedProperty textStylesProperty;
    private SerializedProperty inputStylesProperty;
    private SerializedProperty dropdownStylesProperty;
    private SerializedProperty scrollStylesProperty;

    private void OnEnable()
    {
        themeTypeProperty = serializedObject.FindProperty("themeType");

        backgroundStylesProperty = serializedObject.FindProperty("backgroundStyles");
        buttonStylesProperty = serializedObject.FindProperty("buttonStyles");
        textStylesProperty = serializedObject.FindProperty("textStyles");
        inputStylesProperty = serializedObject.FindProperty("inputStyles");
        dropdownStylesProperty = serializedObject.FindProperty("dropdownStyles");
        scrollStylesProperty = serializedObject.FindProperty("scrollStyles");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();

        DrawThemeInfo();

        EditorGUILayout.Space(8);

        DrawBackgroundSection();
        DrawButtonSection();
        DrawTextSection();
        DrawInputSection();
        DrawDropdownSection();
        DrawScrollSection();

        bool changed = EditorGUI.EndChangeCheck();

        serializedObject.ApplyModifiedProperties();

        if (changed)
        {
            UIThemeData themeData = target as UIThemeData;

            if (themeData != null)
            {
                UIThemeManager.RefreshThemeData(themeData);
                EditorUtility.SetDirty(themeData);
            }
        }
    }

    private void DrawThemeInfo()
    {
        EditorGUILayout.LabelField("Theme Info", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(themeTypeProperty);
    }

    #region Sections

    private void DrawBackgroundSection()
    {
        DrawSimpleStyleListSection(
            ref backgroundSectionExpanded,
            "Background Section",
            backgroundStylesProperty,
            "Background",
            "backgroundType",
            DrawImageComponentFields
        );
    }

    private void DrawButtonSection()
    {
        DrawSimpleStyleListSection(
            ref buttonSectionExpanded,
            "Button Section",
            buttonStylesProperty,
            "Button",
            "buttonType",
            DrawButtonStyleFields
        );
    }

    private void DrawTextSection()
    {
        DrawSimpleStyleListSection(
            ref textSectionExpanded,
            "Text Section",
            textStylesProperty,
            "Text",
            "textType",
            DrawTextComponentFields
        );
    }

    private void DrawInputSection()
    {
        EditorGUILayout.Space(6);

        inputSectionExpanded = DrawRememberedSectionFoldout("Input Section", inputSectionExpanded);

        if (!inputSectionExpanded)
        {
            return;
        }

        DrawList(
            inputStylesProperty,
            "Input",
            DrawInputStyleEntry
        );
    }

    private void DrawDropdownSection()
    {
        EditorGUILayout.Space(6);

        dropdownSectionExpanded = DrawRememberedSectionFoldout("Dropdown Section", dropdownSectionExpanded);

        if (!dropdownSectionExpanded)
        {
            return;
        }

        DrawList(
            dropdownStylesProperty,
            "Dropdown",
            DrawDropdownStyleEntry
        );
    }

    private void DrawScrollSection()
    {
        EditorGUILayout.Space(6);

        scrollSectionExpanded = DrawRememberedSectionFoldout("Scroll Section", scrollSectionExpanded);

        if (!scrollSectionExpanded)
        {
            return;
        }

        DrawList(
            scrollStylesProperty,
            "Scroll",
            DrawScrollStyleEntry
        );
    }

    #endregion

    #region List Drawing

    private void DrawSimpleStyleListSection(
        ref bool sectionExpanded,
        string sectionTitle,
        SerializedProperty listProperty,
        string entryLabel,
        string typePropertyName,
        System.Action<SerializedProperty> drawStyleFields
    )
    {
        EditorGUILayout.Space(6);

        sectionExpanded = DrawRememberedSectionFoldout(sectionTitle, sectionExpanded);

        if (!sectionExpanded)
        {
            return;
        }

        DrawList(
            listProperty,
            entryLabel,
            entryProperty =>
            {
                SerializedProperty typeProperty = entryProperty.FindPropertyRelative(typePropertyName);
                SerializedProperty styleProperty = entryProperty.FindPropertyRelative("style");

                EditorGUILayout.PropertyField(typeProperty, new GUIContent("Type"));

                EditorGUILayout.Space(4);

                DrawFixedStyle("Style", styleProperty, drawStyleFields);
            }
        );
    }

    private void DrawList(
        SerializedProperty listProperty,
        string entryLabel,
        System.Action<SerializedProperty> drawEntry
    )
    {
        if (listProperty == null)
        {
            EditorGUILayout.HelpBox(
                $"Missing property for {entryLabel} list. Make sure UIThemeData has the new list field.",
                MessageType.Error
            );
            return;
        }

        EditorGUI.indentLevel++;

        for (int i = 0; i < listProperty.arraySize; i++)
        {
            SerializedProperty entryProperty = listProperty.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            entryProperty.isExpanded = EditorGUILayout.Foldout(
                entryProperty.isExpanded,
                $"{entryLabel} {i + 1}",
                true
            );

            if (entryProperty.isExpanded)
            {
                EditorGUI.indentLevel++;

                drawEntry?.Invoke(entryProperty);

                EditorGUILayout.Space(4);

                if (GUILayout.Button($"Remove {entryLabel} {i + 1}"))
                {
                    listProperty.DeleteArrayElementAtIndex(i);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(4);

        if (GUILayout.Button($"Add {entryLabel}"))
        {
            int newIndex = listProperty.arraySize;
            listProperty.InsertArrayElementAtIndex(newIndex);

            SerializedProperty newEntry = listProperty.GetArrayElementAtIndex(newIndex);
            newEntry.isExpanded = true;

            ResetNewEntryDefaults(newEntry);
        }

        EditorGUI.indentLevel--;
    }

    #endregion

    #region Compound Entries

    private void DrawInputStyleEntry(SerializedProperty inputProperty)
    {
        EditorGUILayout.PropertyField(
            inputProperty.FindPropertyRelative("inputType"),
            new GUIContent("Type")
        );

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Field", EditorStyles.boldLabel);

        DrawFixedStyle(
            "Field Image",
            inputProperty.FindPropertyRelative("fieldImage"),
            DrawImageComponentFields
        );

        DrawFixedStyle(
            "Field Visual",
            inputProperty.FindPropertyRelative("fieldVisual"),
            DrawSelectableVisualFields
        );

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Text", EditorStyles.boldLabel);

        DrawFixedStyle(
            "Input Text",
            inputProperty.FindPropertyRelative("inputText"),
            DrawTextComponentFields
        );

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Placeholder", EditorStyles.boldLabel);

        DrawFixedStyle(
            "Placeholder Text",
            inputProperty.FindPropertyRelative("placeholderText"),
            DrawTextComponentFields
        );
    }

    private void DrawDropdownStyleEntry(SerializedProperty dropdownProperty)
    {
        EditorGUILayout.PropertyField(
            dropdownProperty.FindPropertyRelative("dropdownType"),
            new GUIContent("Type")
        );

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Dropdown Root", EditorStyles.boldLabel);

        DrawFixedStyle(
            "Dropdown Image",
            dropdownProperty.FindPropertyRelative("dropdownImage"),
            DrawImageComponentFields
        );

        DrawFixedStyle(
            "Dropdown Visual",
            dropdownProperty.FindPropertyRelative("dropdownVisual"),
            DrawSelectableVisualFields
        );

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Label", EditorStyles.boldLabel);

        DrawFixedStyle(
            "Label Text",
            dropdownProperty.FindPropertyRelative("labelText"),
            DrawTextComponentFields
        );

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Arrow", EditorStyles.boldLabel);

        DrawFixedStyle(
            "Arrow Image",
            dropdownProperty.FindPropertyRelative("arrowImage"),
            DrawImageComponentFields
        );

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Template", EditorStyles.boldLabel);

        DrawFixedStyle(
            "Template Image",
            dropdownProperty.FindPropertyRelative("templateImage"),
            DrawImageComponentFields
        );

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Viewport", EditorStyles.boldLabel);

        DrawFixedStyle(
            "Viewport Image",
            dropdownProperty.FindPropertyRelative("viewportImage"),
            DrawImageComponentFields
        );

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Item", EditorStyles.boldLabel);

        DrawFixedStyle(
            "Item Background Image",
            dropdownProperty.FindPropertyRelative("itemBackgroundImage"),
            DrawImageComponentFields
        );

        DrawFixedStyle(
            "Item Checkmark Image",
            dropdownProperty.FindPropertyRelative("itemCheckmarkImage"),
            DrawImageComponentFields
        );

        DrawFixedStyle(
            "Item Visual",
            dropdownProperty.FindPropertyRelative("itemVisual"),
            DrawSelectableVisualFields
        );

        DrawFixedStyle(
            "Item Label Text",
            dropdownProperty.FindPropertyRelative("itemLabelText"),
            DrawTextComponentFields
        );

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Scrollbar", EditorStyles.boldLabel);

        DrawFixedStyle(
            "Scrollbar Image",
            dropdownProperty.FindPropertyRelative("scrollbarImage"),
            DrawImageComponentFields
        );

        DrawFixedStyle(
            "Scrollbar Visual",
            dropdownProperty.FindPropertyRelative("scrollbarVisual"),
            DrawSelectableVisualFields
        );

        DrawFixedStyle(
            "Scrollbar Handle Image",
            dropdownProperty.FindPropertyRelative("scrollbarHandleImage"),
            DrawImageComponentFields
        );
    }

    private void DrawScrollStyleEntry(SerializedProperty scrollProperty)
    {
        EditorGUILayout.PropertyField(
            scrollProperty.FindPropertyRelative("scrollType"),
            new GUIContent("Type")
        );

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Root", EditorStyles.boldLabel);

        DrawFixedStyle(
            "Root Image",
            scrollProperty.FindPropertyRelative("rootImage"),
            DrawImageComponentFields
        );

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Viewport", EditorStyles.boldLabel);

        DrawFixedStyle(
            "Viewport Image",
            scrollProperty.FindPropertyRelative("viewportImage"),
            DrawImageComponentFields
        );

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Horizontal Scrollbar", EditorStyles.boldLabel);

        DrawFixedStyle(
            "Horizontal Scrollbar Image",
            scrollProperty.FindPropertyRelative("horizontalScrollbarImage"),
            DrawImageComponentFields
        );

        DrawFixedStyle(
            "Horizontal Scrollbar Visual",
            scrollProperty.FindPropertyRelative("horizontalScrollbarVisual"),
            DrawSelectableVisualFields
        );

        DrawFixedStyle(
            "Horizontal Handle Image",
            scrollProperty.FindPropertyRelative("horizontalHandleImage"),
            DrawImageComponentFields
        );

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Vertical Scrollbar", EditorStyles.boldLabel);

        DrawFixedStyle(
            "Vertical Scrollbar Image",
            scrollProperty.FindPropertyRelative("verticalScrollbarImage"),
            DrawImageComponentFields
        );

        DrawFixedStyle(
            "Vertical Scrollbar Visual",
            scrollProperty.FindPropertyRelative("verticalScrollbarVisual"),
            DrawSelectableVisualFields
        );

        DrawFixedStyle(
            "Vertical Handle Image",
            scrollProperty.FindPropertyRelative("verticalHandleImage"),
            DrawImageComponentFields
        );
    }

    #endregion

    #region Style Field Drawers

    private void DrawImageComponentFields(SerializedProperty styleProperty)
    {
        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("sourceImage"),
            new GUIContent("Source Image")
        );

        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("color"),
            new GUIContent("Color")
        );

        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("material"),
            new GUIContent("Material")
        );

        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("raycastTarget"),
            new GUIContent("Raycast Target")
        );
    }

    private void DrawButtonStyleFields(SerializedProperty styleProperty)
    {
        EditorGUILayout.LabelField("Image", EditorStyles.boldLabel);
        DrawImageComponentFields(styleProperty);

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Visual State", EditorStyles.boldLabel);
        DrawSelectableVisualFields(styleProperty);

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Text", EditorStyles.boldLabel);
        DrawTextComponentFields(styleProperty);
    }

    private void DrawSelectableVisualFields(SerializedProperty styleProperty)
    {
        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("transition"),
            new GUIContent("Transition")
        );

        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("normalColor"),
            new GUIContent("Normal Color")
        );

        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("highlightedColor"),
            new GUIContent("Highlighted Color")
        );

        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("pressedColor"),
            new GUIContent("Pressed Color")
        );

        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("selectedColor"),
            new GUIContent("Selected Color")
        );

        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("disabledColor"),
            new GUIContent("Disabled Color")
        );

        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("colorMultiplier"),
            new GUIContent("Color Multiplier")
        );

        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("fadeDuration"),
            new GUIContent("Fade Duration")
        );
    }

    private void DrawTextComponentFields(SerializedProperty styleProperty)
    {
        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("fontAsset"),
            new GUIContent("Font Asset")
        );

        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("textMaterial"),
            new GUIContent("Material")
        );

        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("vertexColor"),
            new GUIContent("Vertex Color")
        );

        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("colorGradient"),
            new GUIContent("Color Gradient")
        );

        EditorGUILayout.PropertyField(
            styleProperty.FindPropertyRelative("textRaycastTarget"),
            new GUIContent("Raycast Target")
        );
    }

    #endregion

    #region Helpers

    private void DrawFixedStyle(
        string label,
        SerializedProperty styleProperty,
        System.Action<SerializedProperty> drawStyleFields
    )
    {
        if (styleProperty == null)
        {
            EditorGUILayout.HelpBox($"Missing style property: {label}", MessageType.Error);
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        styleProperty.isExpanded = EditorGUILayout.Foldout(
            styleProperty.isExpanded,
            label,
            true
        );

        if (styleProperty.isExpanded)
        {
            EditorGUI.indentLevel++;

            drawStyleFields?.Invoke(styleProperty);

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void ResetNewEntryDefaults(SerializedProperty entryProperty)
    {
        if (entryProperty == null)
        {
            return;
        }

        SerializedProperty iterator = entryProperty.Copy();
        SerializedProperty endProperty = entryProperty.GetEndProperty();

        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            if (SerializedProperty.EqualContents(iterator, endProperty))
            {
                break;
            }

            enterChildren = true;

            if (iterator.propertyType == SerializedPropertyType.ObjectReference)
            {
                switch (iterator.name)
                {
                    case "sourceImage":
                    case "material":
                    case "fontAsset":
                    case "textMaterial":
                    case "colorGradient":
                        iterator.objectReferenceValue = null;
                        break;
                }

                continue;
            }

            if (iterator.propertyType == SerializedPropertyType.Color)
            {
                switch (iterator.name)
                {
                    case "pressedColor":
                    case "disabledColor":
                        iterator.colorValue = new Color32(128, 128, 128, 255);
                        break;

                    default:
                        iterator.colorValue = new Color32(255, 255, 255, 255);
                        break;
                }

                continue;
            }

            if (iterator.propertyType == SerializedPropertyType.Boolean)
            {
                switch (iterator.name)
                {
                    case "raycastTarget":
                    case "textRaycastTarget":
                        iterator.boolValue = true;
                        break;
                }

                continue;
            }

            if (iterator.propertyType == SerializedPropertyType.Enum)
            {
                if (iterator.name == "transition")
                {
                    iterator.enumValueIndex = (int)UnityEngine.UI.Selectable.Transition.ColorTint;
                }

                continue;
            }

            if (iterator.propertyType == SerializedPropertyType.Float)
            {
                switch (iterator.name)
                {
                    case "colorMultiplier":
                        iterator.floatValue = 1f;
                        break;

                    case "fadeDuration":
                        iterator.floatValue = 0.1f;
                        break;
                }
            }
        }
    }

    private string GetSectionFoldoutKey(string sectionName)
    {
        return $"{target.GetInstanceID()}_UIThemeDataEditor_{sectionName}";
    }

    private bool DrawRememberedSectionFoldout(string sectionName, bool currentValue)
    {
        string key = GetSectionFoldoutKey(sectionName);

        bool savedValue = SessionState.GetBool(key, currentValue);

        bool newValue = EditorGUILayout.Foldout(
            savedValue,
            sectionName,
            true
        );

        if (newValue != savedValue)
        {
            SessionState.SetBool(key, newValue);
        }

        return newValue;
    }

    #endregion
}
#endif