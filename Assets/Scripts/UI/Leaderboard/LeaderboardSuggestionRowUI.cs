using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BingoGame.UI.Leaderboard
{
    [DisallowMultipleComponent]
    public sealed class LeaderboardSuggestionRowUI : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [Header("Row")]
        [SerializeField, Tooltip("Overlay displayed for the selected suggestion.")] private GameObject highlightObject;

        [Space]
        [Header("Content")]
        [SerializeField, Tooltip("Player name label.")] private TMP_Text playerNameText;
        [SerializeField, Tooltip("Full player ID label.")] private TMP_Text userIdText;

        [Space]
        [Header("Empty Results")]
        [SerializeField, Tooltip("Message label shown instead of the player name and ID.")] private TMP_Text emptyResultsText;

        private int index;
        private Action<int> hoverRequested;
        private Action<int> selectionRequested;
        public RectTransform RectTransform { get; private set; }

        private void Awake()
        {
            RectTransform = GetComponent<RectTransform>();
        }

        public void Setup(string _name, string _id, int _index, Action<int> _hover, Action<int> _select)
        {
            index = _index;
            hoverRequested = _hover;
            selectionRequested = _select;
            SetEmptyState(false);
            if (playerNameText != null) playerNameText.text = _name;
            if (userIdText != null) userIdText.text = _id;
            SetHighlighted(false);
        }

        public void SetupEmpty(string _message)
        {
            index = -1;
            hoverRequested = null;
            selectionRequested = null;
            SetEmptyState(true);
            if (emptyResultsText != null) emptyResultsText.text = _message;
            SetHighlighted(false);
        }

        private void SetEmptyState(bool _empty)
        {
            if (playerNameText != null) playerNameText.gameObject.SetActive(!_empty);
            if (userIdText != null) userIdText.gameObject.SetActive(!_empty);
            if (emptyResultsText != null) emptyResultsText.gameObject.SetActive(_empty);
        }

        public void SetHighlighted(bool _highlighted)
        {
            if (highlightObject != null) highlightObject.SetActive(_highlighted && index >= 0);
        }

        public void OnPointerEnter(PointerEventData _eventData)
        {
            hoverRequested?.Invoke(index);
        }

        public void OnPointerClick(PointerEventData _eventData)
        {
            if (_eventData.button == PointerEventData.InputButton.Left) selectionRequested?.Invoke(index);
        }
    }
}
