using System;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class ChatCommandProcessor : MonoBehaviour
{
    private ChatManager chatManager;
    private ChatCommandCatalog commandCatalog;
    private bool isReady;

    public bool IsReady => isReady;

    public bool Initialize()
    {
        if (isReady)
        {
            return true;
        }

        chatManager = GetComponent<ChatManager>();
        commandCatalog = GetComponent<ChatCommandCatalog>();

        if (chatManager == null || commandCatalog == null || !commandCatalog.IsReady)
        {
            return false;
        }

        isReady = true;
        return true;
    }

    public async Task<ChatCommandResult> ProcessAsync(string input)
    {
        if (!isReady || string.IsNullOrWhiteSpace(input) || !input.TrimStart().StartsWith("/", StringComparison.Ordinal))
        {
            return ChatCommandResult.NotHandled();
        }

        string trimmedInput = input.Trim();
        int commandEndIndex = trimmedInput.IndexOf(' ');
        string commandToken = commandEndIndex < 0 ? trimmedInput.Substring(1) : trimmedInput.Substring(1, commandEndIndex - 1);
        string arguments = commandEndIndex < 0 ? string.Empty : trimmedInput.Substring(commandEndIndex + 1).TrimStart();

        ChatCommandDefinition command = commandCatalog.FindCommand(commandToken);

        if (command == null)
        {
            return ChatCommandResult.Failed("That chat command was not found. Type /help to view available commands.");
        }

        if (command.availability == ChatCommandAvailability.SessionOnly && !chatManager.HasSessionConversation)
        {
            return ChatCommandResult.Failed("That command is only available in multiplayer chat.");
        }

        switch (command.name)
        {
            case "help":
                return chatManager.RequestHelpToggle();

            case "msg":
                return await ProcessPrivateMessageAsync(arguments);

            case "block":
                return ProcessBlockCommand(arguments);

            case "unblock":
                return ProcessUnblockCommand(arguments);

            case "report":
                return ProcessReportCommand(arguments);

            case "friend":
                return ProcessFriendCommand(arguments);

            default:
                return ChatCommandResult.Failed("That chat command is not available.");
        }
    }

    private async Task<ChatCommandResult> ProcessPrivateMessageAsync(string arguments)
    {
        if (!chatManager.TryResolveCommandTargetAndRemainder(arguments, true, out ChatParticipantData participant, out string message))
        {
            return ChatCommandResult.Failed("Select a player and enter a message.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return ChatCommandResult.Failed("The private message is empty.");
        }

        ChatSendResult sendResult = await chatManager.SendPrivateSessionMessageAsync(participant.userId, message);
        return sendResult.success ? ChatCommandResult.Succeeded() : ChatCommandResult.Failed(sendResult.failureMessage);
    }

    private ChatCommandResult ProcessBlockCommand(string arguments)
    {
        if (!chatManager.TryResolveCommandTargetAndRemainder(arguments, false, out ChatParticipantData participant, out _, true))
        {
            return ChatCommandResult.Failed(ChatBlockError.GetMessage(ChatBlockErrorType.PlayerNotFound));
        }

        if (IsCurrentUser(participant.userId))
        {
            return ChatCommandResult.Failed(ChatBlockError.GetMessage(ChatBlockErrorType.CannotBlockSelf));
        }

        if (chatManager.IsUserBlocked(participant.userId))
        {
            return ChatCommandResult.Failed(ChatBlockError.GetMessage(ChatBlockErrorType.PlayerAlreadyBlocked));
        }

        if (!chatManager.SetUserBlocked(participant, true))
        {
            return ChatCommandResult.Failed(ChatBlockError.GetMessage(ChatBlockErrorType.PlayerNotFound));
        }

        return ChatCommandResult.Succeeded();
    }

    private ChatCommandResult ProcessUnblockCommand(string arguments)
    {
        ChatParticipantData participant = null;

        if (!chatManager.TryResolveCommandTargetAndRemainder(arguments, false, out participant, out _, true) &&
            !chatManager.TryResolveBlockedUserCommandTarget(arguments, out participant))
        {
            return ChatCommandResult.Failed(ChatBlockError.GetMessage(ChatBlockErrorType.PlayerNotFound));
        }

        if (IsCurrentUser(participant.userId))
        {
            return ChatCommandResult.Failed(ChatBlockError.GetMessage(ChatBlockErrorType.CannotBlockSelf));
        }

        if (!chatManager.IsUserBlocked(participant.userId))
        {
            return ChatCommandResult.Failed(ChatBlockError.GetMessage(ChatBlockErrorType.PlayerNotBlocked));
        }

        if (!chatManager.SetUserBlocked(participant.userId, false))
        {
            return ChatCommandResult.Failed(ChatBlockError.GetMessage(ChatBlockErrorType.PlayerNotBlocked));
        }

        return ChatCommandResult.Succeeded();
    }

    private ChatCommandResult ProcessReportCommand(string arguments)
    {
        if (!chatManager.TryResolveCommandTargetAndRemainder(arguments, false, out ChatParticipantData participant, out _))
        {
            return ChatCommandResult.Failed("Player not found.");
        }

        ChatConversationReference conversation = chatManager.SessionConversation?.Reference;

        if (!ChatReportController.OpenForParticipant(participant, conversation))
        {
            return ChatCommandResult.Failed("The report command failed.");
        }

        return ChatCommandResult.Succeeded();
    }

    private ChatCommandResult ProcessFriendCommand(string arguments)
    {
        if (!chatManager.TryResolveCommandTargetAndRemainder(arguments, false, out ChatParticipantData participant, out _))
        {
            return ChatCommandResult.Failed("Player not found.");
        }

        return chatManager.AddFriend(participant)
            ? ChatCommandResult.Succeeded()
            : ChatCommandResult.Failed("The player could not be added to friends.");
    }

    private bool IsCurrentUser(string userId)
    {
        return !string.IsNullOrWhiteSpace(userId) &&
               UserManager.instance != null &&
               UserManager.instance.HasUser &&
               string.Equals(UserManager.instance.UserId, userId, StringComparison.Ordinal);
    }
}
