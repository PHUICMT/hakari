using System.Text;
using System.Text.Json;

namespace Hakari.Core.Parsing;

/// <summary>
/// Tells whether a response line holds a thinking part. Only used when the user turned on
/// counting thinking: it looks at the kind of each part of the response, never at what any
/// part says, and keeps nothing but yes or no.
/// </summary>
public static class ThinkingMarkParser
{
    private const string ContentField = "content";
    private const string ThinkingType = "thinking";
    private const string RedactedThinkingType = "redacted_thinking";

    private static readonly byte[] ThinkingMarker = Encoding.UTF8.GetBytes("thinking\"");

    public static bool HasThinking(ReadOnlySpan<byte> line)
    {
        if (line.IndexOf(ThinkingMarker) < 0)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(line.ToArray());
            return HasThinkingPart(document.RootElement);
        }
        catch (Exception exception) when (exception is JsonException
            or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    /// <summary>The same answer from a line already parsed, so it is not parsed twice.</summary>
    public static bool HasThinking(ReadOnlySpan<byte> line, JsonElement root) =>
        line.IndexOf(ThinkingMarker) >= 0 && HasThinkingPart(root);

    private static bool HasThinkingPart(JsonElement root) =>
        root.TryGetObject(LogFieldNames.Message, out var message)
        && message.TryGetField(ContentField, out var content)
        && content.ValueKind == JsonValueKind.Array
        && content.EnumerateArray().Any(part =>
            part.ValueKind == JsonValueKind.Object
            && part.GetStringOrNull(LogFieldNames.Type) is ThinkingType or RedactedThinkingType);
}