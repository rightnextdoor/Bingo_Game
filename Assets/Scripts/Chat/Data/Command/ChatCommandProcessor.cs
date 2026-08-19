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
            return ChatCommandResult.Failed("That command is only available in a multiplayer Session chat.");
        }

        switch (command.name)
        {
            case "help":
                return chatManager.ToggleHelp(commandCatalog.BuildHelpMessage());

            case "msg":
            case "reply":
                return await ProcessPrivateMessageAsync(arguments);

            case "block":
            case "unblock":
            case "friend":
            case "report":
                return ProcessPlaceholderUserCommand(command.name, arguments);

            default:
                return ChatCommandResult.Failed("That chat command is not available.");
        }
    }

    private async Task<ChatCommandResult> ProcessPrivateMessageAsync(string arguments)
    {
        if (!chatManager.TryResolveCommandTargetAndRemainder(arguments, true, out ChatParticipantData participant, out string message))
        {
            return ChatCommandResult.Failed("Select a Session player and enter a message.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return ChatCommandResult.Failed("The private message is empty.");
        }

        ChatSendResult sendResult = await chatManager.SendPrivateSessionMessageAsync(participant.userId, message);
        return sendResult.success ? ChatCommandResult.Succeeded() : ChatCommandResult.Failed(sendResult.failureMessage);
    }

    private ChatCommandResult ProcessPlaceholderUserCommand(string commandName, string arguments)
    {
        if (!chatManager.TryResolveCommandTargetAndRemainder(arguments, false, out ChatParticipantData participant, out _))
        {
            return ChatCommandResult.Failed("Select a Session player for that command.");
        }

        string displayName = chatManager.GetParticipantDisplayName(participant.userId);
        Debug.Log($"[ChatCommand] {GetCommandLogLabel(commandName)} requested for {displayName}.");
        return ChatCommandResult.Succeeded();
    }

    private string GetCommandLogLabel(string commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return "Command";
        }

        return char.ToUpperInvariant(commandName[0]) + commandName.Substring(1);
    }
}
