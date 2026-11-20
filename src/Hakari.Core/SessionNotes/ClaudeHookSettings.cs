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
    private const string CommandType = "command";

    private static readonly string[] Events =
        [HookInput.SessionStart, HookInput.UserPromptSubmit, HookInput.PostToolUse];

    /// <summary>Text the user wrote, such as Thai or quotes, stays as written.</summary>
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The command line for Hakari.exe. Forward slashes and quotes work the same whether
    /// Claude Code runs hooks through Git Bash or cmd.
    /// </summary>
    public static string CommandFor(string executablePath) =>
        $"\"{executablePath.Replace('\\', '/')}\" {Flag}";

    /// <exception cref="JsonException">The file is not a JSON object; it is left alone.</exception>
    public static string WithHook(string? json, string command)
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
                    [CommandField] = command,
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
    public static bool HasHook(string? json, string command)
    {
        try
        {
            var hooks = ParseObject(json)[HooksField] as JsonObject;
            return Events.All(eventName => Commands(hooks?[eventName] as JsonArray)
                .Count(text => text.Contains(Flag, StringComparison.Ordinal)) == 1
                && Commands(hooks?[eventName] as JsonArray).Contains(command));
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

    private static IEnumerable<string> Commands(JsonArray? groups) =>
        groups?.OfType<JsonObject>()
            .SelectMany(group => (group[HooksField] as JsonArray ?? []).OfType<JsonObject>())
            .Select(CommandOf)
        ?? [];

    private static string CommandOf(JsonObject handler) =>
        handler[CommandField] is JsonValue value && value.TryGetValue<string>(out var command)
            ? command
            : string.Empty;

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
