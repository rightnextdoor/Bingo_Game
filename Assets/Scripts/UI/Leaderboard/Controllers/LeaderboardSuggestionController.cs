using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BingoGame.UI.Leaderboard
{
    [DisallowMultipleComponent]
    public sealed class LeaderboardSuggestionController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField, Tooltip("Child panel to show and hide; keep this controller's object active.")] private GameObject suggestionPanel;
        [SerializeField, Tooltip("Suggestion list scroll view.")] private ScrollRect scrollRect;
        [SerializeField, Tooltip("Parent of generated suggestion rows.")] private RectTransform content;

        [Space]
        [Header("Panel Size")]
        [SerializeField, Min(1f), Tooltip("Maximum panel height. Shorter result lists shrink to their content.")] private float maximumPanelHeight = 362f;

        [Space]
        [Header("Row")]
        [SerializeField, Tooltip("Suggestion row prefab.")] private LeaderboardSuggestionRowUI rowPrefab;

        private readonly List<UserData> users = new();
        private readonly List<LeaderboardSuggestionRowUI> rows = new();
        private readonly List<RaycastResult> pointerHits = new();
        private TMP_InputField input;
        private RectTransform viewport;
        private RectTransform panelRect;
        private PointerEventData pointerEvent;
        private EventSystem pointerEventSystem;
        private int selectedIndex = -1;
        public event Action<string> KeyboardNameSelected;
        public event Action<string> UserSelected;
        public event Action InputClicked;
        public bool IsOpen => suggestionPanel != null && suggestionPanel.activeSelf;

        private void Awake()
        {
            if (suggestionPanel != null) panelRect = suggestionPanel.GetComponent<RectTransform>();
            if (scrollRect != null) viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
            if (content != null)
            {
                for (int i = content.childCount - 1; i >= 0; i--)
                {
                    GameObject child = content.GetChild(i).gameObject;
                    child.SetActive(false);
                    Destroy(child);
                }
            }
            Close();
        }

        public void BindInput(TMP_InputField _input) => input = _input;

        private void OnDisable() => Close();

        private void Update()
        {
            HandlePointer();
            if (!IsOpen || input == null || !input.isFocused || Keyboard.current == null) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
            int direction = Keyboard.current.upArrowKey.wasPressedThisFrame ? -1 : Keyboard.current.downArrowKey.wasPressedThisFrame ? 1 : 0;
            if (direction == 0 || users.Count == 0) return;
            Select(Mathf.Clamp(selectedIndex + direction, 0, users.Count - 1));
            KeyboardNameSelected?.Invoke(GetPlayerName(users[selectedIndex]));
            ScrollSelectionIntoView();
        }

        private void HandlePointer()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame || EventSystem.current == null) return;
            if (pointerEventSystem != EventSystem.current)
            {
                pointerEventSystem = EventSystem.current;
                pointerEvent = new PointerEventData(pointerEventSystem);
            }
            pointerEvent.Reset();
            pointerEvent.position = Mouse.current.position.ReadValue();
            pointerHits.Clear();
            pointerEventSystem.RaycastAll(pointerEvent, pointerHits);
            Transform hit = pointerHits.Count > 0 ? pointerHits[0].gameObject.transform : null;
            if (hit != null && input != null && hit.IsChildOf(input.transform)) { InputClicked?.Invoke(); return; }
            if (IsOpen && (hit == null || !hit.IsChildOf(suggestionPanel.transform))) Close();
        }

        public void Show(IReadOnlyList<UserData> _matches)
        {
            ClearRows();
            if (suggestionPanel == null || content == null || rowPrefab == null) return;
            suggestionPanel.SetActive(true);
            for (int i = 0; i < _matches.Count; i++)
            {
                UserData user = _matches[i];
                if (user == null) continue;
                int index = users.Count;
                users.Add(user);
                LeaderboardSuggestionRowUI row = Instantiate(rowPrefab, content);
                row.gameObject.SetActive(true);
                row.Setup(GetPlayerName(user), user.userId, index, Select, Accept);
                rows.Add(row);
            }
            if (users.Count == 0)
            {
                LeaderboardSuggestionRowUI row = Instantiate(rowPrefab, content);
                row.gameObject.SetActive(true);
                row.SetupEmpty("No matching users found.");
                rows.Add(row);
            }
            Select(users.Count > 0 ? 0 : -1);
            ResizePanel();
            if (scrollRect != null) { scrollRect.StopMovement(); scrollRect.verticalNormalizedPosition = 1f; }
        }

        private void ResizePanel()
        {
            if (panelRect == null || viewport == null) return;
            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, maximumPanelHeight);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float verticalInsets = Mathf.Max(0f, panelRect.rect.height - viewport.rect.height);
            float height = Mathf.Min(maximumPanelHeight, content.rect.height + verticalInsets);
            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1f, height));
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            Canvas.ForceUpdateCanvases();
        }

        public void Close()
        {
            if (suggestionPanel != null) suggestionPanel.SetActive(false);
            ClearRows();
        }

        private void ClearRows()
        {
            foreach (LeaderboardSuggestionRowUI row in rows)
            {
                if (row == null) continue;
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            rows.Clear();
            users.Clear();
            selectedIndex = -1;
        }

        private void Select(int _index)
        {
            selectedIndex = _index;
            for (int i = 0; i < rows.Count; i++) rows[i].SetHighlighted(i == selectedIndex);
        }

        private static string GetPlayerName(UserData _user)
        {
            return string.IsNullOrWhiteSpace(_user.playerName) ? "Player" : _user.playerName;
        }

        private void Accept(int _index)
        {
            if (_index < 0 || _index >= users.Count) return;
            string id = users[_index].userId;
            Close();
            UserSelected?.Invoke(id);
        }

        private void ScrollSelectionIntoView()
        {
            if (scrollRect == null || viewport == null || content == null || selectedIndex < 0) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float maxY = Mathf.Max(0f, content.rect.height - viewport.rect.height);
            RectTransform row = rows[selectedIndex].RectTransform;
            float top = -row.anchoredPosition.y - (1f - row.pivot.y) * row.rect.height;
            float bottom = top + row.rect.height;
            float y = content.anchoredPosition.y;
            if (top < y) y = top;
            else if (bottom > y + viewport.rect.height) y = bottom - viewport.rect.height;
            if (selectedIndex == 0) y = 0f;
            else if (selectedIndex == rows.Count - 1) y = maxY;
            scrollRect.StopMovement();
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(y, 0f, maxY));
        }
    }
}
