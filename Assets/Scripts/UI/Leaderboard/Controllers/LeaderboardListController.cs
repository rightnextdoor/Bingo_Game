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
    private readonly List<LeaderboardRowCellSetup> rowBlueprint = new();

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

        ClearRows();
    }

    public void SetRowBlueprint(IReadOnlyList<LeaderboardRowCellSetup> blueprint)
    {
        rowBlueprint.Clear();

        if (blueprint != null)
        {
            for (int i = 0; i < blueprint.Count; i++)
            {
                if (blueprint[i] == null)
                {
                    continue;
                }

                rowBlueprint.Add(blueprint[i]);
            }
        }

        ClearRows();
    }

    public IReadOnlyList<LeaderboardRowCellSetup> GetRowBlueprint()
    {
        return rowBlueprint;
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
                OnRowClicked,
                isHighlighted
            );

            spawnedRows.Add(row);
        }
    }

    public void SetRankedUserRows(IReadOnlyList<LeaderboardUserRankData> rankedUserRows, string selectedUserId)
    {
        List<LeaderboardRowData> rows = new();

        if (rankedUserRows != null)
        {
            for (int i = 0; i < rankedUserRows.Count; i++)
            {
                if (rankedUserRows[i] == null || rankedUserRows[i].userData == null)
                {
                    continue;
                }

                rows.Add(BuildRowDataFromBlueprint(rankedUserRows[i]));
            }
        }

        SetRows(rows, selectedUserId);
    }

    public void ClearRows()
    {
        ClearRowParentChildren();
        spawnedRows.Clear();
    }

    private void ClearRowParentChildren()
    {
        if (rowParent == null)
        {
            return;
        }

        for (int i = rowParent.childCount - 1; i >= 0; i--)
        {
            Transform child = rowParent.GetChild(i);

            if (rowPrefab != null && child == rowPrefab.transform)
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

    private LeaderboardRowData BuildRowDataFromBlueprint(LeaderboardUserRankData rankedUserData)
    {
        LeaderboardRowData rowData = new()
        {
            userId = rankedUserData.userData.userId
        };

        for (int i = 0; i < rowBlueprint.Count; i++)
        {
            LeaderboardRowCellSetup cellSetup = rowBlueprint[i];

            if (cellSetup == null)
            {
                continue;
            }

            rowData.cells.Add(BuildCellDataFromSetup(cellSetup, rankedUserData));
        }

        return rowData;
    }

    private LeaderboardCellData BuildCellDataFromSetup(LeaderboardRowCellSetup cellSetup, LeaderboardUserRankData rankedUserData)
    {
        switch (cellSetup.displayType)
        {
            case LeaderboardCellDisplayType.Text:
                return LeaderboardCellData.Text(
                    GetTextValue(cellSetup, rankedUserData),
                    cellSetup.textType,
                    cellSetup.preferredWidth,
                    cellSetup.flexibleWidth,
                    cellSetup.fontSize,
                    cellSetup.alignment
                );

            case LeaderboardCellDisplayType.Image:
                return LeaderboardCellData.Image(
                    GetImageValue(cellSetup.valueType, rankedUserData),
                    cellSetup.preferredWidth,
                    cellSetup.flexibleWidth
                );

            case LeaderboardCellDisplayType.TextAndImage:
                return LeaderboardCellData.TextAndImage(
                    GetTextValue(cellSetup, rankedUserData),
                    GetImageValue(cellSetup.valueType, rankedUserData),
                    cellSetup.textType,
                    cellSetup.preferredWidth,
                    cellSetup.flexibleWidth,
                    cellSetup.fontSize,
                    cellSetup.alignment
                );

            default:
                return LeaderboardCellData.Text(
                    string.Empty,
                    cellSetup.textType,
                    cellSetup.preferredWidth,
                    cellSetup.flexibleWidth,
                    cellSetup.fontSize,
                    cellSetup.alignment
                );
        }
    }

    private string GetTextValue(LeaderboardRowCellSetup cellSetup, LeaderboardUserRankData rankedUserData)
    {
        switch (cellSetup.valueType)
        {
            case LeaderboardRowCellValueType.Rank:
                return $"#{rankedUserData.rank}";

            case LeaderboardRowCellValueType.PlayerNameWithShortId:
                return GetPlayerNameWithShortId(rankedUserData.userData, cellSetup.maxTextCharacters);

            case LeaderboardRowCellValueType.Score:
                return GetUserScore(rankedUserData.userData, cellSetup.maxNumberDigits).ToString("N0");

            default:
                return string.Empty;
        }
    }

    private Sprite GetImageValue(LeaderboardRowCellValueType valueType, LeaderboardUserRankData rankedUserData)
    {
        switch (valueType)
        {
            case LeaderboardRowCellValueType.UserIcon:
                if (UIIconManager.instance == null || rankedUserData.userData == null)
                {
                    return null;
                }

                return UIIconManager.instance.GetPlayerIconSpriteById(rankedUserData.userData.iconId);

            default:
                return null;
        }
    }

    private string GetPlayerNameWithShortId(UserData userData, int maxPlayerNameCharacters)
    {
        if (userData == null)
        {
            return string.Empty;
        }

        string playerName = string.IsNullOrWhiteSpace(userData.playerName) ? "Player" : userData.playerName.Trim();
        playerName = TrimTextToMaxCharacters(playerName, maxPlayerNameCharacters);

        string shortId = GetShortUserId(userData.userId);

        if (string.IsNullOrWhiteSpace(shortId))
        {
            return playerName;
        }

        return $"{playerName} #{shortId}";
    }

    private string GetShortUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return string.Empty;
        }

        userId = userId.Trim();

        if (userId.Length <= 4)
        {
            return userId;
        }

        return userId.Substring(0, 4);
    }

    private int GetUserScore(UserData userData, int maxNumberDigits)
    {
        if (userData == null || userData.stats == null)
        {
            return 0;
        }

        if (maxNumberDigits <= 0)
        {
            return userData.stats.points;
        }

        return Mathf.Clamp(userData.stats.points, 0, GetMaxNumberFromDigits(maxNumberDigits));
    }

    private string TrimTextToMaxCharacters(string textValue, int maxCharacters)
    {
        if (string.IsNullOrEmpty(textValue) || maxCharacters <= 0)
        {
            return textValue;
        }

        if (textValue.Length <= maxCharacters)
        {
            return textValue;
        }

        return textValue.Substring(0, maxCharacters);
    }

    private int GetMaxNumberFromDigits(int digitCount)
    {
        digitCount = Mathf.Clamp(digitCount, 1, 9);

        int maxValue = 0;

        for (int i = 0; i < digitCount; i++)
        {
            maxValue = (maxValue * 10) + 9;
        }

        return maxValue;
    }

    private void OnRowClicked(string userId)
    {
        RowClicked?.Invoke(userId);
    }
}