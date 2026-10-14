using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BingoGame.UI.User
{
    public class UserStatRowUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("Row Components")]
        [Tooltip("The themed Background child Image. The root Image receives clicks.")]
        [SerializeField] private Image backgroundImage;
        [Tooltip("Text displaying Overall or the configured game mode name.")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("Right-aligned points value.")]
        [SerializeField] private TMP_Text pointsText;
        [Tooltip("Overlay shown while this row is selected.")]
        [SerializeField] private GameObject highlightObject;

        private Action<UserStatRowUI> selected;

        public RectTransform RectTransform { get; private set; }

        private void Awake()
        {
            RectTransform = transform as RectTransform;
        }

        public void Setup(string _name, int _points, Action<UserStatRowUI> _selected)
        {
            selected = _selected;
            if (backgroundImage != null)
            {
                backgroundImage.raycastTarget = false;
            }

            if (nameText != null)
            {
                nameText.text = _name;
            }

            SetPoints(_points);
        }

        public void SetPoints(int _points)
        {
            if (pointsText != null)
            {
                pointsText.text = _points.ToString("N0");
            }
        }

        public void SetHighlighted(bool _highlighted)
        {
            if (highlightObject != null)
            {
                highlightObject.SetActive(_highlighted);
            }
        }

        private void OnDisable()
        {
            SetHighlighted(false);
        }

        public void OnPointerClick(PointerEventData _eventData)
        {
            if (_eventData.button == PointerEventData.InputButton.Left)
            {
                selected?.Invoke(this);
            }
        }
    }
}
