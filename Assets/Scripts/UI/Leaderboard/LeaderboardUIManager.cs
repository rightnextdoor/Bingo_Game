using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LeaderboardUIManager : MonoBehaviour
{
    public static LeaderboardUIManager instance;

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

    private int maxScoreDigits = 9;
    private int maxPlayerNameCharacters = 20;

    #endregion

    #region User Setup Fields

    private UserManager userManager;
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

    #region Search Setup Fields

    private const int MinimumSearchCharacters = 3;

    private readonly List<LeaderboardUserRankData> displayRankedUsers = new();

    private bool searchActive;
    private string currentSearchText = string.Empty;
    private string selectedUserId = string.Empty;

    #endregion

    private Coroutine initialRefreshRoutine;

    #region Unity Methods

    private void OnEnable()
    {
        CacheManagers();

        RegisterControllerEvents();

        UserManager.UserChanged += RefreshUserDataAndRebuildPages;
        SaveManager.SaveDataChanged += RefreshUserDataAndRebuildPages;

        QueueInitialRefresh();
    }

    private void OnDisable()
    {
        if (initialRefreshRoutine != null)
        {
            StopCoroutine(initialRefreshRoutine);
            initialRefreshRoutine = null;
        }

        UnregisterControllerEvents();

        UserManager.UserChanged -= RefreshUserDataAndRebuildPages;
        SaveManager.SaveDataChanged -= RefreshUserDataAndRebuildPages;
    }

    private void CacheManagers()
    {
        if (userManager == null)
        {
            userManager = UserManager.instance;
        }
    }

    private void QueueInitialRefresh()
    {
        if (initialRefreshRoutine != null)
        {
            StopCoroutine(initialRefreshRoutine);
        }

        initialRefreshRoutine = StartCoroutine(InitialRefreshNextFrame());
    }

    private IEnumerator InitialRefreshNextFrame()
    {
        yield return null;

        initialRefreshRoutine = null;

        RefreshUserData();
        ApplySelectedFilterMode();
    }

    #endregion

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    #region User Setup

    private void RefreshUserData()
    {
        CacheManagers();

        allUsers.Clear();
        currentUser = null;

        if (userManager == null)
        {
            return;
        }

        currentUser = userManager.CurrentUser;

        List<UserData> users = userManager.GetAllUsers();

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
        if (searchController != null)
        {
            searchController.SearchButtonRequested += HandleSearchButtonRequested;
            searchController.SearchInputSubmitted += HandleSearchInputSubmitted;
            searchController.ClearSearchRequested += ClearSearch;
            searchController.FindMeRequested += FindCurrentUser;
        }

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
        if (searchController != null)
        {
            searchController.SearchButtonRequested -= HandleSearchButtonRequested;
            searchController.SearchInputSubmitted -= HandleSearchInputSubmitted;
            searchController.ClearSearchRequested -= ClearSearch;
            searchController.FindMeRequested -= FindCurrentUser;
        }

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

            case LeaderboardGameModeType.Play:
                SetupPlayMode();
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

        float rankCellWidth = 80f;
        float userIconCellWidth = 44f;
        float playerNameCellFlexibleWidth = 1f;
        float scoreCellWidth = 190f;

        currentHeaderCells.Add(CreateHeaderTextCell("Rank", rankCellWidth, 0f, 18f, TextAlignmentOptions.Center));
        currentHeaderCells.Add(CreateHeaderTextCell("Player", 0f, playerNameCellFlexibleWidth, 18f, TextAlignmentOptions.Left));
        currentHeaderCells.Add(CreateHeaderTextCell("Score", scoreCellWidth, 0f, 18f, TextAlignmentOptions.Right));

        currentRowBlueprint.Add(CreateRowTextCellSetup(LeaderboardRowCellValueType.Rank, rankCellWidth, 0f, 16f, TextAlignmentOptions.Center));
        currentRowBlueprint.Add(CreateRowImageCellSetup(LeaderboardRowCellValueType.UserIcon, userIconCellWidth, 0f));
        currentRowBlueprint.Add(CreateRowTextCellSetup(LeaderboardRowCellValueType.PlayerNameWithShortId, 0f, playerNameCellFlexibleWidth, 16f, TextAlignmentOptions.Left, maxPlayerNameCharacters, 0));
        currentRowBlueprint.Add(CreateRowTextCellSetup(LeaderboardRowCellValueType.Score, scoreCellWidth, 0f, 16f, TextAlignmentOptions.Right, 0, maxScoreDigits));

        ApplyModeSetup();
    }

    private void SetupPlayMode()
    {
        currentModeTitleText = "Play Leaderboard";
        currentSortType = LeaderboardSortType.ScoreHighest;

        currentHeaderCells.Clear();
        currentRowBlueprint.Clear();

        float rankCellWidth = 90f;
        float scoreCellWidth = 210f;
        float playerNameCellFlexibleWidth = 1f;

        currentHeaderCells.Add(CreateHeaderTextCell("Rank", rankCellWidth, 0f, 18f, TextAlignmentOptions.Center));
        currentHeaderCells.Add(CreateHeaderTextCell("Score", scoreCellWidth, 0f, 18f, TextAlignmentOptions.Right));
        currentHeaderCells.Add(CreateHeaderTextCell("Player", 0f, playerNameCellFlexibleWidth, 18f, TextAlignmentOptions.Left));

        currentRowBlueprint.Add(CreateRowTextCellSetup(LeaderboardRowCellValueType.Rank, rankCellWidth, 0f, 16f, TextAlignmentOptions.Center));
        currentRowBlueprint.Add(CreateRowTextCellSetup(LeaderboardRowCellValueType.Score, scoreCellWidth, 0f, 16f, TextAlignmentOptions.Right, 0, maxScoreDigits));
        currentRowBlueprint.Add(CreateRowTextCellSetup(LeaderboardRowCellValueType.PlayerNameWithShortId, 0f, playerNameCellFlexibleWidth, 16f, TextAlignmentOptions.Left, maxPlayerNameCharacters, 0));

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

    #region Search Setup

    #region Search Setup Entry

    private void HandleSearchButtonRequested(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            ClearSearch();
            return;
        }

        SearchUsers(searchText);
    }

    private void HandleSearchInputSubmitted(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return;
        }

        SearchUsers(searchText);
    }

    private void ClearSearch()
    {
        searchActive = false;
        currentSearchText = string.Empty;
        selectedUserId = string.Empty;

        if (searchController != null)
        {
            searchController.SetSearchActive(false);
            searchController.HideError();
        }

        RebuildPagesAndDisplayPage(1);
    }

    private void FindCurrentUser()
    {
        RefreshUserData();

        UserData userToFind = GetCurrentUserForFindMe();

        if (userToFind == null || string.IsNullOrWhiteSpace(userToFind.userId))
        {
            ShowSearchError("Current user was not found.");
            return;
        }

        AddUserToAllUsersIfMissing(userToFind);

        searchActive = false;
        currentSearchText = string.Empty;
        selectedUserId = userToFind.userId;

        if (searchController != null)
        {
            searchController.SetSearchActive(false);
            searchController.HideError();
        }

        RebuildPagesAndDisplayUser(userToFind.userId);
    }

    #endregion

    #region Search Build Methods

    private void SearchUsers(string searchText)
    {
        searchText = searchText.Trim();

        if (searchText.Length < MinimumSearchCharacters)
        {
            ShowSearchError($"Search needs {MinimumSearchCharacters} or more characters.");
            return;
        }

        BuildRankedUsersFromCurrentMode();

        List<LeaderboardUserRankData> searchResults = GetSearchResults(searchText);

        if (searchResults.Count == 0)
        {
            ShowSearchError("No users found.");
            return;
        }

        searchActive = true;
        currentSearchText = searchText;
        selectedUserId = string.Empty;

        displayRankedUsers.Clear();

        for (int i = 0; i < searchResults.Count; i++)
        {
            displayRankedUsers.Add(searchResults[i]);
        }

        BuildPageListsFromDisplayRankedUsers();
        DisplayPage(1);

        if (searchController != null)
        {
            searchController.SetSearchActive(true);
            searchController.HideError();
        }
    }

    private void BuildDisplayRankedUsers()
    {
        displayRankedUsers.Clear();

        if (!searchActive || string.IsNullOrWhiteSpace(currentSearchText))
        {
            for (int i = 0; i < rankedUsers.Count; i++)
            {
                displayRankedUsers.Add(rankedUsers[i]);
            }

            return;
        }

        List<LeaderboardUserRankData> searchResults = GetSearchResults(currentSearchText);

        for (int i = 0; i < searchResults.Count; i++)
        {
            displayRankedUsers.Add(searchResults[i]);
        }
    }

    private List<LeaderboardUserRankData> GetSearchResults(string searchText)
    {
        List<LeaderboardUserRankData> searchResults = new();

        for (int i = 0; i < rankedUsers.Count; i++)
        {
            LeaderboardUserRankData rankedUser = rankedUsers[i];

            if (rankedUser == null || rankedUser.userData == null)
            {
                continue;
            }

            if (DoesUserMatchSearch(rankedUser.userData, searchText))
            {
                searchResults.Add(rankedUser);
            }
        }

        return searchResults;
    }

    #endregion

    #region Find Me Methods

    private void RebuildPagesAndDisplayUser(string userId)
    {
        BuildRankedUsersFromCurrentMode();
        BuildDisplayRankedUsers();
        BuildPageListsFromDisplayRankedUsers();

        int userPage = GetPageNumberForUser(userId);

        if (userPage <= 0)
        {
            ShowSearchError("Current user was not found.");
            return;
        }

        selectedUserId = userId;

        DisplayPage(userPage);

        if (listController != null)
        {
            listController.SelectUserAndScrollIntoView(userId);
        }
    }

    private int GetPageNumberForUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return -1;
        }

        for (int pageIndex = 0; pageIndex < pageLists.Count; pageIndex++)
        {
            List<LeaderboardUserRankData> page = pageLists[pageIndex];

            for (int i = 0; i < page.Count; i++)
            {
                if (page[i] == null || page[i].userData == null)
                {
                    continue;
                }

                if (IsSameUserId(page[i].userData.userId, userId))
                {
                    return pageIndex + 1;
                }
            }
        }

        return -1;
    }

    #endregion

    #region Search Helpers

    private bool DoesUserMatchSearch(UserData userData, string searchText)
    {
        if (userData == null || string.IsNullOrWhiteSpace(searchText))
        {
            return false;
        }

        searchText = searchText.Trim();

        string playerName = GetUserName(userData);
        string userId = string.IsNullOrWhiteSpace(userData.userId) ? string.Empty : userData.userId.Trim();
        string shortId = GetShortUserId(userId);
        string displayName = GetPlayerNameWithShortId(userData);

        return ContainsSearchPattern(playerName, searchText) ||
               ContainsSearchPattern(userId, searchText) ||
               ContainsSearchPattern(shortId, searchText) ||
               ContainsSearchPattern(displayName, searchText);
    }

    private bool ContainsSearchPattern(string sourceText, string searchText)
    {
        if (string.IsNullOrWhiteSpace(sourceText) || string.IsNullOrWhiteSpace(searchText))
        {
            return false;
        }

        return sourceText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private string GetPlayerNameWithShortId(UserData userData)
    {
        if (userData == null)
        {
            return string.Empty;
        }

        string playerName = GetUserName(userData);
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

    private void ShowSearchError(string message)
    {
        if (searchController == null)
        {
            return;
        }

        searchController.ShowError(message);
    }

    private UserData GetCurrentUserForFindMe()
    {
        if (currentUser != null && !string.IsNullOrWhiteSpace(currentUser.userId))
        {
            return currentUser;
        }

        if (userManager == null)
        {
            return null;
        }

        currentUser = userManager.CurrentUser;

        return currentUser;
    }

    private void AddUserToAllUsersIfMissing(UserData userData)
    {
        if (userData == null || string.IsNullOrWhiteSpace(userData.userId))
        {
            return;
        }

        for (int i = 0; i < allUsers.Count; i++)
        {
            if (allUsers[i] == null)
            {
                continue;
            }

            if (IsSameUserId(allUsers[i].userId, userData.userId))
            {
                return;
            }
        }

        allUsers.Add(userData);
    }

    private bool IsSameUserId(string firstUserId, string secondUserId)
    {
        if (string.IsNullOrWhiteSpace(firstUserId) || string.IsNullOrWhiteSpace(secondUserId))
        {
            return false;
        }

        return string.Equals(
            firstUserId.Trim(),
            secondUserId.Trim(),
            StringComparison.OrdinalIgnoreCase
        );
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
        BuildDisplayRankedUsers();
        BuildPageListsFromDisplayRankedUsers();
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

    private void BuildPageListsFromDisplayRankedUsers()
    {
        pageLists.Clear();

        currentPageSize = Mathf.Max(1, currentPageSize);
        totalPages = Mathf.Max(1, Mathf.CeilToInt(displayRankedUsers.Count / (float)currentPageSize));

        for (int i = 0; i < totalPages; i++)
        {
            pageLists.Add(new List<LeaderboardUserRankData>());
        }

        for (int i = 0; i < displayRankedUsers.Count; i++)
        {
            int pageIndex = i / currentPageSize;

            if (pageIndex < 0 || pageIndex >= pageLists.Count)
            {
                continue;
            }

            pageLists[pageIndex].Add(displayRankedUsers[i]);
        }
    }

    #endregion

    #region Page Display

    private void DisplayPage(int pageNumber)
    {
        if (pageLists.Count == 0)
        {
            BuildPageListsFromDisplayRankedUsers();
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

        listController.SetRankedUserRows(pageData, selectedUserId);
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