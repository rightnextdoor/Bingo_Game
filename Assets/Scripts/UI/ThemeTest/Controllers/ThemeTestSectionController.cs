using TMPro;
using UnityEngine;

namespace BingoGame.UI.ThemeTest
{
    [DisallowMultipleComponent]
    public sealed class ThemeTestSectionController : MonoBehaviour
    {
        [Header("Section UI")]
        [Tooltip("Stationary title above the section's horizontal scroll view.")]
        [SerializeField] private TMP_Text sectionTitle;
        [Tooltip("Content inside ItemsScrollView, reserved for preview cards.")]
        [SerializeField] private RectTransform itemsContent;

        [Space]
        [Header("Cards")]
        [Tooltip("Card manager on this section root.")]
        [SerializeField] private ThemeTestSectionCardManager cardManager;

        public UIThemeSectionType SectionType { get; private set; }
        public RectTransform ItemsContent => itemsContent;
        public bool HasRequiredUI => sectionTitle != null && itemsContent != null && cardManager != null;

        public void Configure(UIThemeSectionType _sectionType, UIThemeData _theme)
        {
            SectionType = _sectionType;
            sectionTitle.text = _sectionType + " Section";
            cardManager.Configure(_theme, _sectionType, itemsContent);
        }

        public void RefreshCards()
        {
            if (cardManager != null)
                cardManager.RefreshCards();
        }
    }
}
