using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BingoGame.UI;

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

    [Space]
    [Header("Text Fitting")]
    [SerializeField, Range(0.1f, 1f), Tooltip("Smallest fitted size as a fraction of the requested font size.")] private float minimumFontSizeScale = 0.35f;

    private float preferredFontSize;
    private bool textFitPending;
    private TMP_FontAsset fittedFont;
    private Vector2 fittedSize;

    private void OnEnable()
    {
        Canvas.willRenderCanvases += FitText;
        textFitPending = true;
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= FitText;
    }

    private void OnRectTransformDimensionsChange()
    {
        textFitPending = true;
    }

    private void FitText()
    {
        if (cellText == null || !cellText.gameObject.activeInHierarchy || preferredFontSize <= 0f) return;
        Vector2 size = cellText.rectTransform.rect.size;
        if (!textFitPending && fittedFont == cellText.font && fittedSize == size) return;
        if (size.x <= 0f || size.y <= 0f) return;

        UITextFitResult result = UITextFitter.FitSingleLine(cellText, cellText.text, preferredFontSize,
            size, Mathf.Max(1f, preferredFontSize * minimumFontSizeScale));
        cellText.enableAutoSizing = false;
        cellText.fontSize = result.FontSize;
        cellText.textWrappingMode = TextWrappingModes.NoWrap;
        cellText.maxVisibleLines = 1;
        cellText.overflowMode = TextOverflowModes.Ellipsis;
        cellText.ForceMeshUpdate(true, true);
        fittedFont = cellText.font;
        fittedSize = size;
        textFitPending = false;
    }

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
            preferredFontSize = fontSize;
            textFitPending = true;
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
            preferredFontSize = fontSize;
            textFitPending = true;
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
