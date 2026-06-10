using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class UIThemeStyle
{
    [Header("Image Component")]
    [SerializeField] private Sprite sourceImage;
    [SerializeField] private Color color = Color.white;
    [SerializeField] private Material material;
    [SerializeField] private bool raycastTarget = true;

    [Header("Selectable Visual Style")]
    [SerializeField] private Selectable.Transition transition = Selectable.Transition.ColorTint;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightedColor = Color.white;
    [SerializeField] private Color pressedColor = Color.gray;
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color disabledColor = Color.gray;
    [SerializeField] private float colorMultiplier = 1f;
    [SerializeField] private float fadeDuration = 0.1f;

    [Header("Text Component")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private Material textMaterial;
    [SerializeField] private Color vertexColor = Color.white;
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

    public TMP_FontAsset FontAsset => fontAsset;
    public Material TextMaterial => textMaterial;
    public Color VertexColor => vertexColor;
    public TMP_ColorGradient ColorGradient => colorGradient;
    public bool TextRaycastTarget => textRaycastTarget;
}