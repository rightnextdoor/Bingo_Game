using UnityEngine;

[CreateAssetMenu(fileName = "UIMessageDefaultStyle", menuName = "Bingo Game/UI/UI Message Default Style")]
public class UIMessageDefaultStyle : ScriptableObject
{
    [SerializeField, Min(1)] private int fontSize = 24;
    [SerializeField] private Color textColor = new Color32(241, 243, 245, 255);
    [SerializeField] private Color backgroundColor = new Color32(58, 66, 80, 255);
    [SerializeField] private Sprite backgroundImage;

    public int FontSize => Mathf.Max(1, fontSize);
    public Color TextColor => textColor;
    public Color BackgroundColor => backgroundColor;
    public Sprite BackgroundImage => backgroundImage;
}
