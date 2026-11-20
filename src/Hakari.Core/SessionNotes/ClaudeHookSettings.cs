using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hakari.Core.SessionNotes;

/// <summary>
/// Adds Hakari's hook to a Claude Code settings.json and takes it out again, leaving every
/// other setting and hook as it was. Hakari's entries are known by their command's flag, as
/// hooks carry no name.
/// </summary>
public static class ClaudeHookSettings
{
    public const string Flag = "--usage-note";
    public const string FileName = "settings.json";

    /// <summary>Starting Hakari and reading one row takes well under a second.</summary>
    private const int TimeoutSeconds = 10;

    private const string HooksField = "hooks";
    private const string TypeField = "type";
    private const string CommandField = "command";
    private const string TimeoutField = "timeout";
    private const string ShellField = "shell";
    private const string CommandType = "command";
    private const string BashName = "bash";
    private const string PowerShellName = "powershell";

    private static readonly string[] Events =
        [HookInput.SessionStart, HookInput.UserPromptSubmit, HookInput.PostToolUse];

    /// <summary>Text the user wrote, such as Thai or quotes, stays as written.</summary>
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The command line for Hakari.exe in the shell that will run it. Each first checks that
    /// Hakari is still there and otherwise ends quietly, because uninstalling, above all from
    /// the Store, cannot take the hook out, and a hook that fails shows an error in every
    /// session.
    /// </summary>
    public static HookCommand CommandFor(string executablePath, HookShell shell) => shell switch
    {
        HookShell.PowerShell => new HookCommand(PowerShellCommand(executablePath), shell),
        _ => new HookCommand(BashCommand(executablePath), shell),
    };

    private static string BashCommand(string executablePath)
    {
        var path = executablePath.Replace('\\', '/');
        return $"[ -e \"{path}\" ] && \"{path}\" {Flag}; exit 0";
    }

    private static string PowerShellCommand(string executablePath)
    {
        var path = executablePath.Replace("'", "''");
        return $"if (Test-Path -LiteralPath '{path}') {{ & '{path}' {Flag} }}";
    }

    /// <exception cref="JsonException">The file is not a JSON object; it is left alone.</exception>
    public static string WithHook(string? json, HookCommand command)
    {
        var root = ParseObject(json);
        RemoveOurs(root);
        var hooks = root[HooksField] as JsonObject ?? [];
        root[HooksField] = hooks;
        foreach (var eventName in Events)
        {
            var groups = hooks[eventName] as JsonArray ?? [];
            hooks[eventName] = groups;
            groups.Add(new JsonObject
            {
                [HooksField] = new JsonArray(new JsonObject
                {
                    [TypeField] = CommandType,
                    [CommandField] = command.Command,
                    [ShellField] = ShellName(command.Shell),
                    [TimeoutField] = TimeoutSeconds,
                }),
            });
        }

        return root.ToJsonString(Indented);
    }

    /// <exception cref="JsonException">The file is not a JSON object; it is left alone.</exception>
    public static string WithoutHook(string json)
    {
        var root = ParseObject(json);
        RemoveOurs(root);
        return root.ToJsonString(Indented);
    }

    /// <summary>Whether every event runs exactly this command, so nothing needs writing.</summary>
    public static bool HasHook(string? json, HookCommand command)
    {
        try
        {
            var hooks = ParseObject(json)[HooksField] as JsonObject;
            return Events.All(eventName =>
            {
                var ours = Handlers(hooks?[eventName] as JsonArray)
                    .Where(handler => CommandOf(handler).Contains(Flag, StringComparison.Ordinal))
                    .ToList();
                return ours.Count == 1
                    && CommandOf(ours[0]) == command.Command
                    && TextOf(ours[0], ShellField) == ShellName(command.Shell);
            });
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool HasAnyHook(string? json)
    {
        try
        {
            return ParseObject(json)[HooksField] is JsonObject hooks
                && hooks.Any(entry => Commands(entry.Value as JsonArray)
                    .Any(text => text.Contains(Flag, StringComparison.Ordinal)));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonObject ParseObject(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : JsonNode.Parse(json) as JsonObject
                ?? throw new JsonException("Claude Code settings are not a JSON object.");

    private static IEnumerable<JsonObject> Handlers(JsonArray? groups) =>
        groups?.OfType<JsonObject>()
            .SelectMany(group => (group[HooksField] as JsonArray ?? []).OfType<JsonObject>())
        ?? [];

    private static IEnumerable<string> Commands(JsonArray? groups) =>
        Handlers(groups).Select(CommandOf);

    private static string CommandOf(JsonObject handler) => TextOf(handler, CommandField);

    private static string TextOf(JsonObject handler, string field) =>
        handler[field] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : string.Empty;

    private static string ShellName(HookShell shell) =>
        shell == HookShell.PowerShell ? PowerShellName : BashName;

    /// <summary>Drops Hakari's handlers, then any group, event or hooks list left empty.</summary>
    private static void RemoveOurs(JsonObject root)
    {
        if (root[HooksField] is not JsonObject hooks)
        {
            return;
        }

        foreach (var (eventName, value) in hooks.ToList())
        {
            if (value is not JsonArray groups)
            {
                continue;
            }

            foreach (var group in groups.OfType<JsonObject>().ToList())
            {
                RemoveOursFrom(group);
                if (group[HooksField] is JsonArray { Count: 0 })
                {
                    groups.Remove(group);
                }
            }

            if (groups.Count == 0)
            {
                hooks.Remove(eventName);
            }
        }

        if (hooks.Count == 0)
        {
            root.Remove(HooksField);
        }
    }

    private static void RemoveOursFrom(JsonObject group)
    {
        if (group[HooksField] is not JsonArray handlers)
        {
            return;
        }

        foreach (var handler in handlers.OfType<JsonObject>().ToList())
        {
            if (CommandOf(handler).Contains(Flag, StringComparison.Ordinal))
            {
                handlers.Remove(handler);
            }
        }
    }
}
