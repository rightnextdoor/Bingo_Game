using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardHeaderController : MonoBehaviour
{
    [Header("Cells")]
    [SerializeField] private Transform cellParent;
    [SerializeField] private LeaderboardCellUI cellPrefab;

    [Header("Layout")]
    [SerializeField] private HorizontalLayoutGroup horizontalLayoutGroup;
    [SerializeField] private LayoutElement layoutElement;

    private readonly List<LeaderboardCellUI> spawnedCells = new();

    private void Awake()
    {
        if (cellParent == null)
        {
            cellParent = transform;
        }

        if (horizontalLayoutGroup == null)
        {
            horizontalLayoutGroup = GetComponent<HorizontalLayoutGroup>();
        }

        if (layoutElement == null)
        {
            layoutElement = GetComponent<LayoutElement>();
        }

        ClearHeaderCells();
    }

    public void SetHeaderCells(IReadOnlyList<LeaderboardCellData> cells)
    {
        ClearHeaderCells();

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

    public void ClearHeaderCells()
    {
        ClearCellParentChildren();
        spawnedCells.Clear();
    }

    public void SetHeaderSpacing(float spacing)
    {
        if (horizontalLayoutGroup == null)
        {
            return;
        }

        horizontalLayoutGroup.spacing = spacing;
    }

    public void SetHeaderHeight(float height)
    {
        if (layoutElement == null)
        {
            return;
        }

        layoutElement.preferredHeight = height;
    }

    public void SetHeaderPadding(int left, int right, int top, int bottom)
    {
        if (horizontalLayoutGroup == null)
        {
            return;
        }

        horizontalLayoutGroup.padding.left = left;
        horizontalLayoutGroup.padding.right = right;
        horizontalLayoutGroup.padding.top = top;
        horizontalLayoutGroup.padding.bottom = bottom;
    }

    private void ClearCellParentChildren()
    {
        if (cellParent == null)
        {
            return;
        }

        for (int i = cellParent.childCount - 1; i >= 0; i--)
        {
            Transform child = cellParent.GetChild(i);

            if (cellPrefab != null && child == cellPrefab.transform)
            {
                child.gameObject.SetActive(false);
                continue;
            }

            DestroyChildObject(child.gameObject);
        }
    }

    private void DestroyChildObject(GameObject childObject)
    {
        if (childObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(childObject);
        }
        else
        {
            DestroyImmediate(childObject);
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
}