using System;
using UnityEngine;

[Serializable]
public class UIThemeBoardStyle
{
    [SerializeField] private UIThemeBoardType boardType = UIThemeBoardType.Default;

    [Header("Board Root")]
    [SerializeField] private UIThemeStyle rootImage = new();

    [Header("Letter Header")]
    [SerializeField] private UIThemeStyle letterHeaderImage = new();

    [Header("B Header")]
    [SerializeField] private UIThemeStyle bHeaderImage = new();
    [SerializeField] private UIThemeStyle bHeaderText = new();

    [Header("I Header")]
    [SerializeField] private UIThemeStyle iHeaderImage = new();
    [SerializeField] private UIThemeStyle iHeaderText = new();

    [Header("N Header")]
    [SerializeField] private UIThemeStyle nHeaderImage = new();
    [SerializeField] private UIThemeStyle nHeaderText = new();

    [Header("G Header")]
    [SerializeField] private UIThemeStyle gHeaderImage = new();
    [SerializeField] private UIThemeStyle gHeaderText = new();

    [Header("O Header")]
    [SerializeField] private UIThemeStyle oHeaderImage = new();
    [SerializeField] private UIThemeStyle oHeaderText = new();

    [Header("Cell Grid")]
    [SerializeField] private UIThemeStyle cellGridImage = new();

    [Header("Cell")]
    [SerializeField] private UIThemeStyle cellImage = new();
    [SerializeField] private UIThemeStyle cellVisual = new();
    [SerializeField] private UIThemeStyle calledHighlightImage = new();
    [SerializeField] private UIThemeStyle markedHighlightImage = new();
    [SerializeField] private UIThemeStyle winningHighlightImage = new();
    [SerializeField] private UIThemeStyle valueText = new();

    public UIThemeSectionType SectionType => UIThemeSectionType.Board;
    public UIThemeBoardType BoardType => boardType;

    public UIThemeStyle RootImage => rootImage;
    public UIThemeStyle LetterHeaderImage => letterHeaderImage;

    public UIThemeStyle BHeaderImage => bHeaderImage;
    public UIThemeStyle BHeaderText => bHeaderText;

    public UIThemeStyle IHeaderImage => iHeaderImage;
    public UIThemeStyle IHeaderText => iHeaderText;

    public UIThemeStyle NHeaderImage => nHeaderImage;
    public UIThemeStyle NHeaderText => nHeaderText;

    public UIThemeStyle GHeaderImage => gHeaderImage;
    public UIThemeStyle GHeaderText => gHeaderText;

    public UIThemeStyle OHeaderImage => oHeaderImage;
    public UIThemeStyle OHeaderText => oHeaderText;

    public UIThemeStyle CellGridImage => cellGridImage;

    public UIThemeStyle CellImage => cellImage;
    public UIThemeStyle CellVisual => cellVisual;
    public UIThemeStyle CalledHighlightImage => calledHighlightImage;
    public UIThemeStyle MarkedHighlightImage => markedHighlightImage;
    public UIThemeStyle WinningHighlightImage => winningHighlightImage;
    public UIThemeStyle ValueText => valueText;
}