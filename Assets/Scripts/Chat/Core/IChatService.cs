using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IChatService
{
    bool IsReady { get; }
    string LastError { get; }

    event Action<ChatMessageData> MessageReceived;
    event Action<string> ServiceUnavailable;

    Task<bool> EnsureReadyAsync(ChatParticipantData localParticipant);
    void UpdateLocalParticipant(ChatParticipantData localParticipant);

    Task<bool> JoinConversationAsync(ChatConversationReference conversation);
    Task<bool> LeaveConversationAsync(ChatConversationReference conversation);
    Task<ChatSendResult> SendMessageAsync(ChatConversationReference conversation, string message);
    Task<IReadOnlyList<ChatMessageData>> GetHistoryAsync(ChatHistoryRequest request);
    Task ShutdownAsync();
}
