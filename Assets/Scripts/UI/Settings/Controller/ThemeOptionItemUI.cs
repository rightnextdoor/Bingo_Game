using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ThemeOptionItemUI : MonoBehaviour
{
    #region Inspector Fields

    [Header("Text")]
    [SerializeField] private TMP_Text themeNameText;

    [Header("Preview Images")]
    [SerializeField] private Image previewImageOne;
    [SerializeField] private Image previewImageTwo;
    [SerializeField] private Image previewImageThree;

    [Header("Radio Button")]
    [SerializeField] private Toggle radioToggle;
    [SerializeField] private Image radioBackgroundImage;
    [SerializeField] private Image radioCheckmarkImage;

    #endregion

    #region Private Fields

    private UIThemeType themeType;
    private Action<UIThemeType> selectedCallback;
    private bool isSettingValues;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        if (radioToggle != null)
        {
            radioToggle.onValueChanged.AddListener(OnRadioToggleChanged);

            if (radioCheckmarkImage != null)
            {
                radioToggle.graphic = radioCheckmarkImage;
            }

            if (radioBackgroundImage != null)
            {
                radioToggle.targetGraphic = radioBackgroundImage;
            }
        }
    }

    private void OnDestroy()
    {
        if (radioToggle != null)
        {
            radioToggle.onValueChanged.RemoveListener(OnRadioToggleChanged);
        }
    }

    #endregion

    #region Public Setup

    public void Setup(
        UIThemeData themeData,
        UIThemeType selectedThemeType,
        ToggleGroup toggleGroup,
        Action<UIThemeType> onSelected)
    {
        if (themeData == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        themeType = themeData.ThemeType;
        selectedCallback = onSelected;

        SetThemeName(themeData.ThemeType);
        SetPreviewImages(themeData);

        isSettingValues = true;

        if (radioToggle != null)
        {
            radioToggle.group = toggleGroup;
            radioToggle.SetIsOnWithoutNotify(themeData.ThemeType == selectedThemeType);
        }

        isSettingValues = false;
    }

    public UIThemeType GetThemeType()
    {
        return themeType;
    }

    public Toggle GetToggle()
    {
        return radioToggle;
    }

    public RectTransform GetRectTransform()
    {
        return transform as RectTransform;
    }

    public void SetSelected(bool isSelected)
    {
        if (radioToggle == null)
        {
            return;
        }

        isSettingValues = true;
        radioToggle.SetIsOnWithoutNotify(isSelected);
        isSettingValues = false;
    }

    #endregion

    #region Name

    private void SetThemeName(UIThemeType newThemeType)
    {
        if (themeNameText == null)
        {
            return;
        }

        themeNameText.text = newThemeType.ToString();
    }

    #endregion

    #region Preview

    private void SetPreviewImages(UIThemeData themeData)
    {
        Image[] previewImages =
        {
            previewImageOne,
            previewImageTwo,
            previewImageThree
        };

        for (int i = 0; i < previewImages.Length; i++)
        {
            Image previewImage = previewImages[i];

            if (previewImage == null)
            {
                continue;
            }

            if (themeData.BackgroundStyles == null || i >= themeData.BackgroundStyles.Count)
            {
                previewImage.gameObject.SetActive(false);
                continue;
            }

            UIThemeBackgroundStyle backgroundStyle = themeData.BackgroundStyles[i];

            if (backgroundStyle == null || backgroundStyle.Style == null)
            {
                previewImage.gameObject.SetActive(false);
                continue;
            }

            previewImage.gameObject.SetActive(true);
            UIThemeApplier.ApplyImageStyle(previewImage, backgroundStyle.Style);
        }
    }

    #endregion

    #region Toggle Event

    private void OnRadioToggleChanged(bool isOn)
    {
        if (isSettingValues)
        {
            return;
        }

        if (!isOn)
        {
            return;
        }

        selectedCallback?.Invoke(themeType);
    }

    #endregion
}