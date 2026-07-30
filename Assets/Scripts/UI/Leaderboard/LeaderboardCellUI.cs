using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardCellUI : MonoBehaviour
{
    [Header("Cell Objects")]
    [SerializeField] private GameObject imageObject;
    [SerializeField] private GameObject textObject;

    [Header("Components")]
    [SerializeField] private Image cellImage;
    [SerializeField] private TMP_Text cellText;
    [SerializeField] private UIThemeText themeText;
    [SerializeField] private LayoutElement layoutElement;

    public void SetTextCell(
        string textValue,
        UIThemeTextType textType,
        float preferredWidth,
        float flexibleWidth,
        float fontSize,
        TextAlignmentOptions alignment
    )
    {
        SetLayout(preferredWidth, flexibleWidth);

        SetImageActive(false);
        SetTextActive(true);

        if (cellText != null)
        {
            cellText.text = textValue;
            cellText.fontSize = fontSize;
            cellText.alignment = alignment;
        }

        if (themeText != null)
        {
            themeText.SetTextType(textType);
        }
    }

    public void SetImageCell(
        Sprite sprite,
        float preferredWidth,
        float flexibleWidth
    )
    {
        SetLayout(preferredWidth, flexibleWidth);

        SetTextActive(false);
        SetImageActive(true);

        if (cellImage != null)
        {
            cellImage.sprite = sprite;
            cellImage.preserveAspect = true;
        }
    }

    public void SetTextAndImageCell(
        string textValue,
        Sprite sprite,
        UIThemeTextType textType,
        float preferredWidth,
        float flexibleWidth,
        float fontSize,
        TextAlignmentOptions alignment
    )
    {
        SetLayout(preferredWidth, flexibleWidth);

        SetImageActive(true);
        SetTextActive(true);

        if (cellImage != null)
        {
            cellImage.sprite = sprite;
            cellImage.preserveAspect = true;
        }

        if (cellText != null)
        {
            cellText.text = textValue;
            cellText.fontSize = fontSize;
            cellText.alignment = alignment;
        }

        if (themeText != null)
        {
            themeText.SetTextType(textType);
        }
    }

    public void ClearCell()
    {
        SetImageActive(false);
        SetTextActive(false);

        if (cellText != null)
        {
            cellText.text = string.Empty;
        }

        if (cellImage != null)
        {
            cellImage.sprite = null;
        }

        gameObject.SetActive(false);
    }

    public void ShowCell()
    {
        gameObject.SetActive(true);
    }

    private void SetLayout(float preferredWidth, float flexibleWidth)
    {
        if (layoutElement == null)
        {
            return;
        }

        layoutElement.preferredWidth = preferredWidth;
        layoutElement.flexibleWidth = flexibleWidth;
    }

    private void SetImageActive(bool isActive)
    {
        if (imageObject != null)
        {
            imageObject.SetActive(isActive);
            return;
        }

        if (cellImage != null)
        {
            cellImage.gameObject.SetActive(isActive);
        }
    }

    private void SetTextActive(bool isActive)
    {
        if (textObject != null)
        {
            textObject.SetActive(isActive);
            return;
        }

        if (cellText != null)
        {
            cellText.gameObject.SetActive(isActive);
        }
    }
}