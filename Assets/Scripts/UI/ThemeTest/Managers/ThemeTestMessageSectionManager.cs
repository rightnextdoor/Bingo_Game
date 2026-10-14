using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ThemeTestMessageSectionManager : MonoBehaviour
{
    [Serializable]
    private class PreviewReplacement
    {
        public string token;
        public string value;
        public bool useNumberColor;
    }

    private class Preview
    {
        public UIMessageData data;
        public ThemeTestMessageCardController card;
        public TMP_FontAsset originalFont;
        public float titleFontSize;
        public Vector2 titleSize;
        public Vector2 messageSize;
        public bool needsFit;
    }

    [SerializeField] private ThemeTestMessageCardController cardPrefab;
    [SerializeField] private RectTransform content;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;
    [SerializeField] private List<PreviewReplacement> previewReplacements = new List<PreviewReplacement>
    {
        new PreviewReplacement { token = "letter", value = "N" },
        new PreviewReplacement { token = "number", value = "17", useNumberColor = true }
    };

    private readonly List<Preview> previews = new List<Preview>();
    private readonly Dictionary<string, string> replacements = new Dictionary<string, string>();
    private UIMessageCatalog builtCatalog;
    private ThemeTestMessageCardController builtPrefab;
    private RectTransform builtContent;
    private float nextRefresh;
    private bool warnedAboutSetup;

    private void Awake()
    {
        RefreshSection();
    }

    private void OnEnable()
    {
        _ = CanvasUpdateRegistry.instance;
        Canvas.willRenderCanvases += FitCards;
        nextRefresh = 0f;
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= FitCards;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
        RefreshSection();
    }

    private void RefreshSection()
    {
        if (content == null || cardPrefab == null || !cardPrefab.HasRequiredUI)
        {
            if (!warnedAboutSetup)
                Debug.LogWarning("Message preview requires Content and a Card Prefab with all three UI references assigned.", this);
            warnedAboutSetup = true;
            return;
        }

        warnedAboutSetup = false;
        UIMessageCatalog catalog = UIMessageCatalog.instance;
        if (catalog == null)
            return;

        IReadOnlyList<UIMessageData> messages = catalog.Messages;
        bool rebuild = builtCatalog != catalog || builtPrefab != cardPrefab
            || builtContent != content || previews.Count != messages.Count;
        for (int i = 0; !rebuild && i < messages.Count; i++)
            rebuild = previews[i].data != messages[i] || (messages[i] != null && previews[i].card == null);

        if (rebuild)
            Rebuild(catalog, messages);

        UIMessageDefaultStyle defaultMessage = catalog.GetDefaultMessage();
        Sprite defaultImage = defaultMessage != null ? defaultMessage.BackgroundImage : null;
        foreach (Preview preview in previews)
        {
            if (preview.data != null && preview.card != null)
                ApplyMessage(preview, defaultImage);
        }
    }

    private void ApplyMessage(Preview preview, Sprite defaultImage)
    {
        UIMessageData data = preview.data;
        ThemeTestMessageCardController card = preview.card;
        replacements.Clear();
        foreach (PreviewReplacement replacement in previewReplacements)
        {
            if (replacement == null || string.IsNullOrWhiteSpace(replacement.token))
                continue;
            string value = replacement.value ?? string.Empty;
            if (replacement.useNumberColor)
                value = $"<color=#{data.GetNumberColorHex()}>{value}</color>";
            replacements[replacement.token] = value;
        }

        card.CardTitle.text = data.name;
        card.CardTitle.richText = false;
        card.CardTitle.alignment = TextAlignmentOptions.Center;
        card.CardTitle.raycastTarget = false;

        Image background = card.MessageBackground;
        background.gameObject.SetActive(true);
        background.color = data.BackgroundColor;
        background.raycastTarget = false;
        switch (data.ImageMode)
        {
            case TooltipImageMode.Default:
                background.sprite = defaultImage;
                break;
            case TooltipImageMode.Custom:
                background.sprite = data.CustomImage != null ? data.CustomImage : defaultImage;
                break;
            case TooltipImageMode.None:
                background.sprite = null;
                break;
        }

        TMP_Text text = card.MessageText;
        text.gameObject.SetActive(true);
        text.font = data.FontAsset != null ? data.FontAsset : preview.originalFont;
        text.color = data.TextColor;
        text.richText = true;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.text = data.BuildMessage(replacements);
        preview.needsFit = true;
    }

    private void FitCards()
    {
        foreach (Preview preview in previews)
        {
            ThemeTestMessageCardController card = preview.card;
            if (preview.data == null || card == null || !card.isActiveAndEnabled || !card.HasRequiredUI)
                continue;

            Vector2 titleSize = card.CardTitle.rectTransform.rect.size;
            Vector2 messageSize = card.MessageText.rectTransform.rect.size;
            if (!preview.needsFit && titleSize == preview.titleSize && messageSize == preview.messageSize)
                continue;
            if (titleSize.x <= 0f || titleSize.y <= 0f || messageSize.x <= 0f || messageSize.y <= 0f)
                continue;

            ThemeTestTextFitter.Fit(card.CardTitle, preview.titleFontSize, card.MinimumFontSize);
            ThemeTestTextFitter.Fit(card.MessageText, preview.data.FontSize, card.MinimumFontSize);
            preview.titleSize = titleSize;
            preview.messageSize = messageSize;
            preview.needsFit = false;
        }
    }

    private void Rebuild(UIMessageCatalog catalog, IReadOnlyList<UIMessageData> messages)
    {
        if (builtContent != content)
            DestroyGeneratedCards();

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        previews.Clear();
        foreach (UIMessageData data in messages)
        {
            Preview preview = new Preview { data = data };
            previews.Add(preview);
            if (data == null)
                continue;

            preview.card = Instantiate(cardPrefab, content, false);
            preview.card.name = $"MessagePreview_{data.name}";
            preview.card.gameObject.SetActive(true);
            preview.originalFont = preview.card.MessageText.font;
            TMP_Text title = preview.card.CardTitle;
            preview.titleFontSize = title.enableAutoSizing ? title.fontSizeMax : title.fontSize;
        }

        builtCatalog = catalog;
        builtPrefab = cardPrefab;
        builtContent = content;
        LayoutRebuilder.MarkLayoutForRebuild(content);
    }

    private void OnDestroy()
    {
        DestroyGeneratedCards();
    }

    private void DestroyGeneratedCards()
    {
        foreach (Preview preview in previews)
        {
            if (preview.card == null)
                continue;
            preview.card.gameObject.SetActive(false);
            Destroy(preview.card.gameObject);
        }
        previews.Clear();
    }
}
