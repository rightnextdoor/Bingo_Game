using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public class UIThemeBoard : MonoBehaviour, IUIThemeTarget
{
    #region Fields

    [SerializeField] private UIThemeBoardType boardType = UIThemeBoardType.Default;

    [Header("Board Root")]
    [SerializeField] private Image rootImage;

    [Header("Letter Header")]
    [SerializeField] private Image letterHeaderImage;

    [Header("B Header")]
    [SerializeField] private Image bHeaderImage;
    [SerializeField] private TMP_Text bHeaderText;

    [Header("I Header")]
    [SerializeField] private Image iHeaderImage;
    [SerializeField] private TMP_Text iHeaderText;

    [Header("N Header")]
    [SerializeField] private Image nHeaderImage;
    [SerializeField] private TMP_Text nHeaderText;

    [Header("G Header")]
    [SerializeField] private Image gHeaderImage;
    [SerializeField] private TMP_Text gHeaderText;

    [Header("O Header")]
    [SerializeField] private Image oHeaderImage;
    [SerializeField] private TMP_Text oHeaderText;

    [Header("Cell Grid")]
    [SerializeField] private Image cellGridImage;

    [Header("Cells")]
    [SerializeField] private List<UIThemeBoardCellTarget> cells = new();

    private UIThemeManager themeManager;

    #endregion

    #region Unity Methods

    private void OnEnable()
    {
        RegisterWithManager();
    }

    private void Start()
    {
        RegisterWithManager();
    }

    private void OnDisable()
    {
        if (themeManager != null)
        {
            themeManager.Unregister(this);
            themeManager = null;
        }
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        RegisterWithManager();
    }

#endif

    #endregion

    #region Theme

    private void RegisterWithManager()
    {
        if (!CacheThemeManager())
        {
            return;
        }

        themeManager.Register(this);
    }

    private bool CacheThemeManager()
    {
        if (themeManager == null)
        {
            themeManager = UIThemeManager.instance;
        }

        return themeManager != null;
    }

    public UIThemeBoardType BoardType => boardType;

    public void SetBoardType(UIThemeBoardType newBoardType)
    {
        boardType = newBoardType;
        ReapplyTheme();
    }

    public void ReapplyTheme()
    {
        if (!CacheThemeManager())
        {
            return;
        }

        UIThemeBoardStyle style = themeManager.ApplyTheme(
            UIThemeSectionType.Board,
            boardType) as UIThemeBoardStyle;

        if (style == null)
        {
            return;
        }

        ApplyBoardStyle(style);
        ApplyCellStyles(style);
    }

    private void ApplyBoardStyle(UIThemeBoardStyle style)
    {
        UIThemeApplier.ApplyImageStyle(rootImage, style.RootImage);
        UIThemeApplier.ApplyImageStyle(letterHeaderImage, style.LetterHeaderImage);

        ApplyHeaderStyle(
            bHeaderImage,
            bHeaderText,
            style.BHeaderImage,
            style.BHeaderText);

        ApplyHeaderStyle(
            iHeaderImage,
            iHeaderText,
            style.IHeaderImage,
            style.IHeaderText);

        ApplyHeaderStyle(
            nHeaderImage,
            nHeaderText,
            style.NHeaderImage,
            style.NHeaderText);

        ApplyHeaderStyle(
            gHeaderImage,
            gHeaderText,
            style.GHeaderImage,
            style.GHeaderText);

        ApplyHeaderStyle(
            oHeaderImage,
            oHeaderText,
            style.OHeaderImage,
            style.OHeaderText);

        UIThemeApplier.ApplyImageStyle(
            cellGridImage,
            style.CellGridImage);
    }

    private void ApplyHeaderStyle(
        Image headerImage,
        TMP_Text headerText,
        UIThemeStyle imageStyle,
        UIThemeStyle textStyle)
    {
        UIThemeApplier.ApplyImageStyle(headerImage, imageStyle);
        UIThemeApplier.ApplyTextStyle(headerText, textStyle);
    }

    private void ApplyCellStyles(UIThemeBoardStyle style)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            UIThemeBoardCellTarget cell = cells[i];

            if (cell == null)
            {
                continue;
            }

            ApplyCellStyle(cell, style);
        }
    }

    private void ApplyCellStyle(
        UIThemeBoardCellTarget cell,
        UIThemeBoardStyle style)
    {
        UIThemeApplier.ApplyImageStyle(
            cell.CellImage,
            style.CellImage);

        UIThemeApplier.ApplySelectableVisualStyle(
            cell.CellButton,
            style.CellVisual);

        UIThemeApplier.ApplyImageStyle(
            cell.MarkedHighlightImage,
            style.MarkedHighlightImage);

        UIThemeApplier.ApplyImageStyle(
            cell.WinningHighlightImage,
            style.WinningHighlightImage);

        UIThemeApplier.ApplyTextStyle(
            cell.ValueText,
            style.ValueText);
    }

    #endregion
}