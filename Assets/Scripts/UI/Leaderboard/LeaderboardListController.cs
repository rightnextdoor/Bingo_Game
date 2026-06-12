using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class LeaderboardRowData
{
    public string userId;
    public List<LeaderboardCellData> cells = new();
}

public class LeaderboardListController : MonoBehaviour
{
    [Header("Rows")]
    [SerializeField] private Transform rowParent;
    [SerializeField] private LeaderboardRowUI rowPrefab;

    [Header("Layout")]
    [SerializeField] private VerticalLayoutGroup verticalLayoutGroup;
    [SerializeField] private ContentSizeFitter contentSizeFitter;

    public event Action<string> RowClicked;

    private readonly List<LeaderboardRowUI> spawnedRows = new();

    private void Awake()
    {
        if (rowParent == null)
        {
            rowParent = transform;
        }

        if (verticalLayoutGroup == null)
        {
            verticalLayoutGroup = GetComponent<VerticalLayoutGroup>();
        }

        if (contentSizeFitter == null)
        {
            contentSizeFitter = GetComponent<ContentSizeFitter>();
        }
    }

    public void SetRows(IReadOnlyList<LeaderboardRowData> rows, string selectedUserId)
    {
        ClearRows();

        if (rowParent == null || rowPrefab == null || rows == null)
        {
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            LeaderboardRowData rowData = rows[i];

            if (rowData == null)
            {
                continue;
            }

            LeaderboardRowUI row = Instantiate(rowPrefab, rowParent);
            row.gameObject.SetActive(true);

            bool isHighlighted = !string.IsNullOrWhiteSpace(selectedUserId) &&
                                 rowData.userId == selectedUserId;

            row.Setup(
                rowData.userId,
                rowData.cells,
                HandleRowClicked,
                isHighlighted
            );

            spawnedRows.Add(row);
        }
    }

    public void ClearRows()
    {
        for (int i = spawnedRows.Count - 1; i >= 0; i--)
        {
            if (spawnedRows[i] != null)
            {
                Destroy(spawnedRows[i].gameObject);
            }
        }

        spawnedRows.Clear();
    }

    public void SetSelectedUser(string selectedUserId)
    {
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            LeaderboardRowUI row = spawnedRows[i];

            if (row == null)
            {
                continue;
            }

            bool isHighlighted = !string.IsNullOrWhiteSpace(selectedUserId) &&
                                 row.UserId == selectedUserId;

            row.SetHighlighted(isHighlighted);
        }
    }

    public void SetListSpacing(float spacing)
    {
        if (verticalLayoutGroup == null)
        {
            return;
        }

        verticalLayoutGroup.spacing = spacing;
    }

    public void SetListPadding(int left, int right, int top, int bottom)
    {
        if (verticalLayoutGroup == null)
        {
            return;
        }

        verticalLayoutGroup.padding.left = left;
        verticalLayoutGroup.padding.right = right;
        verticalLayoutGroup.padding.top = top;
        verticalLayoutGroup.padding.bottom = bottom;
    }

    public void SetContentVerticalFit(ContentSizeFitter.FitMode fitMode)
    {
        if (contentSizeFitter == null)
        {
            return;
        }

        contentSizeFitter.verticalFit = fitMode;
    }

    private void HandleRowClicked(string userId)
    {
        RowClicked?.Invoke(userId);
    }
}