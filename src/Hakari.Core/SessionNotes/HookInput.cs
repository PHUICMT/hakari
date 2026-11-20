using System.Text.Json;

namespace Hakari.Core.SessionNotes;

/// <summary>
/// The part of what Claude Code sends a hook that Hakari needs. The prompt and tool fields it
/// also sends are never read.
/// </summary>
/// <param name="Source">For a session start: startup, resume, clear or compact.</param>
public sealed record HookInput(
    string EventName,
    string SessionId,
    string TranscriptPath,
    string? Source)
{
    public const string SessionStart = "SessionStart";
    public const string UserPromptSubmit = "UserPromptSubmit";
    public const string PostToolUse = "PostToolUse";

    private const string EventField = "hook_event_name";
    private const string SessionField = "session_id";
    private const string TranscriptField = "transcript_path";
    private const string SourceField = "source";

    /// <returns>Null when the text is not a hook's input.</returns>
    public static HookInput? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && Text(root, EventField) is { } eventName
                && Text(root, SessionField) is { } sessionId
                && Text(root, TranscriptField) is { } transcriptPath
                ? new HookInput(eventName, sessionId, transcriptPath, Text(root, SourceField))
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string field) =>
        root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
