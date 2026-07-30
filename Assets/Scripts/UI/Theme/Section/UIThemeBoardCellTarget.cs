using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIThemeBoardCellTarget : MonoBehaviour
{
    [Header("Cell")]
    [SerializeField] private Image cellImage;
    [SerializeField] private Button cellButton;

    [Header("Highlights")]
    [SerializeField] private Image markedHighlightImage;
    [SerializeField] private Image winningHighlightImage;

    [Header("Text")]
    [SerializeField] private TMP_Text valueText;

    public Image CellImage => cellImage;
    public Button CellButton => cellButton;

    public Image MarkedHighlightImage => markedHighlightImage;
    public Image WinningHighlightImage => winningHighlightImage;

    public TMP_Text ValueText => valueText;
}