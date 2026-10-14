using UnityEngine;

[CreateAssetMenu(fileName = "UIMessageDefaultStyle", menuName = "Bingo Game/UI/UI Message Default Style")]
public class UIMessageDefaultStyle : ScriptableObject
{
    [SerializeField] private Sprite backgroundImage;

    public Sprite BackgroundImage => backgroundImage;
}
