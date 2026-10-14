using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class UIThemeStyle
{
    [Header("Image Component")]
    [SerializeField] private Sprite sourceImage;
    [SerializeField] private Color color = new Color32(255, 255, 255, 255);
    [SerializeField] private Material material;
    [SerializeField] private bool raycastTarget = true;

    [Header("Selectable Visual Style")]
    [SerializeField] private Selectable.Transition transition = Selectable.Transition.ColorTint;
    [SerializeField] private Color normalColor = new Color32(255, 255, 255, 255);
    [SerializeField] private Color highlightedColor = new Color(0.81671154f, 0.81671154f, 0.81671154f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.55795145f, 0.5436642f, 0.5436642f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.42318046f, 0.42318046f, 0.42318046f, 0.5019608f);
    [SerializeField] private float colorMultiplier = 1f;
    [SerializeField] private float fadeDuration = 0.1f;

    [Space]
    [Header("Navigation")]
    [Tooltip("Navigation mode applied to themed buttons. Explicit direction references remain configured on each button.")]
    [SerializeField] private Navigation.Mode navigationMode = Navigation.Mode.Automatic;

    [Space]
    [Header("Text Component")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private Material textMaterial;
    [SerializeField] private Color vertexColor = new Color32(255, 255, 255, 255);
    [SerializeField] private TMP_ColorGradient colorGradient;
    [SerializeField] private bool textRaycastTarget = true;

    public Sprite SourceImage => sourceImage;
    public Color Color => color;
    public Material Material => material;
    public bool RaycastTarget => raycastTarget;

    public Selectable.Transition Transition => transition;
    public Color NormalColor => normalColor;
    public Color HighlightedColor => highlightedColor;
    public Color PressedColor => pressedColor;
    public Color SelectedColor => selectedColor;
    public Color DisabledColor => disabledColor;
    public float ColorMultiplier => colorMultiplier;
    public float FadeDuration => fadeDuration;

    public Navigation.Mode NavigationMode => navigationMode;

    public TMP_FontAsset FontAsset => fontAsset;
    public Material TextMaterial => textMaterial;
    public Color VertexColor => vertexColor;
    public TMP_ColorGradient ColorGradient => colorGradient;
    public bool TextRaycastTarget => textRaycastTarget;
}
