using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LeaderboardFilterController : MonoBehaviour
{
    [Header("Dropdowns")]
    [SerializeField] private TMP_Dropdown gameModeDropdown;
    [SerializeField] private TMP_Dropdown pageSizeDropdown;

    public event Action<LeaderboardGameModeType> GameModeChanged;
    public event Action<int> PageSizeChanged;

    private readonly List<LeaderboardGameModeType> gameModeOptions = new()
    {
        LeaderboardGameModeType.Overall
    };

    private readonly List<LeaderboardPageSizeType> pageSizeOptions = new()
    {
        LeaderboardPageSizeType.Show10,
        LeaderboardPageSizeType.Show25,
        LeaderboardPageSizeType.Show50,
        LeaderboardPageSizeType.Show100
    };

    private void Awake()
    {
        BuildDropdownOptions();
    }

    private void OnEnable()
    {
        if (gameModeDropdown != null)
        {
            gameModeDropdown.onValueChanged.AddListener(HandleGameModeChanged);
        }

        if (pageSizeDropdown != null)
        {
            pageSizeDropdown.onValueChanged.AddListener(HandlePageSizeChanged);
        }
    }

    private void OnDisable()
    {
        if (gameModeDropdown != null)
        {
            gameModeDropdown.onValueChanged.RemoveListener(HandleGameModeChanged);
        }

        if (pageSizeDropdown != null)
        {
            pageSizeDropdown.onValueChanged.RemoveListener(HandlePageSizeChanged);
        }
    }

    public LeaderboardGameModeType GetSelectedGameMode()
    {
        if (gameModeDropdown == null)
        {
            return LeaderboardGameModeType.Overall;
        }

        int index = Mathf.Clamp(gameModeDropdown.value, 0, gameModeOptions.Count - 1);

        return gameModeOptions[index];
    }

    public int GetSelectedPageSize()
    {
        if (pageSizeDropdown == null)
        {
            return 10;
        }

        int index = Mathf.Clamp(pageSizeDropdown.value, 0, pageSizeOptions.Count - 1);

        return GetPageSizeValue(pageSizeOptions[index]);
    }

    public void SetGameMode(LeaderboardGameModeType gameMode)
    {
        if (gameModeDropdown == null)
        {
            return;
        }

        int index = gameModeOptions.IndexOf(gameMode);

        if (index < 0)
        {
            index = 0;
        }

        gameModeDropdown.SetValueWithoutNotify(index);
        gameModeDropdown.RefreshShownValue();
    }

    public void SetPageSize(LeaderboardPageSizeType pageSize)
    {
        if (pageSizeDropdown == null)
        {
            return;
        }

        int index = pageSizeOptions.IndexOf(pageSize);

        if (index < 0)
        {
            index = 0;
        }

        pageSizeDropdown.SetValueWithoutNotify(index);
        pageSizeDropdown.RefreshShownValue();
    }

    private void BuildDropdownOptions()
    {
        BuildGameModeDropdown();
        BuildPageSizeDropdown();
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
        gameModeDropdown.SetValueWithoutNotify(0);
        gameModeDropdown.RefreshShownValue();
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
        pageSizeDropdown.SetValueWithoutNotify(0);
        pageSizeDropdown.RefreshShownValue();
    }

    private void HandleGameModeChanged(int index)
    {
        index = Mathf.Clamp(index, 0, gameModeOptions.Count - 1);

        GameModeChanged?.Invoke(gameModeOptions[index]);
    }

    private void HandlePageSizeChanged(int index)
    {
        index = Mathf.Clamp(index, 0, pageSizeOptions.Count - 1);

        int pageSize = GetPageSizeValue(pageSizeOptions[index]);

        PageSizeChanged?.Invoke(pageSize);
    }

    private string GetGameModeLabel(LeaderboardGameModeType gameMode)
    {
        switch (gameMode)
        {
            case LeaderboardGameModeType.Overall:
                return "Overall";

            default:
                return gameMode.ToString();
        }
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