using System;

public enum ChatReportReason
{
    None,
    ThreatsOrViolence,
    SexualContentOrBehavior,
    HateOrDiscrimination,
    HarassmentOrBullying,
    Spam,
    Other
}

[Serializable]
public class ChatReportData
{
    public string reporterUserId;
    public string reportedUserId;
    public string reportedPlayerName;

    public ChatReportReason reason;
    public string message;

    public string conversationId;
    public ChatConversationType conversationType;

    public long createdUnixMilliseconds;

    public ChatReportData()
    {
        reporterUserId = string.Empty;
        reportedUserId = string.Empty;
        reportedPlayerName = string.Empty;
        reason = ChatReportReason.None;
        message = string.Empty;
        conversationId = string.Empty;
    }

    public ChatReportData Clone()
    {
        return new ChatReportData
        {
            reporterUserId = reporterUserId,
            reportedUserId = reportedUserId,
            reportedPlayerName = reportedPlayerName,
            reason = reason,
            message = message,
            conversationId = conversationId,
            conversationType = conversationType,
            createdUnixMilliseconds = createdUnixMilliseconds
        };
    }
}
