using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class ChatCommandCatalog : MonoBehaviour
{
    private readonly List<ChatCommandDefinition> commands = new List<ChatCommandDefinition>();
    private bool isReady;

    public bool IsReady => isReady;
    public IReadOnlyList<ChatCommandDefinition> Commands => commands;

    public bool Initialize()
    {
        if (isReady)
        {
            return true;
        }

        commands.Clear();
        commands.Add(new ChatCommandDefinition("help", "/help", "Show or hide chat help.", ChatCommandAvailability.All, false));
        commands.Add(new ChatCommandDefinition("msg", "/msg <player> <message>", "Send a private message.", ChatCommandAvailability.SessionOnly, true, "message", "whisper", "w"));
        commands.Add(new ChatCommandDefinition("block", "/block <player>", "Block a player.", ChatCommandAvailability.SessionOnly, true));
        commands.Add(new ChatCommandDefinition("unblock", "/unblock <player>", "Unblock a player.", ChatCommandAvailability.SessionOnly, true));
        commands.Add(new ChatCommandDefinition("friend", "/friend <player>", "Add a player as a friend.", ChatCommandAvailability.SessionOnly, true));
        commands.Add(new ChatCommandDefinition("report", "/report <player>", "Report a player.", ChatCommandAvailability.SessionOnly, true));

        isReady = true;
        return true;
    }

    public ChatCommandDefinition FindCommand(string commandName)
    {
        if (!isReady || string.IsNullOrWhiteSpace(commandName))
        {
            return null;
        }

        for (int i = 0; i < commands.Count; i++)
        {
            ChatCommandDefinition command = commands[i];

            if (command != null && command.enabled && command.Matches(commandName))
            {
                return command;
            }
        }

        return null;
    }

    public string BuildHelpMessage()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Chat Commands");
        builder.AppendLine();

        for (int i = 0; i < commands.Count; i++)
        {
            ChatCommandDefinition command = commands[i];

            if (command == null || !command.enabled)
            {
                continue;
            }

            builder.Append(command.usage);
            builder.Append('\t');
            builder.AppendLine(command.description);
        }

        return builder.ToString().TrimEnd();
    }
}
