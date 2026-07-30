using System;
using System.Collections.Generic;
using UnityEngine;

public enum MultiplayerNetworkPriority
{
    Critical,
    High,
    Normal,
    Low
}

public enum MultiplayerNetworkWorkType
{
    Event,
    State
}

[DefaultExecutionOrder(-1150)]
[DisallowMultipleComponent]
public class MultiplayerNetworkScheduler : MonoBehaviour
{
    #region Fields

    private const string GlobalSessionId = "global";

    public static MultiplayerNetworkScheduler instance;

    [Header("Traffic Budget")]
    [SerializeField, Min(1024)] private int bytesPerSecondBudget = 1048576;
    [SerializeField, Min(1024)] private int maximumBurstBytes = 32768;
    [SerializeField, Min(1)] private int maximumItemsPerFrame = 64;

    [Header("Backpressure")]
    [SerializeField, Min(1024)] private int softMaximumQueuedBytes = 4194304;

    private readonly Dictionary<MultiplayerNetworkPriority, Dictionary<string, Queue<NetworkWorkItem>>> queues =
        new Dictionary<MultiplayerNetworkPriority, Dictionary<string, Queue<NetworkWorkItem>>>();

    private readonly Dictionary<MultiplayerNetworkPriority, int> roundRobinCursorByPriority =
        new Dictionary<MultiplayerNetworkPriority, int>();

    private readonly Dictionary<string, NetworkWorkItem> coalescedStateByKey =
        new Dictionary<string, NetworkWorkItem>(StringComparer.Ordinal);

    private bool isReady;
    private float availableBytes;
    private long queuedBytes;

    private NetworkRoot networkRoot;

    public bool IsReady => isReady;
    public int QueuedItemCount => GetQueuedItemCount();
    public long QueuedBytes => queuedBytes;
    public double OldestQueuedAgeSeconds
    {
        get
        {
            double oldestQueuedTime = FindOldestQueuedTime();
            return oldestQueuedTime <= 0d ? 0d : Math.Max(0d, Time.unscaledTimeAsDouble - oldestQueuedTime);
        }
    }

    #endregion

    #region Unity Methods

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        networkRoot = GetComponentInParent<NetworkRoot>();

        if (networkRoot == null || !networkRoot.IsPrimaryInstance)
        {
            enabled = false;
            return;
        }

        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }

        instance = this;

        foreach (MultiplayerNetworkPriority priority in Enum.GetValues(typeof(MultiplayerNetworkPriority)))
        {
            queues[priority] = new Dictionary<string, Queue<NetworkWorkItem>>(StringComparer.Ordinal);
            roundRobinCursorByPriority[priority] = 0;
        }
    }

    private void Update()
    {
        if (!isReady)
        {
            return;
        }

        availableBytes = Mathf.Min(maximumBurstBytes, availableBytes + bytesPerSecondBudget * Time.unscaledDeltaTime);
        ProcessQueuedWork();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Initialization

    public bool Initialize()
    {
        if (isReady)
        {
            return true;
        }

        networkRoot = NetworkRoot.instance;

        if (networkRoot == null || networkRoot != GetComponentInParent<NetworkRoot>())
        {
            return false;
        }

        availableBytes = maximumBurstBytes;
        isReady = true;
        return true;
    }

    #endregion

    #region Queue

    public bool Enqueue(
        string sessionId,
        int estimatedBytes,
        MultiplayerNetworkPriority priority,
        MultiplayerNetworkWorkType workType,
        string coalesceKey,
        Func<bool> sendAction)
    {
        if (sendAction == null)
        {
            return false;
        }

        if (!isReady)
        {
            return sendAction();
        }

        sessionId = string.IsNullOrWhiteSpace(sessionId) ? GlobalSessionId : sessionId.Trim();
        estimatedBytes = Mathf.Max(1, estimatedBytes);

        if (workType == MultiplayerNetworkWorkType.State && !string.IsNullOrWhiteSpace(coalesceKey))
        {
            string resolvedCoalesceKey = BuildCoalesceKey(sessionId, coalesceKey);

            if (coalescedStateByKey.TryGetValue(resolvedCoalesceKey, out NetworkWorkItem existingItem) &&
                existingItem != null &&
                !existingItem.IsCancelled)
            {
                queuedBytes -= existingItem.EstimatedBytes;

                existingItem.EstimatedBytes = estimatedBytes;
                existingItem.SendAction = sendAction;
                existingItem.EnqueuedTime = Time.unscaledTimeAsDouble;

                queuedBytes += estimatedBytes;
                return true;
            }

            coalesceKey = resolvedCoalesceKey;
        }
        else
        {
            coalesceKey = string.Empty;
        }

        if (queuedBytes + estimatedBytes > softMaximumQueuedBytes)
        {
            TrimReplaceableState(estimatedBytes);

            if (queuedBytes + estimatedBytes > softMaximumQueuedBytes &&
                workType == MultiplayerNetworkWorkType.State &&
                (int)priority >= (int)MultiplayerNetworkPriority.Normal)
            {
                return false;
            }
        }

        NetworkWorkItem item = new NetworkWorkItem
        {
            SessionId = sessionId,
            EstimatedBytes = estimatedBytes,
            Priority = priority,
            WorkType = workType,
            CoalesceKey = coalesceKey,
            SendAction = sendAction,
            EnqueuedTime = Time.unscaledTimeAsDouble
        };

        Dictionary<string, Queue<NetworkWorkItem>> priorityQueues = queues[priority];

        if (!priorityQueues.TryGetValue(sessionId, out Queue<NetworkWorkItem> sessionQueue))
        {
            sessionQueue = new Queue<NetworkWorkItem>();
            priorityQueues[sessionId] = sessionQueue;
        }

        sessionQueue.Enqueue(item);
        queuedBytes += estimatedBytes;

        if (!string.IsNullOrWhiteSpace(coalesceKey))
        {
            coalescedStateByKey[coalesceKey] = item;
        }

        return true;
    }

    public void ClearAll()
    {
        foreach (MultiplayerNetworkPriority priority in Enum.GetValues(typeof(MultiplayerNetworkPriority)))
        {
            Dictionary<string, Queue<NetworkWorkItem>> priorityQueues = queues[priority];

            foreach (Queue<NetworkWorkItem> sessionQueue in priorityQueues.Values)
            {
                while (sessionQueue.Count > 0)
                {
                    CancelItem(sessionQueue.Dequeue());
                }
            }

            priorityQueues.Clear();
            roundRobinCursorByPriority[priority] = 0;
        }

        coalescedStateByKey.Clear();
        queuedBytes = 0L;
        availableBytes = maximumBurstBytes;
    }

    public void ClearSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        foreach (Dictionary<string, Queue<NetworkWorkItem>> priorityQueues in queues.Values)
        {
            if (!priorityQueues.TryGetValue(sessionId, out Queue<NetworkWorkItem> sessionQueue))
            {
                continue;
            }

            while (sessionQueue.Count > 0)
            {
                CancelItem(sessionQueue.Dequeue());
            }

            priorityQueues.Remove(sessionId);
        }

    }

    #endregion

    #region Processing

    private void ProcessQueuedWork()
    {
        int processedItems = 0;

        while (processedItems < maximumItemsPerFrame &&
               TryGetNextItem(availableBytes, out NetworkWorkItem item))
        {
            int budgetCost = GetBudgetCost(item);

            DequeueItem(item);
            availableBytes -= budgetCost;
            processedItems++;

            if (item.IsCancelled)
            {
                continue;
            }

            try
            {
                item.SendAction?.Invoke();
            }
            catch (Exception)
            {
            }
        }
    }

    private bool TryGetNextItem(float byteBudget, out NetworkWorkItem item)
    {
        item = null;

        foreach (MultiplayerNetworkPriority priority in Enum.GetValues(typeof(MultiplayerNetworkPriority)))
        {
            if (TryGetNextItem(priority, byteBudget, out item))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetNextItem(MultiplayerNetworkPriority priority, float byteBudget, out NetworkWorkItem item)
    {
        item = null;

        Dictionary<string, Queue<NetworkWorkItem>> priorityQueues = queues[priority];

        if (priorityQueues.Count == 0)
        {
            return false;
        }

        List<string> sessionIds = new List<string>(priorityQueues.Keys);

        if (sessionIds.Count == 0)
        {
            return false;
        }

        int startIndex = roundRobinCursorByPriority[priority] % sessionIds.Count;

        for (int offset = 0; offset < sessionIds.Count; offset++)
        {
            int index = (startIndex + offset) % sessionIds.Count;
            string sessionId = sessionIds[index];

            if (!priorityQueues.TryGetValue(sessionId, out Queue<NetworkWorkItem> sessionQueue))
            {
                continue;
            }

            while (sessionQueue.Count > 0 && sessionQueue.Peek().IsCancelled)
            {
                DequeueItem(sessionQueue.Peek());
            }

            if (sessionQueue.Count == 0)
            {
                priorityQueues.Remove(sessionId);
                continue;
            }

            NetworkWorkItem candidate = sessionQueue.Peek();

            if (byteBudget < GetBudgetCost(candidate))
            {
                continue;
            }

            item = candidate;
            roundRobinCursorByPriority[priority] = index + 1;
            return true;
        }

        return false;
    }

    private int GetBudgetCost(NetworkWorkItem item)
    {
        return item == null ? 0 : Mathf.Min(item.EstimatedBytes, maximumBurstBytes);
    }

    private void DequeueItem(NetworkWorkItem item)
    {
        if (item == null)
        {
            return;
        }

        Dictionary<string, Queue<NetworkWorkItem>> priorityQueues = queues[item.Priority];

        if (priorityQueues.TryGetValue(item.SessionId, out Queue<NetworkWorkItem> sessionQueue) &&
            sessionQueue.Count > 0 &&
            ReferenceEquals(sessionQueue.Peek(), item))
        {
            sessionQueue.Dequeue();

            if (sessionQueue.Count == 0)
            {
                priorityQueues.Remove(item.SessionId);
            }
        }

        if (!item.IsCancelled)
        {
            queuedBytes = Math.Max(0L, queuedBytes - item.EstimatedBytes);
        }

        if (!string.IsNullOrWhiteSpace(item.CoalesceKey) &&
            coalescedStateByKey.TryGetValue(item.CoalesceKey, out NetworkWorkItem currentItem) &&
            ReferenceEquals(currentItem, item))
        {
            coalescedStateByKey.Remove(item.CoalesceKey);
        }
    }

    #endregion

    #region Backpressure

    private void TrimReplaceableState(int incomingBytes)
    {
        MultiplayerNetworkPriority[] priorities =
        {
            MultiplayerNetworkPriority.Low,
            MultiplayerNetworkPriority.Normal,
            MultiplayerNetworkPriority.High
        };

        for (int p = 0; p < priorities.Length && queuedBytes + incomingBytes > softMaximumQueuedBytes; p++)
        {
            Dictionary<string, Queue<NetworkWorkItem>> priorityQueues = queues[priorities[p]];

            foreach (Queue<NetworkWorkItem> sessionQueue in priorityQueues.Values)
            {
                foreach (NetworkWorkItem item in sessionQueue)
                {
                    if (queuedBytes + incomingBytes <= softMaximumQueuedBytes)
                    {
                        break;
                    }

                    if (item == null ||
                        item.IsCancelled ||
                        item.WorkType != MultiplayerNetworkWorkType.State)
                    {
                        continue;
                    }

                    CancelItem(item);
                }
            }
        }
    }

    private void CancelItem(NetworkWorkItem item)
    {
        if (item == null || item.IsCancelled)
        {
            return;
        }

        item.IsCancelled = true;
        queuedBytes = Math.Max(0L, queuedBytes - item.EstimatedBytes);

        if (!string.IsNullOrWhiteSpace(item.CoalesceKey) &&
            coalescedStateByKey.TryGetValue(item.CoalesceKey, out NetworkWorkItem currentItem) &&
            ReferenceEquals(currentItem, item))
        {
            coalescedStateByKey.Remove(item.CoalesceKey);
        }
    }

    #endregion

    #region Helpers

    public static int EstimateUtf8Bytes(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 1;
        }

        return System.Text.Encoding.UTF8.GetByteCount(value);
    }

    private string BuildCoalesceKey(string sessionId, string coalesceKey)
    {
        return $"{sessionId}:{coalesceKey}";
    }

    private int GetQueuedItemCount()
    {
        int count = 0;

        foreach (Dictionary<string, Queue<NetworkWorkItem>> priorityQueues in queues.Values)
        {
            foreach (Queue<NetworkWorkItem> sessionQueue in priorityQueues.Values)
            {
                foreach (NetworkWorkItem item in sessionQueue)
                {
                    if (item != null && !item.IsCancelled)
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }

    private double FindOldestQueuedTime()
    {
        double oldestTime = 0d;

        foreach (Dictionary<string, Queue<NetworkWorkItem>> priorityQueues in queues.Values)
        {
            foreach (Queue<NetworkWorkItem> sessionQueue in priorityQueues.Values)
            {
                foreach (NetworkWorkItem item in sessionQueue)
                {
                    if (item == null || item.IsCancelled)
                    {
                        continue;
                    }

                    if (oldestTime <= 0d || item.EnqueuedTime < oldestTime)
                    {
                        oldestTime = item.EnqueuedTime;
                    }
                }
            }
        }

        return oldestTime;
    }

    private class NetworkWorkItem
    {
        public string SessionId;
        public int EstimatedBytes;
        public MultiplayerNetworkPriority Priority;
        public MultiplayerNetworkWorkType WorkType;
        public string CoalesceKey;
        public Func<bool> SendAction;
        public double EnqueuedTime;
        public bool IsCancelled;
    }

    #endregion
}
