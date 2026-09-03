using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class GameBallItemController : MonoBehaviour
{
    private Image ballBodyImage;
    private TMP_Text letterText;
    private TMP_Text numberText;

    public RectTransform RectTransform => transform as RectTransform;

    private void Awake()
    {
        CacheReferences();
        ApplyFixedColors();
        Clear();
    }

    public void Apply(GameBallPresentationData presentationData)
    {
        CacheReferences();
        ApplyFixedColors();

        if (letterText != null)
        {
            letterText.text = presentationData.Letter.ToString();
            letterText.color = presentationData.LetterColor;
        }

        if (numberText != null)
        {
            numberText.text = presentationData.Number.ToString();
        }
    }

    public void Clear()
    {
        if (letterText != null)
        {
            letterText.text = string.Empty;
        }

        if (numberText != null)
        {
            numberText.text = string.Empty;
        }
    }

    private void CacheReferences()
    {
        ballBodyImage ??= GetComponent<Image>();

        if (letterText != null && numberText != null)
        {
            return;
        }

        TMP_Text[] textComponents = GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < textComponents.Length; i++)
        {
            TMP_Text textComponent = textComponents[i];

            if (textComponent == null)
            {
                continue;
            }

            if (letterText == null && textComponent.name == "LetterText")
            {
                letterText = textComponent;
            }
            else if (numberText == null && textComponent.name == "NumberText")
            {
                numberText = textComponent;
            }
        }
    }

    private void ApplyFixedColors()
    {
        if (ballBodyImage != null)
        {
            ballBodyImage.color = Color.white;
        }

        if (numberText != null)
        {
            numberText.color = Color.black;
        }
    }
}
