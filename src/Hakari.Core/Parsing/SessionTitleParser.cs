using System.Text;
using System.Text.Json;

namespace Hakari.Core.Parsing;

/// <summary>
/// Reads a session's title from the two kinds of line that carry one. Only used when the user
/// has asked for titles: they are written from the conversation, unlike the usage fields the
/// rest of Hakari reads. Nothing but the title and the session it belongs to is taken.
/// </summary>
public static class SessionTitleParser
{
    private static readonly byte[] AiTitleMarker =
        Encoding.UTF8.GetBytes($"\"{LogFieldNames.Type}\":\"{LogFieldValues.AiTitleType}\"");

    private static readonly byte[] CustomTitleMarker =
        Encoding.UTF8.GetBytes($"\"{LogFieldNames.Type}\":\"{LogFieldValues.CustomTitleType}\"");

    public static SessionTitle? TryParse(ReadOnlySpan<byte> line)
    {
        var isCustom = line.IndexOf(CustomTitleMarker) >= 0;
        if (!isCustom && line.IndexOf(AiTitleMarker) < 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line.ToArray());
            var root = document.RootElement;
            var titleField = isCustom ? LogFieldNames.CustomTitle : LogFieldNames.AiTitle;
            return root.GetStringOrNull(LogFieldNames.SessionId) is { Length: > 0 } sessionId
                && root.GetStringOrNull(titleField) is { Length: > 0 } title
                    ? new SessionTitle(sessionId, title.Trim(), isCustom)
                    : null;
        }
        catch (Exception exception) when (exception is JsonException
            or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
