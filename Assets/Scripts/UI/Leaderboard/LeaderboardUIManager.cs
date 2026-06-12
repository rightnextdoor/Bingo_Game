using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LeaderboardUIManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("Title")]
    [SerializeField] private TMP_Text leaderboardTitleText;

    [Header("Controllers")]
    [SerializeField] private LeaderboardSearchController searchController;
    [SerializeField] private LeaderboardFilterController filterController;
    [SerializeField] private LeaderboardHeaderController headerController;
    [SerializeField] private LeaderboardListController listController;
    [SerializeField] private LeaderboardPageController pageController;

    #endregion

    #region Shared Cell Setup Values

    [SerializeField] private float rankCellWidth = 65f;
    [SerializeField] private float userIconCellWidth = 40f;
    [SerializeField] private float playerNameCellFlexibleWidth = 1f;
    [SerializeField] private float scoreCellWidth = 165f;

    [SerializeField] private int maxScoreDigits = 9;
    [SerializeField] private int maxPlayerNameCharacters = 40;

    #endregion

    #region User Setup Fields

    private readonly List<UserData> allUsers = new();
    private UserData currentUser;

    #endregion

    #region Mode Setup Fields

    private LeaderboardGameModeType currentMode;
    private string currentModeTitleText;
    private LeaderboardSortType currentSortType;

    private readonly List<LeaderboardCellData> currentHeaderCells = new();
    private readonly List<LeaderboardRowCellSetup> currentRowBlueprint = new();

    #endregion

    #region Page Setup Fields

    private readonly List<LeaderboardUserRankData> rankedUsers = new();
    private readonly List<List<LeaderboardUserRankData>> pageLists = new();

    private int currentPage = 1;
    private int currentPageSize = 10;
    private int totalPages = 1;

    #endregion

    #region Unity Methods

    private void OnEnable()
    {
        RegisterControllerEvents();

        UserManager.UserChanged += RefreshUserDataAndRebuildPages;
    }

    private void OnDisable()
    {
        UnregisterControllerEvents();

        UserManager.UserChanged -= RefreshUserDataAndRebuildPages;
    }

    private void Start()
    {
        RefreshUserData();
        ApplySelectedFilterMode();
    }

    #endregion

    #region User Setup

    private void RefreshUserData()
    {
        allUsers.Clear();
        currentUser = null;

        if (UserManager.instance == null)
        {
            return;
        }

        currentUser = UserManager.instance.CurrentUser;

        List<UserData> users = UserManager.instance.GetAllUsers();

        if (users == null)
        {
            return;
        }

        for (int i = 0; i < users.Count; i++)
        {
            if (users[i] == null)
            {
                continue;
            }

            allUsers.Add(users[i]);
        }
    }

    #endregion

    private void RefreshUserDataAndRebuildPages()
    {
        RefreshUserData();
        RebuildPagesAndDisplayPage(currentPage);
    }

    #region Controller Setup

    private void RegisterControllerEvents()
    {
        if (filterController != null)
        {
            filterController.GameModeChanged += SetLeaderboardMode;
            filterController.PageSizeChanged += SetPageSizeFromDropdown;
        }

        if (pageController != null)
        {
            pageController.PreviousPageRequested += DisplayPreviousPage;
            pageController.NextPageRequested += DisplayNextPage;
            pageController.PageRequested += DisplayRequestedPage;
        }
    }

    private void UnregisterControllerEvents()
    {
        if (filterController != null)
        {
            filterController.GameModeChanged -= SetLeaderboardMode;
            filterController.PageSizeChanged -= SetPageSizeFromDropdown;
        }

        if (pageController != null)
        {
            pageController.PreviousPageRequested -= DisplayPreviousPage;
            pageController.NextPageRequested -= DisplayNextPage;
            pageController.PageRequested -= DisplayRequestedPage;
        }
    }

    #endregion

    #region Mode Setup

    #region Mode Setup Entry

    private void ApplySelectedFilterMode()
    {
        currentPageSize = GetSelectedPageSizeFromFilter();

        LeaderboardGameModeType selectedMode = LeaderboardGameModeType.Overall;

        if (filterController != null)
        {
            selectedMode = filterController.GetSelectedGameMode();
        }

        SetLeaderboardMode(selectedMode);
    }

    public void SetLeaderboardMode(LeaderboardGameModeType gameMode)
    {
        currentMode = gameMode;

        switch (currentMode)
        {
            case LeaderboardGameModeType.Overall:
                SetupOverallMode();
                break;

            default:
                currentMode = LeaderboardGameModeType.Overall;
                SetupOverallMode();
                break;
        }
    }

    #endregion

    #region Mode Setup Methods

    private void SetupOverallMode()
    {
        currentModeTitleText = "Overall Leaderboard";
        currentSortType = LeaderboardSortType.ScoreHighest;

        currentHeaderCells.Clear();
        currentRowBlueprint.Clear();

        currentHeaderCells.Add(CreateHeaderTextCell("Rank", rankCellWidth, 0f, 18f, TextAlignmentOptions.Center));
        currentHeaderCells.Add(CreateHeaderTextCell("Player", 0f, playerNameCellFlexibleWidth, 18f, TextAlignmentOptions.Left));
        currentHeaderCells.Add(CreateHeaderTextCell("Score", scoreCellWidth, 0f, 18f, TextAlignmentOptions.Right));

        currentRowBlueprint.Add(CreateRowTextCellSetup(LeaderboardRowCellValueType.Rank, rankCellWidth, 0f, 16f, TextAlignmentOptions.Center));
        currentRowBlueprint.Add(CreateRowImageCellSetup(LeaderboardRowCellValueType.UserIcon, userIconCellWidth, 0f));
        currentRowBlueprint.Add(CreateRowTextCellSetup(LeaderboardRowCellValueType.PlayerNameWithShortId, 0f, playerNameCellFlexibleWidth, 16f, TextAlignmentOptions.Left, maxPlayerNameCharacters, 0));
        currentRowBlueprint.Add(CreateRowTextCellSetup(LeaderboardRowCellValueType.Score, scoreCellWidth, 0f, 16f, TextAlignmentOptions.Right, 0, maxScoreDigits));

        ApplyModeSetup();
    }

    #endregion

    #region Mode Build Methods

    private void ApplyModeSetup()
    {
        BuildHeaderFromModeSetup();
        BuildRowsFromModeSetup();

        RebuildPagesAndDisplayPage(1);
    }

    private void BuildHeaderFromModeSetup()
    {
        SetLeaderboardTitle(currentModeTitleText);

        if (headerController == null)
        {
            return;
        }

        headerController.SetHeaderCells(currentHeaderCells);
    }

    private void BuildRowsFromModeSetup()
    {
        if (listController == null)
        {
            return;
        }

        listController.SetRowBlueprint(currentRowBlueprint);
    }

    #endregion

    #region Mode Setup Helpers

    private LeaderboardCellData CreateHeaderTextCell(string textValue, float preferredWidth, float flexibleWidth, float fontSize, TextAlignmentOptions alignment)
    {
        return LeaderboardCellData.Text(
            textValue,
            UIThemeTextType.LeaderboardHeader,
            preferredWidth,
            flexibleWidth,
            fontSize,
            alignment
        );
    }

    private LeaderboardRowCellSetup CreateRowTextCellSetup(
    LeaderboardRowCellValueType valueType,
    float preferredWidth,
    float flexibleWidth,
    float fontSize,
    TextAlignmentOptions alignment,
    int maxTextCharacters = 0,
    int maxNumberDigits = 0)
    {
        return new LeaderboardRowCellSetup
        {
            displayType = LeaderboardCellDisplayType.Text,
            valueType = valueType,
            textType = UIThemeTextType.LeaderboardCell,
            preferredWidth = preferredWidth,
            flexibleWidth = flexibleWidth,
            fontSize = fontSize,
            alignment = alignment,
            maxTextCharacters = maxTextCharacters,
            maxNumberDigits = maxNumberDigits
        };
    }

    private LeaderboardRowCellSetup CreateRowImageCellSetup(LeaderboardRowCellValueType valueType, float preferredWidth, float flexibleWidth)
    {
        return new LeaderboardRowCellSetup
        {
            displayType = LeaderboardCellDisplayType.Image,
            valueType = valueType,
            textType = UIThemeTextType.LeaderboardCell,
            preferredWidth = preferredWidth,
            flexibleWidth = flexibleWidth,
            fontSize = 0f,
            alignment = TextAlignmentOptions.Center
        };
    }

    private void SetLeaderboardTitle(string titleText)
    {
        if (leaderboardTitleText == null)
        {
            return;
        }

        leaderboardTitleText.text = titleText;
    }

    #endregion

    #endregion

    #region Page Setup

    #region Page Setup Entry

    private void SetPageSizeFromDropdown(int pageSize)
    {
        switch (pageSize)
        {
            case 10:
                currentPageSize = 10;
                break;

            case 25:
                currentPageSize = 25;
                break;

            case 50:
                currentPageSize = 50;
                break;

            case 100:
                currentPageSize = 100;
                break;

            default:
                currentPageSize = Mathf.Max(1, pageSize);
                break;
        }

        RebuildPagesAndDisplayPage(1);
    }

    private void DisplayPreviousPage()
    {
        DisplayPage(currentPage - 1);
    }

    private void DisplayNextPage()
    {
        DisplayPage(currentPage + 1);
    }

    private void DisplayRequestedPage(int requestedPage)
    {
        DisplayPage(requestedPage);
    }

    private void RebuildPagesAndDisplayPage(int pageNumber)
    {
        BuildRankedUsersFromCurrentMode();
        BuildPageListsFromRankedUsers();
        DisplayPage(pageNumber);
    }

    #endregion

    #region Page Creation

    private void BuildRankedUsersFromCurrentMode()
    {
        rankedUsers.Clear();

        List<UserData> sortedModeUsers = new();

        for (int i = 0; i < allUsers.Count; i++)
        {
            if (allUsers[i] == null)
            {
                continue;
            }

            sortedModeUsers.Add(allUsers[i]);
        }

        switch (currentSortType)
        {
            case LeaderboardSortType.ScoreHighest:
                sortedModeUsers.Sort(SortUsersByScoreHighest);
                break;
        }

        for (int i = 0; i < sortedModeUsers.Count; i++)
        {
            rankedUsers.Add(new LeaderboardUserRankData
            {
                rank = i + 1,
                userData = sortedModeUsers[i]
            });
        }
    }

    private void BuildPageListsFromRankedUsers()
    {
        pageLists.Clear();

        currentPageSize = Mathf.Max(1, currentPageSize);
        totalPages = Mathf.Max(1, Mathf.CeilToInt(rankedUsers.Count / (float)currentPageSize));

        for (int i = 0; i < totalPages; i++)
        {
            pageLists.Add(new List<LeaderboardUserRankData>());
        }

        for (int i = 0; i < rankedUsers.Count; i++)
        {
            int pageIndex = i / currentPageSize;

            if (pageIndex < 0 || pageIndex >= pageLists.Count)
            {
                continue;
            }

            pageLists[pageIndex].Add(rankedUsers[i]);
        }
    }

    #endregion

    #region Page Display

    private void DisplayPage(int pageNumber)
    {
        if (pageLists.Count == 0)
        {
            BuildPageListsFromRankedUsers();
        }

        currentPage = Mathf.Clamp(pageNumber, 1, totalPages);

        SendCurrentPageDataToList();
        UpdatePageControllerDisplay();
    }

    private void SendCurrentPageDataToList()
    {
        if (listController == null)
        {
            return;
        }

        IReadOnlyList<LeaderboardUserRankData> pageData = GetPageData(currentPage);

        listController.SetRankedUserRows(pageData, string.Empty);
    }

    private void UpdatePageControllerDisplay()
    {
        if (pageController == null)
        {
            return;
        }

        pageController.SetPageDisplay(currentPage, totalPages);
    }

    #endregion

    #region Page Setup Helpers

    private IReadOnlyList<LeaderboardUserRankData> GetPageData(int pageNumber)
    {
        if (pageLists.Count == 0)
        {
            return new List<LeaderboardUserRankData>();
        }

        int pageIndex = Mathf.Clamp(pageNumber - 1, 0, pageLists.Count - 1);

        return pageLists[pageIndex];
    }

    private int GetSelectedPageSizeFromFilter()
    {
        if (filterController == null)
        {
            return 10;
        }

        return filterController.GetSelectedPageSize();
    }

    private int SortUsersByScoreHighest(UserData firstUser, UserData secondUser)
    {
        int scoreCompare = GetUserScore(secondUser).CompareTo(GetUserScore(firstUser));

        if (scoreCompare != 0)
        {
            return scoreCompare;
        }

        return string.Compare(
            GetUserName(firstUser),
            GetUserName(secondUser),
            StringComparison.OrdinalIgnoreCase
        );
    }

    private int GetUserScore(UserData userData)
    {
        if (userData == null || userData.stats == null)
        {
            return 0;
        }

        return userData.stats.points;
    }

    private string GetUserName(UserData userData)
    {
        if (userData == null || string.IsNullOrWhiteSpace(userData.playerName))
        {
            return string.Empty;
        }

        return userData.playerName.Trim();
    }

    #endregion

    #endregion
}