using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "UserIconData", menuName = "Bingo Game/UI/User Icon Data")]
public class UserIconData : ScriptableObject
{
    [Header("Icon Data")]
    [SerializeField] private string iconId;
    [SerializeField] private UIIconType iconType = UIIconType.None;
    [SerializeField] private Sprite iconSprite;

    public string IconId => iconId;
    public UIIconType IconType => iconType;
    public Sprite IconSprite => iconSprite;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(iconId) && iconSprite != null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        string path = AssetDatabase.GetAssetPath(this);

        if (!string.IsNullOrWhiteSpace(path))
            iconId = AssetDatabase.AssetPathToGUID(path);

        EditorUtility.SetDirty(this);
    }
#endif
}