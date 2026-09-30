using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace BingoGame.UI.ThemeTest
{
    [DisallowMultipleComponent]
    public sealed class ThemeTestBoardHighlightPreview : MonoBehaviour
    {
        [Header("Cells")]
        [Tooltip("CellGrid containing exactly 25 UIThemeBoardCellTarget components in row-major hierarchy order.")]
        [SerializeField] private Transform cellRoot;

        [Space]
        [Header("Patterns")]
        [Tooltip("Pattern assets to preview in checker order. Duplicate pattern types and empty entries are skipped.")]
        [SerializeField] private List<BingoPatternData> patterns = new();

        [Space]
        [Header("Playback")]
        [Tooltip("Unscaled seconds spent checking each cell.")]
        [SerializeField, Min(0.01f)] private float cellCheckSeconds = 0.3f;
        [Tooltip("Unscaled seconds for each win and fail display.")]
        [SerializeField, Min(0.01f)] private float resultSeconds = 2f;
        [Tooltip("Failure tint applied to the existing winning/check highlight Image.")]
        [SerializeField] private Color failureColor = Color.red;

        private enum PlaybackPhase { Checking, Win, Fail }

        private static readonly BingoPatternType[] PatternOrder =
        {
            BingoPatternType.SingleLine,
            BingoPatternType.TwoLines,
            BingoPatternType.FourCorners,
            BingoPatternType.Cross,
            BingoPatternType.XPattern,
            BingoPatternType.Star,
            BingoPatternType.Diamond,
            BingoPatternType.Blackout
        };

        private readonly Dictionary<BingoPatternType, IReadOnlyList<int>> layouts = new();
        private readonly List<BingoPatternData> sequence = new();
        private UIThemeBoardCellTarget[] cells;
        private UnityAction[] clickHandlers;
        private bool[] marked;
        private bool[] originalInteractable;
        private IReadOnlyList<int> currentCells;
        private PlaybackPhase phase;
        private int patternIndex;
        private int checkIndex;
        private double nextStep;
        private bool initialized;

        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            if (cellRoot == null)
            {
                Debug.LogWarning("Assign the preview board's CellGrid to the highlight player's Cell Root.", this);
                return;
            }

            cells = cellRoot.GetComponentsInChildren<UIThemeBoardCellTarget>(true);
            if (cells.Length != 25)
            {
                Debug.LogWarning("Board highlight preview requires exactly 25 cells in row-major hierarchy order.", this);
                return;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].CellButton == null || cells[i].MarkedHighlightImage == null || cells[i].WinningHighlightImage == null)
                {
                    Debug.LogWarning("Each preview cell needs its Button, Marked Highlight Image, and Winning Highlight Image assigned.", this);
                    return;
                }
            }

            marked = new bool[cells.Length];
            originalInteractable = new bool[cells.Length];
            clickHandlers = new UnityAction[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                int cellIndex = i;
                clickHandlers[i] = () => ToggleMark(cellIndex);
                cells[i].CellButton.onClick.AddListener(clickHandlers[i]);
                originalInteractable[i] = cells[i].CellButton.interactable;
                if (cells[i].ValueText != null)
                    cells[i].ValueText.text = i == 12 ? "FREE" : (i + 1).ToString();
            }

            BingoPatternValidator validator = new();
            foreach (BingoPatternType type in Enum.GetValues(typeof(BingoPatternType)))
                layouts[type] = validator.GetSimulationPatternCells(type);

            initialized = true;
            RefreshVisuals();
        }

        public void SetPlaying(bool _play)
        {
            if (!initialized)
                return;
            if (_play && IsPlaying)
                return;

            if (!_play)
            {
                IsPlaying = false;
                Array.Clear(marked, 0, marked.Length);
                SetCellInteraction(false);
                RefreshVisuals();
                return;
            }

            BuildSequence();
            if (sequence.Count == 0)
            {
                Debug.LogWarning("Assign at least one supported BingoPatternData asset to preview highlights.", this);
                return;
            }

            for (int i = 0; i < cells.Length; i++)
                originalInteractable[i] = cells[i].CellButton.interactable;
            IsPlaying = true;
            SetCellInteraction(true);
            patternIndex = 0;
            BeginPattern();
        }

        private void BuildSequence()
        {
            sequence.Clear();
            for (int orderIndex = 0; orderIndex < PatternOrder.Length; orderIndex++)
            {
                BingoPatternType type = PatternOrder[orderIndex];
                if (!layouts.TryGetValue(type, out IReadOnlyList<int> indices) || indices.Count == 0)
                    continue;

                for (int i = 0; i < patterns.Count; i++)
                {
                    BingoPatternData pattern = patterns[i];
                    if (pattern == null || pattern.PatternType != type)
                        continue;
                    sequence.Add(pattern);
                    break;
                }
            }
        }

        private void BeginPattern()
        {
            Array.Clear(marked, 0, marked.Length);
            BingoPatternData pattern = sequence[patternIndex];
            if (pattern == null || !layouts.TryGetValue(pattern.PatternType, out currentCells) || currentCells.Count == 0)
            {
                SetPlaying(false);
                return;
            }

            for (int i = 0; i < currentCells.Count; i++)
                marked[currentCells[i]] = true;
            checkIndex = 0;
            phase = PlaybackPhase.Checking;
            nextStep = Time.unscaledTimeAsDouble + Mathf.Max(0.01f, cellCheckSeconds);
            RefreshVisuals();
        }

        private void Update()
        {
            if (!IsPlaying || Time.unscaledTimeAsDouble < nextStep)
                return;

            switch (phase)
            {
                case PlaybackPhase.Checking:
                    checkIndex++;
                    if (checkIndex >= currentCells.Count)
                    {
                        phase = PlaybackPhase.Win;
                        nextStep = Time.unscaledTimeAsDouble + Mathf.Max(0.01f, resultSeconds);
                    }
                    else
                        nextStep = Time.unscaledTimeAsDouble + Mathf.Max(0.01f, cellCheckSeconds);
                    break;
                case PlaybackPhase.Win:
                    phase = PlaybackPhase.Fail;
                    nextStep = Time.unscaledTimeAsDouble + Mathf.Max(0.01f, resultSeconds);
                    break;
                case PlaybackPhase.Fail:
                    patternIndex++;
                    if (patternIndex >= sequence.Count)
                    {
                        BuildSequence();
                        patternIndex = 0;
                    }
                    if (sequence.Count == 0)
                        SetPlaying(false);
                    else
                        BeginPattern();
                    return;
            }

            RefreshVisuals();
        }

        private void ToggleMark(int _cellIndex)
        {
            if (IsPlaying)
                return;
            marked[_cellIndex] = !marked[_cellIndex];
            RefreshVisuals();
        }

        public void RefreshVisuals()
        {
            if (!initialized)
                return;

            Color color = failureColor;
            if (IsPlaying && phase != PlaybackPhase.Fail && sequence[patternIndex] != null)
                color = sequence[patternIndex].WinningHighlightColor;

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null)
                    continue;
                bool highlighted = IsPlaying && (phase == PlaybackPhase.Checking
                    ? i == currentCells[checkIndex] : marked[i]);
                cells[i].MarkedHighlightImage.gameObject.SetActive(marked[i] && !highlighted);
                cells[i].WinningHighlightImage.gameObject.SetActive(highlighted);
                if (highlighted)
                    cells[i].WinningHighlightImage.color = color;
            }
        }

        private void SetCellInteraction(bool _locked)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != null && cells[i].CellButton != null)
                    cells[i].CellButton.interactable = !_locked && originalInteractable[i];
            }
        }

        private void OnDisable()
        {
            SetPlaying(false);
        }

        private void OnDestroy()
        {
            if (!initialized)
                return;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != null && cells[i].CellButton != null)
                    cells[i].CellButton.onClick.RemoveListener(clickHandlers[i]);
            }
        }
    }
}
