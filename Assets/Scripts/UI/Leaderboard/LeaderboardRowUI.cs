using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[Serializable]
public class LeaderboardCellData
{
    public LeaderboardCellDisplayType displayType;
    public string textValue;
    public Sprite imageSprite;
    public UIThemeTextType textType;
    public float preferredWidth;
    public float flexibleWidth;
    public float fontSize;
    public TextAlignmentOptions alignment;

    public static LeaderboardCellData Text(
        string textValue,
        UIThemeTextType textType,
        float preferredWidth,
        float flexibleWidth,
        float fontSize,
        TextAlignmentOptions alignment
    )
    {
        return new LeaderboardCellData
        {
            displayType = LeaderboardCellDisplayType.Text,
            textValue = textValue,
            textType = textType,
            preferredWidth = preferredWidth,
            flexibleWidth = flexibleWidth,
            fontSize = fontSize,
            alignment = alignment
        };
    }

    public static LeaderboardCellData Image(
        Sprite imageSprite,
        float preferredWidth,
        float flexibleWidth
    )
    {
        return new LeaderboardCellData
        {
            displayType = LeaderboardCellDisplayType.Image,
            imageSprite = imageSprite,
            preferredWidth = preferredWidth,
            flexibleWidth = flexibleWidth
        };
    }

    public static LeaderboardCellData TextAndImage(
        string textValue,
        Sprite imageSprite,
        UIThemeTextType textType,
        float preferredWidth,
        float flexibleWidth,
        float fontSize,
        TextAlignmentOptions alignment
    )
    {
        return new LeaderboardCellData
        {
            displayType = LeaderboardCellDisplayType.TextAndImage,
            textValue = textValue,
            imageSprite = imageSprite,
            textType = textType,
            preferredWidth = preferredWidth,
            flexibleWidth = flexibleWidth,
            fontSize = fontSize,
            alignment = alignment
        };
    }
}

public class LeaderboardRowUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Row Objects")]
    [SerializeField] private GameObject highlightObject;
    [SerializeField] private Transform cellParent;

    [Header("Prefabs")]
    [SerializeField] private LeaderboardCellUI cellPrefab;

    private readonly List<LeaderboardCellUI> spawnedCells = new();

    private string userId;
    private Action<string> rowClicked;

    public string UserId => userId;

    public void Setup(
        string newUserId,
        IReadOnlyList<LeaderboardCellData> cells,
        Action<string> onRowClicked,
        bool isHighlighted
    )
    {
        userId = newUserId;
        rowClicked = onRowClicked;

        BuildCells(cells);
        SetHighlighted(isHighlighted);
    }

    public void SetHighlighted(bool isHighlighted)
    {
        if (highlightObject != null)
        {
            highlightObject.SetActive(isHighlighted);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        rowClicked?.Invoke(userId);
    }

    private void BuildCells(IReadOnlyList<LeaderboardCellData> cells)
    {
        ClearCells();

        if (cellParent == null || cellPrefab == null || cells == null)
        {
            return;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            LeaderboardCellData cellData = cells[i];

            if (cellData == null)
            {
                continue;
            }

            LeaderboardCellUI cell = Instantiate(cellPrefab, cellParent);
            cell.gameObject.SetActive(true);

            ApplyCellData(cell, cellData);

            spawnedCells.Add(cell);
        }
    }

    private void ApplyCellData(LeaderboardCellUI cell, LeaderboardCellData cellData)
    {
        if (cell == null || cellData == null)
        {
            return;
        }

        cell.ShowCell();

        switch (cellData.displayType)
        {
            case LeaderboardCellDisplayType.Text:
                cell.SetTextCell(
                    cellData.textValue,
                    cellData.textType,
                    cellData.preferredWidth,
                    cellData.flexibleWidth,
                    cellData.fontSize,
                    cellData.alignment
                );
                break;

            case LeaderboardCellDisplayType.Image:
                cell.SetImageCell(
                    cellData.imageSprite,
                    cellData.preferredWidth,
                    cellData.flexibleWidth
                );
                break;

            case LeaderboardCellDisplayType.TextAndImage:
                cell.SetTextAndImageCell(
                    cellData.textValue,
                    cellData.imageSprite,
                    cellData.textType,
                    cellData.preferredWidth,
                    cellData.flexibleWidth,
                    cellData.fontSize,
                    cellData.alignment
                );
                break;
        }
    }

    private void ClearCells()
    {
        for (int i = spawnedCells.Count - 1; i >= 0; i--)
        {
            if (spawnedCells[i] != null)
            {
                Destroy(spawnedCells[i].gameObject);
            }
        }

        spawnedCells.Clear();
    }
}