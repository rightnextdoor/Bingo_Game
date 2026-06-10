#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UIThemeData))]
public class UIThemeDataEditor : Editor
{
    private SerializedProperty themeTypeProperty;

    private bool backgroundSectionExpanded = true;
    private bool buttonSectionExpanded = true;
    private bool textSectionExpanded = true;
    private bool inputSectionExpanded = true;
    private bool dropdownSectionExpanded = true;
    private bool scrollSectionExpanded = true;

    private SerializedProperty mainBackgroundProperty;
    private SerializedProperty headerBackgroundProperty;
    private SerializedProperty secondaryBackgroundProperty;

    private SerializedProperty primaryButtonProperty;
    private SerializedProperty secondaryButtonProperty;
    private SerializedProperty closeButtonProperty;

    private SerializedProperty titleTextProperty;
    private SerializedProperty subtitleTextProperty;

    private SerializedProperty inputProperty;

    private SerializedProperty dropdownProperty;

    private SerializedProperty scrollProperty;

    private void OnEnable()
    {
        themeTypeProperty = serializedObject.FindProperty("themeType");

        mainBackgroundProperty = serializedObject.FindProperty("mainBackground");
        headerBackgroundProperty = serializedObject.FindProperty("headerBackground");
        secondaryBackgroundProperty = serializedObject.FindProperty("secondaryBackground");

        primaryButtonProperty = serializedObject.FindProperty("primaryButton");
        secondaryButtonProperty = serializedObject.FindProperty("secondaryButton");
        closeButtonProperty = serializedObject.FindProperty("closeButton");

        titleTextProperty = serializedObject.FindProperty("titleText");
        subtitleTextProperty = serializedObject.FindProperty("subtitleText");

        inputProperty = serializedObject.FindProperty("input");

        dropdownProperty = serializedObject.FindProperty("dropdown");
        scrollProperty = serializedObject.FindProperty("scroll");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawThemeInfo();

        EditorGUILayout.Space(8);

        DrawBackgroundSection();
        DrawButtonSection();
        DrawTextSection();
        DrawInputSection();
        DrawDropdownSection();
        DrawScrollSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawThemeInfo()
    {
        EditorGUILayout.LabelField("Theme Info", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(themeTypeProperty);
    }

    #region Sections

    private void DrawBackgroundSection()
    {
        EditorGUILayout.Space(6);

        backgroundSectionExpanded = EditorGUILayout.Foldout(
            backgroundSectionExpanded,
            "Background Section",
            true
        );

        if (!backgroundSectionExpanded)
        {
            return;
        }

        EditorGUI.indentLevel++;

        DrawFixedStyle("Main Background", mainBackgroundProperty, DrawImageComponentFields);
        DrawFixedStyle("Header Background", headerBackgroundProperty, DrawImageComponentFields);
        DrawFixedStyle("Secondary Background", secondaryBackgroundProperty, DrawImageComponentFields);

        EditorGUI.indentLevel--;
    }

    private void DrawButtonSection()
    {
        EditorGUILayout.Space(6);

        buttonSectionExpanded = EditorGUILayout.Foldout(
            buttonSectionExpanded,
            "Button Section",
            true
        );

        if (!buttonSectionExpanded)
        {
            return;
        }

        EditorGUI.indentLevel++;

        DrawFixedStyle("Primary Button", primaryButtonProperty, DrawButtonStyleFields);
        DrawFixedStyle("Secondary Button", secondaryButtonProperty, DrawButtonStyleFields);
        DrawFixedStyle("Close Button", closeButtonProperty, DrawButtonStyleFields);

        EditorGUI.indentLevel--;
    }

    private void DrawTextSection()
    {
        EditorGUILayout.Space(6);

        textSectionExpanded = EditorGUILayout.Foldout(
            textSectionExpanded,
            "Text Section",
            true
        );

        if (!textSectionExpanded)
        {
            return;
        }

        EditorGUI.indentLevel++;

        DrawFixedStyle("Title Text", titleTextProperty, DrawTextComponentFields);
        DrawFixedStyle("Subtitle Text", subtitleTextProperty, DrawTextComponentFields);

        EditorGUI.indentLevel--;
    }

    private void DrawInputSection()
    {
        EditorGUILayout.Space(6);

        inputSectionExpanded = EditorGUILayout.Foldout(
            inputSectionExpanded,
            "Input Section",
            true
        );

        if (!inputSectionExpanded)
        {
            return;
        }

        EditorGUI.indentLevel++;

        inputProperty.isExpanded = EditorGUILayout.Foldout(
            inputProperty.isExpanded,
            "Input",
            true
        );

        if (!inputProperty.isExpanded)
        {
            EditorGUI.indentLevel--;
            return;
        }

        EditorGUI.indentLevel++;

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

        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;
    }

    private void DrawDropdownSection()
    {
        EditorGUILayout.Space(6);

        dropdownSectionExpanded = EditorGUILayout.Foldout(
            dropdownSectionExpanded,
            "Dropdown Section",
            true
        );

        if (!dropdownSectionExpanded)
        {
            return;
        }

        EditorGUI.indentLevel++;

        dropdownProperty.isExpanded = EditorGUILayout.Foldout(
            dropdownProperty.isExpanded,
            "Dropdown",
            true
        );

        if (!dropdownProperty.isExpanded)
        {
            EditorGUI.indentLevel--;
            return;
        }

        EditorGUI.indentLevel++;

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
            "Item Visual",
            dropdownProperty.FindPropertyRelative("itemVisual"),
            DrawSelectableVisualFields
        );
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

        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;
    }

    private void DrawScrollSection()
    {
        EditorGUILayout.Space(6);

        scrollSectionExpanded = EditorGUILayout.Foldout(
            scrollSectionExpanded,
            "Scroll Section",
            true
        );

        if (!scrollSectionExpanded)
        {
            return;
        }

        EditorGUI.indentLevel++;

        scrollProperty.isExpanded = EditorGUILayout.Foldout(
            scrollProperty.isExpanded,
            "Scroll",
            true
        );

        if (!scrollProperty.isExpanded)
        {
            EditorGUI.indentLevel--;
            return;
        }

        EditorGUI.indentLevel++;

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

        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;
    }

    #endregion

    #region Draw Fields

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

    #endregion
}
#endif