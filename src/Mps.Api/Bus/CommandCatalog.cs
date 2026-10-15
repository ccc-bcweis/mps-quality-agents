// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Auth;
using Mps.Application.Errors;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Api.Bus;

public sealed class CommandCatalog
{
    private readonly Dictionary<string, CommandSpec> _commands;

    private CommandCatalog(IEnumerable<CommandSpec> commands)
    {
        _commands = new Dictionary<string, CommandSpec>(StringComparer.Ordinal);
        foreach (var command in commands)
        {
            if (!string.IsNullOrWhiteSpace(command.Name))
            {
                _commands[command.Name.Trim()] = command;
            }
        }
    }

    public IReadOnlyCollection<CommandSpec> Commands => _commands.Values;

    public static CommandCatalog Load(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        var path = Path.Combine(dataDirectory, "commands", "catalog.yaml");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The command catalog was not found.", path);
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var document = deserializer.Deserialize<CommandCatalogFile>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("The command catalog is empty.");
        return new CommandCatalog(document.Commands ?? []);
    }

    public CommandSpec Require(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !_commands.TryGetValue(name.Trim(), out var command))
        {
            throw new IntakeValidationException("Unknown command.");
        }

        return command;
    }

    /// <summary>
    /// Local mode is the dev admin and may issue every command.
    /// A hosted caller who is not an admin may not issue a system command.
    /// </summary>
    public void EnsureMayIssue(string name, DemoCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var command = Require(name);
        var hosted = string.Equals(caller.Mode, EasyAuthPrincipal.AzureMode, StringComparison.OrdinalIgnoreCase);
        if (!hosted || caller.IsAdmin)
        {
            return;
        }

        if (string.Equals(command.Kind, "system", StringComparison.OrdinalIgnoreCase))
        {
            throw new CommandDeniedException("This command is only available to an admin in the hosted demo.");
        }
    }
}

public sealed class CommandSpec
{
    public string Name { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public List<string> WhoMayIssue { get; set; } = [];

    public List<string> Emits { get; set; } = [];

    public List<CommandField> Payload { get; set; } = [];
}

public sealed class CommandField
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public bool Optional { get; set; }
}

public sealed class CommandCatalogFile
{
    public List<CommandSpec> Commands { get; set; } = [];
}
