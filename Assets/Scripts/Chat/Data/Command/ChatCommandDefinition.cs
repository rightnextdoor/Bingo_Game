using System;

[Serializable]
public class ChatCommandDefinition
{
    public string name;
    public string[] aliases;
    public string usage;
    public string description;
    public ChatCommandAvailability availability;
    public bool enabled;
    public bool targetsSessionUser;

    public ChatCommandDefinition(string name, string usage, string description, ChatCommandAvailability availability, bool targetsSessionUser, params string[] aliases)
    {
        this.name = name ?? string.Empty;
        this.usage = usage ?? string.Empty;
        this.description = description ?? string.Empty;
        this.availability = availability;
        this.targetsSessionUser = targetsSessionUser;
        this.aliases = aliases ?? Array.Empty<string>();
        enabled = true;
    }

    public bool Matches(string commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return false;
        }

        if (string.Equals(name, commandName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (int i = 0; i < aliases.Length; i++)
        {
            if (string.Equals(aliases[i], commandName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
