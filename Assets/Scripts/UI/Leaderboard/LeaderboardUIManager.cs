using TMPro;
using UnityEngine;

public class LeaderboardUIManager : MonoBehaviour
{
    [Header("Title")]
    [SerializeField] private TMP_Text leaderboardTitleText;

    [Header("Controllers")]
    [SerializeField] private LeaderboardSearchController searchController;
    [SerializeField] private LeaderboardFilterController filterController;
    [SerializeField] private LeaderboardHeaderController headerController;
    [SerializeField] private LeaderboardListController listController;
    [SerializeField] private LeaderboardPageController pageController;

    private void OnEnable()
    {
        RegisterControllerEvents();
    }

    private void OnDisable()
    {
        UnregisterControllerEvents();
    }

    private void RegisterControllerEvents()
    {
        if (searchController != null)
        {
            searchController.SearchRequested += HandleSearchRequested;
            searchController.FindMeRequested += HandleFindMeRequested;
        }

        if (filterController != null)
        {
            filterController.GameModeChanged += HandleGameModeChanged;
            filterController.PageSizeChanged += HandlePageSizeChanged;
        }

        if (listController != null)
        {
            listController.RowClicked += HandleRowClicked;
        }

        if (pageController != null)
        {
            pageController.PreviousPageRequested += HandlePreviousPageRequested;
            pageController.NextPageRequested += HandleNextPageRequested;
            pageController.PageRequested += HandlePageRequested;
        }
    }

    private void UnregisterControllerEvents()
    {
        if (searchController != null)
        {
            searchController.SearchRequested -= HandleSearchRequested;
            searchController.FindMeRequested -= HandleFindMeRequested;
        }

        if (filterController != null)
        {
            filterController.GameModeChanged -= HandleGameModeChanged;
            filterController.PageSizeChanged -= HandlePageSizeChanged;
        }

        if (listController != null)
        {
            listController.RowClicked -= HandleRowClicked;
        }

        if (pageController != null)
        {
            pageController.PreviousPageRequested -= HandlePreviousPageRequested;
            pageController.NextPageRequested -= HandleNextPageRequested;
            pageController.PageRequested -= HandlePageRequested;
        }
    }

    public void SetLeaderboardTitle(string titleText)
    {
        if (leaderboardTitleText == null)
        {
            return;
        }

        leaderboardTitleText.text = titleText;
    }

    private void HandleSearchRequested(string searchText)
    {
    }

    private void HandleFindMeRequested()
    {
    }

    private void HandleGameModeChanged(LeaderboardGameModeType gameMode)
    {
    }

    private void HandlePageSizeChanged(int pageSize)
    {
    }

    private void HandleRowClicked(string userId)
    {
    }

    private void HandlePreviousPageRequested()
    {
    }

    private void HandleNextPageRequested()
    {
    }

    private void HandlePageRequested(int pageNumber)
    {
    }
}