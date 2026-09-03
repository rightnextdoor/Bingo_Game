using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GameBallAnimationController : MonoBehaviour
{
    private const int DisplaySlotCount = 4;

    private sealed class BallMove
    {
        public RectTransform RectTransform { get; }
        public Vector3 StartPosition { get; }
        public Vector3 EndPosition { get; }

        public BallMove(RectTransform rectTransform, Vector3 startPosition, Vector3 endPosition)
        {
            RectTransform = rectTransform;
            StartPosition = startPosition;
            EndPosition = endPosition;
        }
    }

    [Header("Prefab and Container")]
    [SerializeField] private GameBallItemController ballPrefab;
    [SerializeField] private RectTransform itemContainer;

    [Header("Animation Points")]
    [SerializeField] private RectTransform entryPoint;
    [SerializeField] private RectTransform exitPoint;
    [SerializeField] private List<RectTransform> displaySlots = new List<RectTransform>(DisplaySlotCount);

    [Header("Animation")]
    [SerializeField] private AnimationCurve movementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private readonly Queue<GameBallPresentationData> pendingBalls =
        new Queue<GameBallPresentationData>();
    private readonly List<GameBallItemController> activeItems =
        new List<GameBallItemController>(DisplaySlotCount);
    private readonly List<GameBallItemController> allItems =
        new List<GameBallItemController>();
    private readonly Stack<GameBallItemController> availableItems =
        new Stack<GameBallItemController>();

    private Coroutine animationRoutine;
    private bool hasLoggedMissingSetup;

    private void Awake()
    {
        RemoveStartingBallItems();
    }

    public void EnqueueBall(GameBallPresentationData presentationData)
    {
        if (!presentationData.IsValid)
        {
            return;
        }

        pendingBalls.Enqueue(presentationData);

        if (animationRoutine == null && isActiveAndEnabled)
        {
            animationRoutine = StartCoroutine(ProcessAnimationQueue());
        }
    }

    public void ShowHistoryImmediately(IReadOnlyList<GameBallPresentationData> history)
    {
        ClearDisplay();

        if (history == null || !HasRequiredSetup())
        {
            return;
        }

        int firstHistoryIndex = Mathf.Max(0, history.Count - DisplaySlotCount);

        for (int historyIndex = firstHistoryIndex;
             historyIndex < history.Count && activeItems.Count < DisplaySlotCount;
             historyIndex++)
        {
            GameBallPresentationData presentationData = history[historyIndex];

            if (!presentationData.IsValid)
            {
                continue;
            }

            GameBallItemController item = AcquireItem();

            if (item == null)
            {
                return;
            }

            item.Apply(presentationData);
            item.RectTransform.position = displaySlots[activeItems.Count].position;
            activeItems.Add(item);
        }
    }

    public void ClearDisplay()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        pendingBalls.Clear();
        activeItems.Clear();
        availableItems.Clear();

        for (int i = 0; i < allItems.Count; i++)
        {
            GameBallItemController item = allItems[i];

            if (item == null)
            {
                continue;
            }

            item.Clear();
            item.gameObject.SetActive(false);
            availableItems.Push(item);
        }
    }

    private IEnumerator ProcessAnimationQueue()
    {
        while (pendingBalls.Count > 0)
        {
            GameBallPresentationData presentationData = pendingBalls.Dequeue();
            yield return AnimateBall(presentationData);
        }

        animationRoutine = null;
    }

    private IEnumerator AnimateBall(GameBallPresentationData presentationData)
    {
        if (!HasRequiredSetup())
        {
            yield break;
        }

        GameBallItemController newItem = AcquireItem();

        if (newItem == null || newItem.RectTransform == null)
        {
            yield break;
        }

        newItem.Apply(presentationData);
        newItem.RectTransform.position = entryPoint.position;

        List<BallMove> moves = new List<BallMove>(DisplaySlotCount + 1);
        GameBallItemController exitingItem = null;

        if (activeItems.Count < DisplaySlotCount)
        {
            int targetSlotIndex = activeItems.Count;
            moves.Add(new BallMove(
                newItem.RectTransform,
                newItem.RectTransform.position,
                displaySlots[targetSlotIndex].position));
        }
        else
        {
            exitingItem = activeItems[0];

            moves.Add(new BallMove(
                exitingItem.RectTransform,
                exitingItem.RectTransform.position,
                exitPoint.position));

            for (int itemIndex = 1; itemIndex < activeItems.Count; itemIndex++)
            {
                RectTransform itemRectTransform = activeItems[itemIndex].RectTransform;

                moves.Add(new BallMove(
                    itemRectTransform,
                    itemRectTransform.position,
                    displaySlots[itemIndex - 1].position));
            }

            moves.Add(new BallMove(
                newItem.RectTransform,
                newItem.RectTransform.position,
                displaySlots[DisplaySlotCount - 1].position));
        }

        yield return AnimateMoves(moves, ResolveSlideDuration());

        if (exitingItem != null)
        {
            activeItems.RemoveAt(0);
            ReleaseItem(exitingItem);
        }

        activeItems.Add(newItem);
        SnapActiveItemsToSlots();
    }

    private IEnumerator AnimateMoves(IReadOnlyList<BallMove> moves, float duration)
    {
        if (duration <= 0f)
        {
            ApplyMovePositions(moves, 1f);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float curvedTime = movementCurve != null
                ? movementCurve.Evaluate(normalizedTime)
                : normalizedTime;

            ApplyMovePositions(moves, curvedTime);
            yield return null;
        }

        ApplyMovePositions(moves, 1f);
    }

    private static void ApplyMovePositions(IReadOnlyList<BallMove> moves, float progress)
    {
        for (int moveIndex = 0; moveIndex < moves.Count; moveIndex++)
        {
            BallMove move = moves[moveIndex];

            if (move?.RectTransform == null)
            {
                continue;
            }

            move.RectTransform.position = Vector3.LerpUnclamped(
                move.StartPosition,
                move.EndPosition,
                progress);
        }
    }

    private GameBallItemController AcquireItem()
    {
        GameBallItemController item = null;

        while (availableItems.Count > 0 && item == null)
        {
            item = availableItems.Pop();
        }

        if (item == null && ballPrefab != null)
        {
            RectTransform resolvedContainer = itemContainer != null
                ? itemContainer
                : transform as RectTransform;

            item = Instantiate(ballPrefab, resolvedContainer, false);
            allItems.Add(item);
        }

        if (item != null)
        {
            item.gameObject.SetActive(true);
        }

        return item;
    }

    private void ReleaseItem(GameBallItemController item)
    {
        if (item == null)
        {
            return;
        }

        item.Clear();
        item.gameObject.SetActive(false);
        availableItems.Push(item);
    }

    private void SnapActiveItemsToSlots()
    {
        int visibleCount = Mathf.Min(activeItems.Count, DisplaySlotCount);

        for (int itemIndex = 0; itemIndex < visibleCount; itemIndex++)
        {
            GameBallItemController item = activeItems[itemIndex];

            if (item?.RectTransform != null)
            {
                item.RectTransform.position = displaySlots[itemIndex].position;
            }
        }
    }

    private bool HasRequiredSetup()
    {
        bool hasSetup =
            ballPrefab != null &&
            entryPoint != null &&
            exitPoint != null &&
            displaySlots != null &&
            displaySlots.Count >= DisplaySlotCount;

        if (hasSetup)
        {
            for (int slotIndex = 0; slotIndex < DisplaySlotCount; slotIndex++)
            {
                if (displaySlots[slotIndex] == null)
                {
                    hasSetup = false;
                    break;
                }
            }
        }

        if (!hasSetup && !hasLoggedMissingSetup)
        {
            hasLoggedMissingSetup = true;
            Debug.LogWarning(
                "[GameBallAnimationController] Assign the ball prefab, entry point, exit point, and four display slots.",
                this);
        }

        return hasSetup;
    }

    private static float ResolveSlideDuration()
    {
        return GameSettings.instance != null
            ? GameSettings.instance.BallSlideDurationSeconds
            : GameSettings.DefaultBallSlideDurationSeconds;
    }

    private void RemoveStartingBallItems()
    {
        Transform searchRoot = itemContainer != null
            ? itemContainer
            : transform;

        GameBallItemController[] startingItems =
            searchRoot.GetComponentsInChildren<GameBallItemController>(true);

        for (int itemIndex = 0; itemIndex < startingItems.Length; itemIndex++)
        {
            GameBallItemController startingItem = startingItems[itemIndex];

            if (startingItem == null)
            {
                continue;
            }

            startingItem.gameObject.SetActive(false);
            Destroy(startingItem.gameObject);
        }
    }
}
