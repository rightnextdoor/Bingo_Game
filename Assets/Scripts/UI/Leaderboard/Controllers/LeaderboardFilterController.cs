using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LeaderboardFilterController : MonoBehaviour
{
    [Header("Dropdowns")]
    [SerializeField] private TMP_Dropdown gameModeDropdown;
    [SerializeField] private TMP_Dropdown pageSizeDropdown;

    public event Action<LeaderboardModeFilter> GameModeChanged;
    public event Action<int> PageSizeChanged;

    private readonly List<LeaderboardModeFilter> gameModeOptions = new();
    private readonly List<LeaderboardPageSizeType> pageSizeOptions = new();

    private LeaderboardModeFilter selectedGameMode = LeaderboardModeFilter.CreateOverall();
    private LeaderboardPageSizeType selectedPageSize = LeaderboardPageSizeType.Show10;

    private void Awake()
    {
        BuildDropdownOptions();
    }

    private void OnEnable()
    {
        if (gameModeDropdown != null)
        {
            gameModeDropdown.onValueChanged.AddListener(OnGameModeDropdownValueChanged);
        }

        if (pageSizeDropdown != null)
        {
            pageSizeDropdown.onValueChanged.AddListener(OnPageSizeDropdownValueChanged);
        }
    }

    private void OnDisable()
    {
        if (gameModeDropdown != null)
        {
            gameModeDropdown.onValueChanged.RemoveListener(OnGameModeDropdownValueChanged);
        }

        if (pageSizeDropdown != null)
        {
            pageSizeDropdown.onValueChanged.RemoveListener(OnPageSizeDropdownValueChanged);
        }
    }

    public LeaderboardModeFilter GetSelectedGameMode()
    {
        return selectedGameMode;
    }

    public LeaderboardPageSizeType GetSelectedPageSizeType()
    {
        return selectedPageSize;
    }

    public int GetSelectedPageSize()
    {
        return GetPageSizeValue(selectedPageSize);
    }


    public void SetGameMode(LeaderboardModeFilter gameMode)
    {
        selectedGameMode = gameMode;

        if (gameModeDropdown == null)
        {
            return;
        }

        int index = gameModeOptions.IndexOf(gameMode);

        if (index < 0)
        {
            index = 0;
            selectedGameMode = gameModeOptions.Count > 0 ? gameModeOptions[index] : LeaderboardModeFilter.CreateOverall();
        }

        gameModeDropdown.SetValueWithoutNotify(index);
        gameModeDropdown.RefreshShownValue();
    }

    public void SetPageSize(LeaderboardPageSizeType pageSize)
    {
        selectedPageSize = pageSize;

        if (pageSizeDropdown == null)
        {
            return;
        }

        int index = pageSizeOptions.IndexOf(pageSize);

        if (index < 0)
        {
            index = 0;
            selectedPageSize = pageSizeOptions.Count > 0 ? pageSizeOptions[index] : LeaderboardPageSizeType.Show10;
        }

        pageSizeDropdown.SetValueWithoutNotify(index);
        pageSizeDropdown.RefreshShownValue();
    }

    private void BuildDropdownOptions()
    {
        BuildGameModeOptions();
        BuildPageSizeOptions();

        BuildGameModeDropdown();
        BuildPageSizeDropdown();
    }

    private void BuildGameModeOptions()
    {
        gameModeOptions.Clear();

        gameModeOptions.Add(LeaderboardModeFilter.CreateOverall());

        foreach (BingoGameModeType gameMode in Enum.GetValues(typeof(BingoGameModeType)))
        {
            gameModeOptions.Add(LeaderboardModeFilter.CreateGameMode(gameMode));
        }
    }

    private void BuildPageSizeOptions()
    {
        pageSizeOptions.Clear();

        foreach (LeaderboardPageSizeType pageSize in Enum.GetValues(typeof(LeaderboardPageSizeType)))
        {
            pageSizeOptions.Add(pageSize);
        }
    }

    private void BuildGameModeDropdown()
    {
        if (gameModeDropdown == null)
        {
            return;
        }

        gameModeDropdown.ClearOptions();

        List<string> optionLabels = new();

        for (int i = 0; i < gameModeOptions.Count; i++)
        {
            optionLabels.Add(GetGameModeLabel(gameModeOptions[i]));
        }

        gameModeDropdown.AddOptions(optionLabels);
        SetGameMode(selectedGameMode);
    }

    private void BuildPageSizeDropdown()
    {
        if (pageSizeDropdown == null)
        {
            return;
        }

        pageSizeDropdown.ClearOptions();

        List<string> optionLabels = new();

        for (int i = 0; i < pageSizeOptions.Count; i++)
        {
            optionLabels.Add(GetPageSizeLabel(pageSizeOptions[i]));
        }

        pageSizeDropdown.AddOptions(optionLabels);
        SetPageSize(selectedPageSize);
    }

    private void OnGameModeDropdownValueChanged(int index)
    {
        if (gameModeOptions.Count == 0)
        {
            selectedGameMode = LeaderboardModeFilter.CreateOverall();
            GameModeChanged?.Invoke(selectedGameMode);
            return;
        }

        index = Mathf.Clamp(index, 0, gameModeOptions.Count - 1);

        selectedGameMode = gameModeOptions[index];

        GameModeChanged?.Invoke(selectedGameMode);
    }

    private void OnPageSizeDropdownValueChanged(int index)
    {
        if (pageSizeOptions.Count == 0)
        {
            selectedPageSize = LeaderboardPageSizeType.Show10;
            PageSizeChanged?.Invoke(GetSelectedPageSize());
            return;
        }

        index = Mathf.Clamp(index, 0, pageSizeOptions.Count - 1);

        selectedPageSize = pageSizeOptions[index];

        PageSizeChanged?.Invoke(GetSelectedPageSize());
    }

    private string GetGameModeLabel(LeaderboardModeFilter gameMode)
    {
        if (gameMode.IsOverall)
        {
            return "Overall";
        }

        return gameMode.gameModeType.ToString();
    }

    private string GetPageSizeLabel(LeaderboardPageSizeType pageSize)
    {
        return GetPageSizeValue(pageSize).ToString();
    }

    private int GetPageSizeValue(LeaderboardPageSizeType pageSize)
    {
        switch (pageSize)
        {
            case LeaderboardPageSizeType.Show10:
                return 10;

            case LeaderboardPageSizeType.Show25:
                return 25;

            case LeaderboardPageSizeType.Show50:
                return 50;

            case LeaderboardPageSizeType.Show100:
                return 100;

            default:
                return 10;
        }
    }
}