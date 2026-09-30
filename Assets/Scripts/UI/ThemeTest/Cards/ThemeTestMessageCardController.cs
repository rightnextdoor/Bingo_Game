using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ThemeTestMessageCardController : MonoBehaviour
{
    [SerializeField] private TMP_Text cardTitle;
    [SerializeField] private Image messageBackground;
    [SerializeField] private TMP_Text messageText;
    [SerializeField, Min(1f)] private float minimumFontSize = 12f;

    public TMP_Text CardTitle => cardTitle;
    public Image MessageBackground => messageBackground;
    public TMP_Text MessageText => messageText;
    public float MinimumFontSize => minimumFontSize;
    public bool HasRequiredUI => cardTitle != null && messageBackground != null && messageText != null;
}
