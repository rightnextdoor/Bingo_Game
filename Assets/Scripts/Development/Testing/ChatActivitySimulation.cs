using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class ChatActivitySimulation : MonoBehaviour
{
    #region Fields

    private const float PrivateMessageChance = 0.12f;
    private const float LeaveAfterMessagesChance = 0.35f;
    private const float MinimumLeaveDelaySeconds = 0.25f;
    private const float MaximumLeaveDelaySeconds = 2.5f;
    private const float MessageSettleSeconds = 0.25f;

    [Header("Target Lobby")]
    [SerializeField] private MultiplayerStressTargetPlayer targetPlayer = MultiplayerStressTargetPlayer.Player1;

    [Header("Fake Player Join")]
    [SerializeField] private bool useMaxLobbySize;
    [SerializeField, Min(1)] private int playersToAdd = 50;
    [SerializeField, Min(1)] private int minimumJoinBatch = 1;
    [SerializeField, Min(1)] private int maximumJoinBatch = 8;
    [SerializeField, Min(0f)] private float minimumJoinDelaySeconds = 0.2f;
    [SerializeField, Min(0f)] private float maximumJoinDelaySeconds = 1.5f;
    [SerializeField, Min(0f)] private float minimumLoadDelaySeconds = 0.5f;
    [SerializeField, Min(0f)] private float maximumLoadDelaySeconds = 4f;

    [Header("Chat Activity")]
    [SerializeField, Min(1)] private int minimumMessagesPerPlayer = 3;
    [SerializeField, Min(1)] private int maximumMessagesPerPlayer = 12;
    [SerializeField, Min(0f)] private float minimumMessageDelaySeconds = 0.15f;
    [SerializeField, Min(0f)] private float maximumMessageDelaySeconds = 2f;
    [SerializeField, Min(0)] private int blockedPlayers = 5;

    [Header("Synthetic Message Queue")]
    [SerializeField, Min(0.1f)] private float maximumMessagesPerSecond = 8f;
    [SerializeField, Min(1f)] private float maximumBytesPerSecond = 750f;

    [Header("Run Control")]
    [SerializeField, Min(5f)] private float maximumRunSeconds = 180f;
    [SerializeField] private bool runSimulation;
    [SerializeField] private bool stopSimulation;

    private readonly HashSet<string> simulatedUserIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> startedActivityUserIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> selectedBlockedUserIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> testAddedBlockedUserIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<int> blockedReadyOrdinals = new HashSet<int>();
    private readonly Dictionary<ChatSimulationFilterType, int> filterCounts = new Dictionary<ChatSimulationFilterType, int>();
    private readonly List<Task> playerActivityTasks = new List<Task>();
    private readonly Queue<QueuedSyntheticMessage> outboundMessages = new Queue<QueuedSyntheticMessage>();

    private ChatManager chatManager;

    private int stressRunId;
    private int joinOperationId;
    private int runToken;
    private bool isRunning;
    private bool localStopRequested;
    private double runStartedTime;
    private double nextSyntheticDispatchTime;
    private string activeLobbyId = string.Empty;
    private string runtimeCancelReason = string.Empty;
    private string localRecipientUserId = string.Empty;

    private int requestedPlayers;
    private int sceneReadyPlayers;
    private int totalMessagesRequested;
    private int totalMessagesSent;
    private int failedMessages;
    private int publicMessages;
    private int privateMessages;
    private int deliveredMessages;
    private int deliveredPublicMessages;
    private int deliveredPrivateMessages;
    private int normalMessages;
    private int filterTestMessages;
    private int blockedMessagesAttempted;
    private int blockedMessagesVisible;
    private int playersLeft;
    private int failedLeaves;
    private int nextReadyOrdinal;
    private int playersStartedBeforeJoinCompleted;
    private int peakQueuedMessages;
    private double totalQueueWaitSeconds;
    private double longestQueueWaitSeconds;

    #endregion

    #region Unity Methods

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ProcessStopTrigger();
        ProcessRunTrigger();
        TryApplyPendingBlocks();
        ProcessSyntheticMessageQueue();
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (isRunning)
        {
            CancelImmediately("The chat activity simulation was disabled before completion.");
        }
#endif
    }

    #endregion

    #region Run Control

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void ProcessRunTrigger()
    {
        if (!runSimulation || isRunning)
        {
            return;
        }

        runSimulation = false;
        _ = RunSimulationAsync(++runToken);
    }

    private void ProcessStopTrigger()
    {
        if (!stopSimulation)
        {
            return;
        }

        stopSimulation = false;

        if (!isRunning)
        {
            return;
        }

        RequestRuntimeCancel("User stopped the chat activity simulation.");

        if (joinOperationId > 0)
        {
            StressFakePlayerManager.instance?.CancelJoinWave(joinOperationId, "User stopped the chat activity simulation.");
        }
    }

    private void CancelImmediately(string reason)
    {
        runtimeCancelReason = string.IsNullOrWhiteSpace(reason) ? "The chat activity simulation was cancelled." : reason.Trim();
        localStopRequested = true;
        runToken++;

        if (joinOperationId > 0)
        {
            StressFakePlayerManager.instance?.CancelJoinWave(joinOperationId, reason);
        }

        UnsubscribeFromMessages();
        string summary = BuildSummary();
        CleanupTestBlocks();

        if (stressRunId > 0)
        {
            StressSimulationCoordinator.instance?.CancelRun(stressRunId, summary, reason);
        }

        ResetRuntimeState();
    }

#endif

    #endregion

    #region Simulation

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private async Task RunSimulationAsync(int token)
    {
        isRunning = true;
        localStopRequested = false;
        ResetCounters();

        if (!TryPrepareSimulation(out Lobby lobby, out string failureReason))
        {
            FinishNotStarted(failureReason);
            return;
        }

        requestedPlayers = GetRequestedJoinPlayerCount(lobby);

        if (requestedPlayers <= 0)
        {
            FinishNotStarted("The target Lobby does not have capacity for any additional synthetic players.");
            return;
        }

        string setupSummary = BuildSetupSummary(lobby, requestedPlayers);

        if (!StressSimulationCoordinator.instance.TryBeginRun("Chat Activity Simulation", setupSummary, out stressRunId, out _))
        {
            ResetRuntimeState();
            return;
        }

        runStartedTime = Time.unscaledTimeAsDouble;
        nextSyntheticDispatchTime = runStartedTime;
        activeLobbyId = lobby.GetLobbyId();
        localRecipientUserId = UserManager.instance != null && UserManager.instance.HasUser ? UserManager.instance.UserId : string.Empty;
        SubscribeToMessages();

        try
        {
            if (!StartFakePlayerJoin(lobby, requestedPlayers, out failureReason))
            {
                FinishRun(StressTestResult.Failed, failureReason);
                return;
            }

            PrepareBlockSelection(requestedPlayers);
            StressFakePlayerJoinResult joinResult = await RunJoinAndPlayerActivityAsync(token);

            if (!IsCurrentRun(token))
            {
                return;
            }

            if (ShouldCancel())
            {
                FinishRun(StressTestResult.Cancelled, GetStopReason());
                return;
            }

            if (joinResult == null || !joinResult.completed)
            {
                FinishRun(StressTestResult.Failed, "The chat activity simulation exceeded its maximum run time before the join/activity work completed.");
                return;
            }

            if (joinResult.outcome == StressFakePlayerJoinOutcome.Cancelled)
            {
                FinishRun(StressTestResult.Cancelled, joinResult.failureReason);
                return;
            }

            if (joinResult.outcome == StressFakePlayerJoinOutcome.Failed)
            {
                FinishRun(StressTestResult.Failed, joinResult.failureReason);
                return;
            }

            if (joinResult.outcome == StressFakePlayerJoinOutcome.LobbyStarted)
            {
                FinishRun(StressTestResult.Failed, "The Lobby started before the requested synthetic chat players finished joining.");
                return;
            }

            if (sceneReadyPlayers == 0)
            {
                FinishRun(StressTestResult.Failed, "No synthetic players finished Lobby loading.");
                return;
            }

            await WaitForSecondsAsync(MessageSettleSeconds, token);

            if (!IsCurrentRun(token))
            {
                return;
            }

            bool success = failedMessages == 0 && failedLeaves == 0 && blockedMessagesVisible == 0;
            string reason = success ? string.Empty : BuildFailureReason();
            FinishRun(success ? StressTestResult.Passed : StressTestResult.Failed, reason);
        }
        catch (Exception exception)
        {
            if (IsCurrentRun(token))
            {
                Debug.LogException(exception);
                FinishRun(StressTestResult.Failed, "An unexpected exception occurred during the chat activity simulation.");
            }
        }
    }

    private async Task RunPlayerActivityAsync(StressFakePlayerRecord record, int token)
    {
        if (record == null)
        {
            return;
        }

        int minimumMessages = Mathf.Max(1, Mathf.Min(minimumMessagesPerPlayer, maximumMessagesPerPlayer));
        int maximumMessages = Mathf.Max(minimumMessages, Mathf.Max(minimumMessagesPerPlayer, maximumMessagesPerPlayer));
        int messageCount = UnityEngine.Random.Range(minimumMessages, maximumMessages + 1);
        totalMessagesRequested += messageCount;

        await WaitForSecondsAsync(GetRandomMessageDelay(), token);

        for (int i = 0; i < messageCount; i++)
        {
            if (!IsCurrentRun(token) || ShouldCancel() || HasTimedOut())
            {
                return;
            }

            if (!ChatSimulationMessagePool.TryGetRandomMessage(out ChatSimulationMessageEntry entry))
            {
                failedMessages++;
                continue;
            }

            RecordMessageSelection(entry);

            ChatParticipantData sender = new ChatParticipantData(record.userId, record.playerName, string.Empty);
            bool isPrivate = UnityEngine.Random.value < PrivateMessageChance;
            bool senderBlocked = selectedBlockedUserIds.Contains(record.userId);

            if (senderBlocked)
            {
                blockedMessagesAttempted++;
            }

            if (isPrivate)
            {
                privateMessages++;
            }
            else
            {
                publicMessages++;
            }

            QueueSyntheticMessage(sender, entry.message, isPrivate, isPrivate ? localRecipientUserId : string.Empty);

            if (i < messageCount - 1)
            {
                await WaitForSecondsAsync(GetRandomMessageDelay(), token);
            }
        }

        if (!IsCurrentRun(token) || ShouldCancel() || HasTimedOut() || UnityEngine.Random.value >= LeaveAfterMessagesChance)
        {
            return;
        }

        await WaitForSecondsAsync(UnityEngine.Random.Range(MinimumLeaveDelaySeconds, MaximumLeaveDelaySeconds), token);

        if (!IsCurrentRun(token) || ShouldCancel() || HasTimedOut())
        {
            return;
        }

        if (StressFakePlayerManager.instance != null && StressFakePlayerManager.instance.TryRemovePlayer(record.userId, out _))
        {
            playersLeft++;
        }
        else
        {
            failedLeaves++;
        }
    }

    private void QueueSyntheticMessage(ChatParticipantData sender, string message, bool isPrivate, string recipientUserId)
    {
        if (sender == null || !sender.IsValid || string.IsNullOrWhiteSpace(message))
        {
            failedMessages++;
            return;
        }

        outboundMessages.Enqueue(new QueuedSyntheticMessage
        {
            sender = sender,
            message = message.Trim(),
            isPrivate = isPrivate,
            recipientUserId = recipientUserId?.Trim() ?? string.Empty,
            queuedTime = Time.unscaledTimeAsDouble
        });

        peakQueuedMessages = Mathf.Max(peakQueuedMessages, outboundMessages.Count);
    }

    private void ProcessSyntheticMessageQueue()
    {
        if (!isRunning || outboundMessages.Count == 0)
        {
            return;
        }

        double now = Time.unscaledTimeAsDouble;

        if (now < nextSyntheticDispatchTime)
        {
            return;
        }

        QueuedSyntheticMessage queuedMessage = outboundMessages.Dequeue();
        double queueWait = Math.Max(0d, now - queuedMessage.queuedTime);
        totalQueueWaitSeconds += queueWait;
        longestQueueWaitSeconds = Math.Max(longestQueueWaitSeconds, queueWait);

        if (NetworkLobbyManager.instance != null &&
            NetworkLobbyManager.instance.TryBroadcastStressChatMessage(
                activeLobbyId,
                queuedMessage.sender,
                queuedMessage.message,
                queuedMessage.isPrivate,
                queuedMessage.recipientUserId,
                out _,
                out _))
        {
            totalMessagesSent++;
        }
        else
        {
            failedMessages++;
        }

        int messageBytes = Encoding.UTF8.GetByteCount(queuedMessage.message);
        double messageSpacing = 1d / Math.Max(0.1d, maximumMessagesPerSecond);
        double byteSpacing = messageBytes / Math.Max(1d, maximumBytesPerSecond);
        nextSyntheticDispatchTime = now + Math.Max(messageSpacing, byteSpacing);
    }

#endif

    #endregion

    #region Setup

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private bool TryPrepareSimulation(out Lobby lobby, out string failureReason)
    {
        lobby = null;
        failureReason = string.Empty;

        if (NetworkBootstrap.instance == null || !NetworkBootstrap.instance.IsAuthority)
        {
            failureReason = "Run the chat simulation from the authority/host player.";
            return false;
        }

        if (MultiplayerPlayModeTestContext.IsActive && targetPlayer != MultiplayerStressTargetPlayer.Player1)
        {
            failureReason = "Chat simulations currently verify Player 1 because the authority process owns Player 1's local ChatManager.";
            return false;
        }

        if (StressSimulationCoordinator.instance == null || StressHealthReporter.instance == null ||
            StressFakePlayerManager.instance == null || NetworkLobbyManager.instance == null || !NetworkLobbyManager.instance.IsReady)
        {
            failureReason = "The shared stress simulation services are not ready.";
            return false;
        }

        if (ChatSimulationMessagePool.Count == 0)
        {
            failureReason = "ChatSimulationMessagePool does not contain any valid messages.";
            return false;
        }

        if (!TryResolveTargetLobby(out lobby, out failureReason))
        {
            return false;
        }

        chatManager = ChatManager.instance;
        return true;
    }

    private bool TryResolveTargetLobby(out Lobby lobby, out string failureReason)
    {
        lobby = null;
        failureReason = string.Empty;

        string targetUserId = MultiplayerPlayModeTestContext.IsActive
            ? MultiplayerPlayModeTestContext.GetUserId((int)targetPlayer)
            : UserManager.instance != null && UserManager.instance.HasUser ? UserManager.instance.UserId : string.Empty;

        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            failureReason = $"The identity for {targetPlayer} could not be resolved.";
            return false;
        }

        if (!NetworkLobbyManager.instance.TryGetStressLobbyForUser(targetUserId, out lobby) || lobby?.Controller == null)
        {
            failureReason = $"{targetPlayer} is not currently in a network Lobby.";
            return false;
        }

        if (lobby.playMode != MainMenuPlayMode.Online && lobby.playMode != MainMenuPlayMode.Custom)
        {
            failureReason = "Chat stress simulations require an Online or Custom Lobby.";
            lobby = null;
            return false;
        }

        return true;
    }

    private int GetRequestedJoinPlayerCount(Lobby lobby)
    {
        if (lobby?.Controller == null)
        {
            return 0;
        }

        if (!useMaxLobbySize)
        {
            return Mathf.Max(1, playersToAdd);
        }

        return Mathf.Max(0, lobby.Controller.MaxPlayer - lobby.Controller.PlayerCount);
    }

    private bool StartFakePlayerJoin(Lobby lobby, int playerCount, out string failureReason)
    {
        failureReason = string.Empty;

        StressFakePlayerJoinRequest request = new StressFakePlayerJoinRequest
        {
            lobbyId = lobby.GetLobbyId(),
            playerCount = playerCount,
            minimumJoinBatch = minimumJoinBatch,
            maximumJoinBatch = maximumJoinBatch,
            minimumJoinDelaySeconds = minimumJoinDelaySeconds,
            maximumJoinDelaySeconds = maximumJoinDelaySeconds,
            minimumLoadDelaySeconds = minimumLoadDelaySeconds,
            maximumLoadDelaySeconds = maximumLoadDelaySeconds,
            firstPlayerIsHost = false,
            limitToAvailableLobbyCapacity = true
        };

        joinOperationId = StressFakePlayerManager.instance.StartJoinWave(request);

        if (joinOperationId > 0)
        {
            return true;
        }

        failureReason = "The synthetic player join wave could not start.";
        return false;
    }

    private async Task<StressFakePlayerJoinResult> RunJoinAndPlayerActivityAsync(int token)
    {
        StressFakePlayerJoinResult joinResult = null;

        while (IsCurrentRun(token))
        {
            bool joinCompleted = StressFakePlayerManager.instance != null &&
                                 StressFakePlayerManager.instance.TryGetJoinWaveResult(joinOperationId, out joinResult) &&
                                 joinResult != null && joinResult.completed;

            if (ShouldCancel())
            {
                if (!joinCompleted && joinOperationId > 0)
                {
                    StressFakePlayerManager.instance?.CancelJoinWave(joinOperationId, GetStopReason());
                }

                if (joinCompleted)
                {
                    return joinResult;
                }

                await Task.Yield();
                continue;
            }

            if (HasTimedOut())
            {
                StressFakePlayerManager.instance?.CancelJoinWave(joinOperationId, "The chat activity simulation exceeded its maximum run time.");
                return null;
            }

            if (joinCompleted && joinResult.outcome != StressFakePlayerJoinOutcome.Completed)
            {
                return joinResult;
            }

            StartNewlyReadyPlayerActivity(token, joinCompleted);

            if (joinCompleted && ArePlayerActivitiesComplete() && outboundMessages.Count == 0)
            {
                return joinResult;
            }

            await Task.Yield();
        }

        return null;
    }

    private void StartNewlyReadyPlayerActivity(int token, bool joinWaveCompleted)
    {
        if (StressFakePlayerManager.instance == null || string.IsNullOrWhiteSpace(activeLobbyId))
        {
            return;
        }

        List<StressFakePlayerRecord> readyPlayers = StressFakePlayerManager.instance.GetSceneReadyPlayersForLobby(activeLobbyId);

        for (int i = 0; i < readyPlayers.Count; i++)
        {
            StressFakePlayerRecord record = readyPlayers[i];

            if (record == null || record.operationId != joinOperationId || !startedActivityUserIds.Add(record.userId))
            {
                continue;
            }

            int readyOrdinal = nextReadyOrdinal++;
            simulatedUserIds.Add(record.userId);
            sceneReadyPlayers++;

            if (!joinWaveCompleted)
            {
                playersStartedBeforeJoinCompleted++;
            }

            if (blockedReadyOrdinals.Contains(readyOrdinal))
            {
                ApplyBlock(record);
            }

            playerActivityTasks.Add(RunPlayerActivityAsync(record, token));
        }
    }

    private bool ArePlayerActivitiesComplete()
    {
        for (int i = 0; i < playerActivityTasks.Count; i++)
        {
            Task task = playerActivityTasks[i];

            if (task != null && !task.IsCompleted)
            {
                return false;
            }
        }

        return true;
    }

#endif

    #endregion

    #region Blocking

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void PrepareBlockSelection(int expectedPlayerCount)
    {
        blockedReadyOrdinals.Clear();
        selectedBlockedUserIds.Clear();
        testAddedBlockedUserIds.Clear();

        int resolvedPlayerCount = Mathf.Max(0, expectedPlayerCount);
        int blockCount = Mathf.Min(Mathf.Max(0, blockedPlayers), resolvedPlayerCount);

        if (blockCount <= 0)
        {
            return;
        }

        List<int> ordinals = new List<int>(resolvedPlayerCount);

        for (int i = 0; i < resolvedPlayerCount; i++)
        {
            ordinals.Add(i);
        }

        Shuffle(ordinals);

        for (int i = 0; i < blockCount; i++)
        {
            blockedReadyOrdinals.Add(ordinals[i]);
        }
    }

    private void ApplyBlock(StressFakePlayerRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.userId))
        {
            return;
        }

        selectedBlockedUserIds.Add(record.userId);
        TryApplyBlock(record);
    }

    private void TryApplyPendingBlocks()
    {
        if (!isRunning || selectedBlockedUserIds.Count == 0 || string.IsNullOrWhiteSpace(activeLobbyId))
        {
            return;
        }

        ChatManager currentChatManager = ChatManager.instance;

        if (currentChatManager == null || !currentChatManager.IsReady)
        {
            return;
        }

        chatManager = currentChatManager;
        List<StressFakePlayerRecord> readyPlayers = StressFakePlayerManager.instance != null
            ? StressFakePlayerManager.instance.GetSceneReadyPlayersForLobby(activeLobbyId)
            : null;

        if (readyPlayers == null)
        {
            return;
        }

        for (int i = 0; i < readyPlayers.Count; i++)
        {
            StressFakePlayerRecord record = readyPlayers[i];

            if (record != null && selectedBlockedUserIds.Contains(record.userId))
            {
                TryApplyBlock(record);
            }
        }
    }

    private void TryApplyBlock(StressFakePlayerRecord record)
    {
        if (record == null || chatManager == null || !chatManager.IsReady || string.IsNullOrWhiteSpace(record.userId) ||
            chatManager.IsUserBlocked(record.userId))
        {
            return;
        }

        ChatParticipantData participant = new ChatParticipantData(record.userId, record.playerName, string.Empty);

        if (chatManager.SetUserBlocked(participant, true))
        {
            testAddedBlockedUserIds.Add(record.userId);
        }
    }

    private void CleanupTestBlocks()
    {
        if (chatManager != null)
        {
            foreach (string userId in testAddedBlockedUserIds)
            {
                chatManager.SetUserBlocked(userId, false);
            }
        }

        testAddedBlockedUserIds.Clear();
        selectedBlockedUserIds.Clear();
    }

#endif

    #endregion

    #region Message Tracking

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void SubscribeToMessages()
    {
        if (chatManager != null)
        {
            chatManager.MessageReceived -= OnMessageReceived;
            chatManager.MessageReceived += OnMessageReceived;
        }
    }

    private void UnsubscribeFromMessages()
    {
        if (chatManager != null)
        {
            chatManager.MessageReceived -= OnMessageReceived;
        }
    }

    private void OnMessageReceived(ChatMessageData message)
    {
        if (!isRunning || message == null || string.IsNullOrWhiteSpace(message.senderUserId) || !simulatedUserIds.Contains(message.senderUserId))
        {
            return;
        }

        deliveredMessages++;

        if (message.isPrivate)
        {
            deliveredPrivateMessages++;
        }
        else
        {
            deliveredPublicMessages++;
        }

        if (selectedBlockedUserIds.Contains(message.senderUserId))
        {
            blockedMessagesVisible++;
        }
    }

    private void RecordMessageSelection(ChatSimulationMessageEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        if (entry.kind == ChatSimulationMessageKind.FilterTest)
        {
            filterTestMessages++;

            if (!filterCounts.ContainsKey(entry.filterType))
            {
                filterCounts[entry.filterType] = 0;
            }

            filterCounts[entry.filterType]++;
        }
        else
        {
            normalMessages++;
        }

    }

#endif

    #endregion

    #region Finish / Report

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void FinishNotStarted(string reason)
    {
        StressHealthReporter.instance?.ReportTestNotStarted("Chat Activity Simulation", StressSimulationCoordinator.instance?.ActiveRunName, reason);
        ResetRuntimeState();
    }

    private void FinishRun(StressTestResult result, string reason)
    {
        if (!isRunning)
        {
            return;
        }

        UnsubscribeFromMessages();
        string summary = BuildSummary();
        CleanupTestBlocks();

        if (stressRunId > 0)
        {
            if (result == StressTestResult.Cancelled)
            {
                StressSimulationCoordinator.instance?.CancelRun(stressRunId, summary, reason);
            }
            else
            {
                StressSimulationCoordinator.instance?.CompleteRun(stressRunId, result == StressTestResult.Passed, summary, reason);
            }
        }

        ResetRuntimeState();
    }

    private string BuildSummary()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Lobby: {activeLobbyId}");
        builder.AppendLine($"Requested Synthetic Players: {requestedPlayers}");
        builder.AppendLine($"Scene Ready Synthetic Players: {sceneReadyPlayers}");
        builder.AppendLine($"Players Started Chat Activity: {startedActivityUserIds.Count}");
        builder.AppendLine($"Players Started Before Join Wave Completed: {playersStartedBeforeJoinCompleted}");
        builder.AppendLine($"Blocked Players Requested: {Mathf.Max(0, blockedPlayers)}");
        builder.AppendLine($"Blocked Players Selected: {selectedBlockedUserIds.Count}");
        builder.AppendLine($"Messages Requested: {totalMessagesRequested}");
        builder.AppendLine($"Synthetic Messages Distributed: {totalMessagesSent}");
        builder.AppendLine($"Messages Still Queued At End: {outboundMessages.Count}");
        builder.AppendLine($"Peak Message Queue Depth: {peakQueuedMessages}");
        builder.AppendLine($"Average Queue Wait: {(totalMessagesSent > 0 ? totalQueueWaitSeconds / totalMessagesSent : 0d):F3}s");
        builder.AppendLine($"Longest Queue Wait: {longestQueueWaitSeconds:F3}s");
        builder.AppendLine($"Failed Messages: {failedMessages}");
        builder.AppendLine($"Public Messages: {publicMessages}");
        builder.AppendLine($"Private Messages To Player 1: {privateMessages}");
        builder.AppendLine($"Messages Visible On Player 1: {deliveredMessages}");
        builder.AppendLine($"Visible Public Messages: {deliveredPublicMessages}");
        builder.AppendLine($"Visible Private Messages: {deliveredPrivateMessages}");
        builder.AppendLine($"Normal Pool Messages: {normalMessages}");
        builder.AppendLine($"Filter-Test Pool Messages: {filterTestMessages}");
        builder.AppendLine("Safe Text Provider Validation: Not part of this synthetic activity test");
        builder.AppendLine($"Messages Generated By Blocked Players: {blockedMessagesAttempted}");
        builder.AppendLine($"Blocked Messages Visible To Player 1: {blockedMessagesVisible}");
        builder.AppendLine($"Players Randomly Left: {playersLeft}");
        builder.AppendLine($"Failed Leaves: {failedLeaves}");

        foreach (KeyValuePair<ChatSimulationFilterType, int> pair in filterCounts)
        {
            if (pair.Value > 0 && pair.Key != ChatSimulationFilterType.None)
            {
                builder.AppendLine($"Filter {pair.Key}: {pair.Value}");
            }
        }

        builder.Append($"Block Verification: {(blockedMessagesVisible == 0 ? "PASS" : "FAIL")}");
        return builder.ToString();
    }

    private string BuildFailureReason()
    {
        if (blockedMessagesVisible > 0)
        {
            return "One or more messages from blocked synthetic players became visible to Player 1.";
        }

        if (failedMessages > 0)
        {
            return "One or more synthetic chat messages failed to inject into the normal receive path.";
        }

        if (failedLeaves > 0)
        {
            return "One or more randomized synthetic-player leaves failed.";
        }

        return "The chat activity simulation failed.";
    }

    private string BuildSetupSummary(Lobby lobby, int playerCount)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Target Player: {targetPlayer}");
        builder.AppendLine($"Lobby: {lobby.GetLobbyId()}");
        builder.AppendLine($"Use Max Lobby Size: {useMaxLobbySize}");
        builder.AppendLine($"Players To Add: {playerCount}");
        builder.AppendLine($"Join Batch: {Mathf.Max(1, minimumJoinBatch)} - {Mathf.Max(1, maximumJoinBatch)}");
        builder.AppendLine($"Join Delay: {Mathf.Max(0f, minimumJoinDelaySeconds):F2}s - {Mathf.Max(0f, maximumJoinDelaySeconds):F2}s");
        builder.AppendLine($"Load Delay: {Mathf.Max(0f, minimumLoadDelaySeconds):F2}s - {Mathf.Max(0f, maximumLoadDelaySeconds):F2}s");
        builder.AppendLine($"Messages Per Player: {Mathf.Max(1, minimumMessagesPerPlayer)} - {Mathf.Max(1, maximumMessagesPerPlayer)}");
        builder.AppendLine($"Message Delay: {Mathf.Max(0f, minimumMessageDelaySeconds):F2}s - {Mathf.Max(0f, maximumMessageDelaySeconds):F2}s");
        builder.AppendLine($"Blocked Players: {Mathf.Max(0, blockedPlayers)}");
        builder.AppendLine($"Synthetic Queue Max Messages/Second: {Mathf.Max(0.1f, maximumMessagesPerSecond):F1}");
        builder.AppendLine($"Synthetic Queue Max Bytes/Second: {Mathf.Max(1f, maximumBytesPerSecond):F0}");
        builder.AppendLine("Synthetic Message Delivery: Rate-limited authority queue distributed to connected real Lobby clients (no fake Vivox sends)");
        builder.AppendLine("Chat Off Behavior: each real client independently ignores synthetic messages while its Chat is off; simulation continues");
        builder.AppendLine($"Private Message Chance: {PrivateMessageChance:P0} (internal)");
        builder.Append($"Leave After Messaging Chance: {LeaveAfterMessagesChance:P0} (internal)");
        return builder.ToString();
    }

    private void ResetRuntimeState()
    {
        isRunning = false;
        localStopRequested = false;
        stressRunId = 0;
        joinOperationId = 0;
        activeLobbyId = string.Empty;
        localRecipientUserId = string.Empty;
        runtimeCancelReason = string.Empty;
        nextSyntheticDispatchTime = 0d;
        runSimulation = false;
        stopSimulation = false;
        chatManager = null;
        simulatedUserIds.Clear();
        startedActivityUserIds.Clear();
        selectedBlockedUserIds.Clear();
        testAddedBlockedUserIds.Clear();
        blockedReadyOrdinals.Clear();
        playerActivityTasks.Clear();
        outboundMessages.Clear();
    }

    private void ResetCounters()
    {
        requestedPlayers = 0;
        sceneReadyPlayers = 0;
        totalMessagesRequested = 0;
        totalMessagesSent = 0;
        failedMessages = 0;
        publicMessages = 0;
        privateMessages = 0;
        deliveredMessages = 0;
        deliveredPublicMessages = 0;
        deliveredPrivateMessages = 0;
        normalMessages = 0;
        filterTestMessages = 0;
        blockedMessagesAttempted = 0;
        blockedMessagesVisible = 0;
        playersLeft = 0;
        failedLeaves = 0;
        nextReadyOrdinal = 0;
        playersStartedBeforeJoinCompleted = 0;
        peakQueuedMessages = 0;
        totalQueueWaitSeconds = 0d;
        longestQueueWaitSeconds = 0d;
        filterCounts.Clear();
        simulatedUserIds.Clear();
        startedActivityUserIds.Clear();
        selectedBlockedUserIds.Clear();
        testAddedBlockedUserIds.Clear();
        blockedReadyOrdinals.Clear();
        playerActivityTasks.Clear();
        outboundMessages.Clear();
    }

#endif

    #endregion

    #region Helpers

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private bool IsCurrentRun(int token)
    {
        return isRunning && token == runToken;
    }

    private bool ShouldCancel()
    {
        return localStopRequested || (stressRunId > 0 && StressSimulationCoordinator.instance != null &&
               StressSimulationCoordinator.instance.IsStopRequestedFor(stressRunId));
    }

    private string GetStopReason()
    {
        if (!string.IsNullOrWhiteSpace(runtimeCancelReason))
        {
            return runtimeCancelReason;
        }

        if (StressSimulationCoordinator.instance != null && StressSimulationCoordinator.instance.IsStopRequestedFor(stressRunId) &&
            !string.IsNullOrWhiteSpace(StressSimulationCoordinator.instance.StopReason))
        {
            return StressSimulationCoordinator.instance.StopReason;
        }

        return "User stopped the chat activity simulation.";
    }

    private void RequestRuntimeCancel(string reason)
    {
        if (localStopRequested)
        {
            return;
        }

        runtimeCancelReason = string.IsNullOrWhiteSpace(reason) ? "The chat activity simulation was cancelled." : reason.Trim();
        localStopRequested = true;
        StressSimulationCoordinator.instance?.RequestStopRun(stressRunId, runtimeCancelReason);

        if (joinOperationId > 0)
        {
            StressFakePlayerManager.instance?.CancelJoinWave(joinOperationId, runtimeCancelReason);
        }
    }

    private bool HasTimedOut()
    {
        return runStartedTime > 0d && Time.unscaledTimeAsDouble - runStartedTime >= Mathf.Max(5f, maximumRunSeconds);
    }

    private async Task WaitForSecondsAsync(float seconds, int token)
    {
        double endTime = Time.unscaledTimeAsDouble + Mathf.Max(0f, seconds);

        while (IsCurrentRun(token) && !ShouldCancel() && !HasTimedOut() && Time.unscaledTimeAsDouble < endTime)
        {
            await Task.Yield();
        }
    }

    private float GetRandomMessageDelay()
    {
        float minimum = Mathf.Max(0f, Mathf.Min(minimumMessageDelaySeconds, maximumMessageDelaySeconds));
        float maximum = Mathf.Max(minimum, Mathf.Max(minimumMessageDelaySeconds, maximumMessageDelaySeconds));
        return Mathf.Approximately(minimum, maximum) ? minimum : UnityEngine.Random.Range(minimum, maximum);
    }

    private void Shuffle<T>(List<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            T value = values[i];
            values[i] = values[randomIndex];
            values[randomIndex] = value;
        }
    }

    private class QueuedSyntheticMessage
    {
        public ChatParticipantData sender;
        public string message = string.Empty;
        public bool isPrivate;
        public string recipientUserId = string.Empty;
        public double queuedTime;
    }

#endif

    #endregion
}
