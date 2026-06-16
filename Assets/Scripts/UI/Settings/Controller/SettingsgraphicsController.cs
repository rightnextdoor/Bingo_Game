using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SettingsGraphicsController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Dropdowns")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown screenModeDropdown;

    #endregion

    #region Events

    public event Action<int> ResolutionIndexChanged;
    public event Action<int> ScreenModeIndexChanged;

    #endregion

    #region Private Fields

    private bool isSettingValues;

    #endregion

    #region Unity Methods

    private void OnEnable()
    {
        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.AddListener(OnResolutionDropdownChanged);
        }

        if (screenModeDropdown != null)
        {
            screenModeDropdown.onValueChanged.AddListener(OnScreenModeDropdownChanged);
        }
    }

    private void OnDisable()
    {
        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.RemoveListener(OnResolutionDropdownChanged);
        }

        if (screenModeDropdown != null)
        {
            screenModeDropdown.onValueChanged.RemoveListener(OnScreenModeDropdownChanged);
        }
    }

    #endregion

    #region Public Setup Methods

    public void SetGraphicsOptions(
        IReadOnlyList<string> resolutionLabels,
        int selectedResolutionIndex,
        IReadOnlyList<string> screenModeLabels,
        int selectedScreenModeIndex)
    {
        isSettingValues = true;

        SetDropdownOptions(resolutionDropdown, resolutionLabels, selectedResolutionIndex);
        SetDropdownOptions(screenModeDropdown, screenModeLabels, selectedScreenModeIndex);

        isSettingValues = false;
    }

    public void SetResolutionOptions(IReadOnlyList<string> resolutionLabels, int selectedResolutionIndex)
    {
        isSettingValues = true;
        SetDropdownOptions(resolutionDropdown, resolutionLabels, selectedResolutionIndex);
        isSettingValues = false;
    }

    public void SetScreenModeOptions(IReadOnlyList<string> screenModeLabels, int selectedScreenModeIndex)
    {
        isSettingValues = true;
        SetDropdownOptions(screenModeDropdown, screenModeLabels, selectedScreenModeIndex);
        isSettingValues = false;
    }

    public void SetResolutionIndex(int selectedResolutionIndex)
    {
        SetDropdownValue(resolutionDropdown, selectedResolutionIndex);
    }

    public void SetScreenModeIndex(int selectedScreenModeIndex)
    {
        SetDropdownValue(screenModeDropdown, selectedScreenModeIndex);
    }

    public void SetInteractable(bool isInteractable)
    {
        if (resolutionDropdown != null)
        {
            resolutionDropdown.interactable = isInteractable;
        }

        if (screenModeDropdown != null)
        {
            screenModeDropdown.interactable = isInteractable;
        }
    }

    #endregion

    #region Dropdown Setup

    private void SetDropdownOptions(TMP_Dropdown dropdown, IReadOnlyList<string> labels, int selectedIndex)
    {
        if (dropdown == null)
        {
            return;
        }

        dropdown.ClearOptions();

        List<string> optionLabels = new();

        if (labels != null)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                optionLabels.Add(labels[i]);
            }
        }

        dropdown.AddOptions(optionLabels);

        if (optionLabels.Count == 0)
        {
            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
            dropdown.interactable = false;
            return;
        }

        dropdown.interactable = true;

        int clampedIndex = Mathf.Clamp(selectedIndex, 0, optionLabels.Count - 1);
        dropdown.SetValueWithoutNotify(clampedIndex);
        dropdown.RefreshShownValue();
    }

    private void SetDropdownValue(TMP_Dropdown dropdown, int selectedIndex)
    {
        if (dropdown == null || dropdown.options.Count == 0)
        {
            return;
        }

        int clampedIndex = Mathf.Clamp(selectedIndex, 0, dropdown.options.Count - 1);

        isSettingValues = true;
        dropdown.SetValueWithoutNotify(clampedIndex);
        dropdown.RefreshShownValue();
        isSettingValues = false;
    }

    #endregion

    #region Dropdown Events

    private void OnResolutionDropdownChanged(int selectedIndex)
    {
        if (isSettingValues)
        {
            return;
        }

        ResolutionIndexChanged?.Invoke(selectedIndex);
    }

    private void OnScreenModeDropdownChanged(int selectedIndex)
    {
        if (isSettingValues)
        {
            return;
        }

        ScreenModeIndexChanged?.Invoke(selectedIndex);
    }

    #endregion
}