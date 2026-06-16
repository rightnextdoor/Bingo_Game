using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsSoundController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Master Volume")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_Text masterVolumeValueText;

    [Header("Music Volume")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TMP_Text musicVolumeValueText;

    [Header("Sound Volume")]
    [SerializeField] private Slider soundVolumeSlider;
    [SerializeField] private TMP_Text soundVolumeValueText;

    #endregion

    #region Events

    public event Action<float> MasterVolumeChanged;
    public event Action<float> MusicVolumeChanged;
    public event Action<float> SoundVolumeChanged;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        SetupSlider(masterVolumeSlider);
        SetupSlider(musicVolumeSlider);
        SetupSlider(soundVolumeSlider);
    }

    private void OnEnable()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeSliderChanged);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeSliderChanged);
        }

        if (soundVolumeSlider != null)
        {
            soundVolumeSlider.onValueChanged.AddListener(OnSoundVolumeSliderChanged);
        }
    }

    private void OnDisable()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeSliderChanged);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeSliderChanged);
        }

        if (soundVolumeSlider != null)
        {
            soundVolumeSlider.onValueChanged.RemoveListener(OnSoundVolumeSliderChanged);
        }
    }

    #endregion

    #region Public Setup Methods

    public void SetSoundValues(float masterVolume, float musicVolume, float soundVolume)
    {
        SetMasterVolume(masterVolume);
        SetMusicVolume(musicVolume);
        SetSoundVolume(soundVolume);
    }

    public void SetMasterVolume(float value)
    {
        value = Mathf.Clamp01(value);

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(value);
        }

        UpdateMasterVolumeText(value);
    }

    public void SetMusicVolume(float value)
    {
        value = Mathf.Clamp01(value);

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.SetValueWithoutNotify(value);
        }

        UpdateMusicVolumeText(value);
    }

    public void SetSoundVolume(float value)
    {
        value = Mathf.Clamp01(value);

        if (soundVolumeSlider != null)
        {
            soundVolumeSlider.SetValueWithoutNotify(value);
        }

        UpdateSoundVolumeText(value);
    }

    public void SetInteractable(bool isInteractable)
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.interactable = isInteractable;
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.interactable = isInteractable;
        }

        if (soundVolumeSlider != null)
        {
            soundVolumeSlider.interactable = isInteractable;
        }
    }

    #endregion

    #region Slider Setup

    private void SetupSlider(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
    }

    #endregion

    #region Slider Events

    private void OnMasterVolumeSliderChanged(float value)
    {
        value = Mathf.Clamp01(value);

        UpdateMasterVolumeText(value);
        MasterVolumeChanged?.Invoke(value);
    }

    private void OnMusicVolumeSliderChanged(float value)
    {
        value = Mathf.Clamp01(value);

        UpdateMusicVolumeText(value);
        MusicVolumeChanged?.Invoke(value);
    }

    private void OnSoundVolumeSliderChanged(float value)
    {
        value = Mathf.Clamp01(value);

        UpdateSoundVolumeText(value);
        SoundVolumeChanged?.Invoke(value);
    }

    #endregion

    #region Text Updates

    private void UpdateMasterVolumeText(float value)
    {
        SetVolumeText(masterVolumeValueText, value);
    }

    private void UpdateMusicVolumeText(float value)
    {
        SetVolumeText(musicVolumeValueText, value);
    }

    private void UpdateSoundVolumeText(float value)
    {
        SetVolumeText(soundVolumeValueText, value);
    }

    private void SetVolumeText(TMP_Text valueText, float value)
    {
        if (valueText == null)
        {
            return;
        }

        int percent = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f);
        valueText.text = $"{percent}%";
    }

    #endregion
}