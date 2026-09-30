using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BingoGame.UI.ThemeTest
{
    [DisallowMultipleComponent]
    public sealed class ThemeTestSectionCardManager : MonoBehaviour
    {
        [Header("Card Prefabs")]
        [Tooltip("Shared prefab for Background, Button, Text, Input, Dropdown, Scroll, Slider, and Toggle entries.")]
        [SerializeField] private ThemeTestCardController cardPrefab;
        [Tooltip("Separate prefab for Board entries.")]
        [SerializeField] private ThemeTestBoardCardController boardCardPrefab;

        private struct Entry
        {
            public object source;
            public int type;
        }

        private sealed class Card
        {
            public Entry entry;
            public ThemeTestCardController component;
            public ThemeTestBoardCardController board;
            public bool used;
            public Transform Root => component != null ? component.transform : board != null ? board.transform : null;
        }

        private readonly List<Entry> entries = new();
        private readonly List<Card> cards = new();
        private readonly List<Card> nextCards = new();
        private UIThemeData theme;
        private UIThemeSectionType sectionType;
        private RectTransform content;
        private bool configured;

        public void Configure(UIThemeData _theme, UIThemeSectionType _sectionType, RectTransform _content)
        {
            theme = _theme;
            sectionType = _sectionType;
            content = _content;
            bool prefabReady = sectionType == UIThemeSectionType.Board
                ? boardCardPrefab != null && boardCardPrefab.HasRequiredUI
                : cardPrefab != null && cardPrefab.HasRequiredUI;
            configured = theme != null && content != null && prefabReady;
            if (!configured)
            {
                Debug.LogWarning("Assign the appropriate card prefab and all its UI references before building section cards.", this);
                return;
            }

            ClearCards();
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                GameObject child = content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        public void RefreshCards()
        {
            if (!configured || theme == null || content == null)
                return;

            UIThemeManager manager = ThemeTestSelectorSectionManager.ReadyThemeManager;
            if (manager == null || manager != UIThemeManager.instance || !manager.isActiveAndEnabled)
                return;

            CollectEntries();
            nextCards.Clear();
            for (int i = 0; i < cards.Count; i++)
                cards[i].used = false;

            bool changed = false;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                Card card = null;
                for (int j = 0; j < cards.Count; j++)
                {
                    Card candidate = cards[j];
                    if (!candidate.used && candidate.Root != null && ReferenceEquals(candidate.entry.source, entry.source)
                        && candidate.entry.type == entry.type)
                    {
                        card = candidate;
                        break;
                    }
                }

                if (card == null)
                {
                    card = SpawnCard(entry);
                    changed = true;
                }

                card.used = true;
                nextCards.Add(card);
                if (card.Root.GetSiblingIndex() != i)
                {
                    card.Root.SetSiblingIndex(i);
                    changed = true;
                }
                if (card.component != null)
                    card.component.RefreshPreview();
                else if (card.board != null)
                    card.board.RefreshPreview();
            }

            for (int i = 0; i < cards.Count; i++)
            {
                if (!cards[i].used)
                {
                    RemoveCard(cards[i]);
                    changed = true;
                }
            }

            cards.Clear();
            cards.AddRange(nextCards);
            if (changed)
                LayoutRebuilder.MarkLayoutForRebuild(content);
        }

        private Card SpawnCard(Entry _entry)
        {
            Card card = new() { entry = _entry };
            if (sectionType == UIThemeSectionType.Board)
            {
                card.board = Instantiate(boardCardPrefab, content, false);
                card.board.gameObject.SetActive(true);
                card.board.Configure((UIThemeBoardType)_entry.type);
            }
            else
            {
                card.component = Instantiate(cardPrefab, content, false);
                card.component.gameObject.SetActive(true);
                card.component.Configure(sectionType, GetEntryType(_entry.type));
            }

            return card;
        }

        private void CollectEntries()
        {
            entries.Clear();
            switch (sectionType)
            {
                case UIThemeSectionType.Background: AddEntries(theme.BackgroundStyles, _style => (int)_style.BackgroundType); break;
                case UIThemeSectionType.Button: AddEntries(theme.ButtonStyles, _style => (int)_style.ButtonType); break;
                case UIThemeSectionType.Text: AddEntries(theme.TextStyles, _style => (int)_style.TextType); break;
                case UIThemeSectionType.Input: AddEntries(theme.InputStyles, _style => (int)_style.InputType); break;
                case UIThemeSectionType.Dropdown: AddEntries(theme.DropdownStyles, _style => (int)_style.DropdownType); break;
                case UIThemeSectionType.Scroll: AddEntries(theme.ScrollStyles, _style => (int)_style.ScrollType); break;
                case UIThemeSectionType.Slider: AddEntries(theme.SliderStyles, _style => (int)_style.SliderType); break;
                case UIThemeSectionType.Toggle: AddEntries(theme.ToggleStyles, _style => (int)_style.ToggleType); break;
                case UIThemeSectionType.Board: AddEntries(theme.BoardStyles, _style => (int)_style.BoardType); break;
            }
        }

        private void AddEntries<T>(IReadOnlyList<T> _styles, Func<T, int> _getType) where T : class
        {
            if (_styles == null)
                return;
            for (int i = 0; i < _styles.Count; i++)
            {
                T style = _styles[i];
                if (style != null)
                    entries.Add(new Entry { source = style, type = _getType(style) });
            }
        }

        private Enum GetEntryType(int _type)
        {
            switch (sectionType)
            {
                case UIThemeSectionType.Background: return (UIThemeBackgroundType)_type;
                case UIThemeSectionType.Button: return (UIThemeButtonType)_type;
                case UIThemeSectionType.Text: return (UIThemeTextType)_type;
                case UIThemeSectionType.Input: return (UIThemeInputType)_type;
                case UIThemeSectionType.Dropdown: return (UIThemeDropdownType)_type;
                case UIThemeSectionType.Scroll: return (UIThemeScrollType)_type;
                case UIThemeSectionType.Slider: return (UIThemeSliderType)_type;
                case UIThemeSectionType.Toggle: return (UIThemeToggleType)_type;
                default: throw new ArgumentOutOfRangeException(nameof(sectionType));
            }
        }

        private static void RemoveCard(Card _card)
        {
            Transform root = _card.Root;
            if (root == null)
                return;
            root.gameObject.SetActive(false);
            Destroy(root.gameObject);
        }

        private void ClearCards()
        {
            for (int i = 0; i < cards.Count; i++)
                RemoveCard(cards[i]);
            cards.Clear();
        }

        private void OnDestroy()
        {
            ClearCards();
        }
    }
}
