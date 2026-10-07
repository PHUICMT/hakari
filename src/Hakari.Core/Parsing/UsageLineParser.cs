using System.Text;
using System.Text.Json;
using Hakari.Core.Usage;

namespace Hakari.Core.Parsing;

public static class UsageLineParser
{
    private static readonly byte[] AssistantLineMarker =
        Encoding.UTF8.GetBytes($"\"{LogFieldNames.Type}\":\"{LogFieldValues.AssistantType}\"");

    public static UsageRecord? TryParse(ReadOnlySpan<byte> line) =>
        TryParse(line, checkThinking: false, out _);

    /// <summary>
    /// Reads the usage record and, when asked, whether the response had a thinking part,
    /// from one parse of the line.
    /// </summary>
    public static UsageRecord? TryParse(
        ReadOnlySpan<byte> line,
        bool checkThinking,
        out bool hasThinking)
    {
        hasThinking = false;
        if (line.IndexOf(AssistantLineMarker) < 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line.ToArray());
            var record = ReadRecord(document.RootElement);
            hasThinking = record is not null && checkThinking
                && ThinkingMarkParser.HasThinking(line, document.RootElement);
            return record;
        }
        catch (Exception exception) when (exception is JsonException
            or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static UsageRecord? ReadRecord(JsonElement root)
    {
        if (!root.TryGetObject(LogFieldNames.Message, out var message)
            || !message.TryGetObject(LogFieldNames.Usage, out var usage)
            || message.GetStringOrNull(LogFieldNames.MessageId) is not { } messageId
            || !TryReadTimestamp(root, out var timestamp))
        {
            return null;
        }

        var model = message.GetStringOrNull(LogFieldNames.Model) ?? LogFieldValues.UnknownModel;
        if (model == LogFieldValues.SyntheticModel)
        {
            return null;
        }

        return new UsageRecord(
            MessageId: messageId,
            RequestId: root.GetStringOrNull(LogFieldNames.RequestId),
            Timestamp: timestamp,
            Model: model,
            SessionId: root.GetStringOrNull(LogFieldNames.SessionId) ?? string.Empty,
            WorkingDirectory: root.GetStringOrNull(LogFieldNames.WorkingDirectory),
            GitBranch: root.GetStringOrNull(LogFieldNames.GitBranch),
            IsSidechain: root.IsTrue(LogFieldNames.IsSidechain),
            Speed: usage.GetStringOrNull(LogFieldNames.Speed) ?? SpeedNames.Standard,
            InferenceGeography: usage.GetStringOrNull(LogFieldNames.InferenceGeography),
            Tokens: ReadTokens(usage),
            WebSearchRequests: ReadWebSearchRequests(usage));
    }

    private static long ReadWebSearchRequests(JsonElement usage) =>
        usage.TryGetObject(LogFieldNames.ServerToolUse, out var serverToolUse)
            ? serverToolUse.GetInt64OrZero(LogFieldNames.WebSearchRequests)
            : 0;

    private static TokenCounts ReadTokens(JsonElement usage)
    {
        var (fiveMinuteWrites, oneHourWrites) = ReadCacheWrites(usage);
        return new TokenCounts(
            Input: usage.GetInt64OrZero(LogFieldNames.InputTokens),
            Output: usage.GetInt64OrZero(LogFieldNames.OutputTokens),
            CacheWriteFiveMinutes: fiveMinuteWrites,
            CacheWriteOneHour: oneHourWrites,
            CacheRead: usage.GetInt64OrZero(LogFieldNames.CacheReadInputTokens));
    }

    private static (long FiveMinutes, long OneHour) ReadCacheWrites(JsonElement usage)
    {
        if (!usage.TryGetObject(LogFieldNames.CacheCreation, out var cacheCreation))
        {
            var unsplitWrites = usage.GetInt64OrZero(LogFieldNames.CacheCreationInputTokens);
            return (unsplitWrites, 0);
        }

        return (
            cacheCreation.GetInt64OrZero(LogFieldNames.CacheWriteFiveMinutes),
            cacheCreation.GetInt64OrZero(LogFieldNames.CacheWriteOneHour));
    }

    private static bool TryReadTimestamp(JsonElement root, out DateTimeOffset timestamp)
    {
        timestamp = default;
        return root.TryGetProperty(LogFieldNames.Timestamp, out var property)
            && property.ValueKind == JsonValueKind.String
            && property.TryGetDateTimeOffset(out timestamp);
    }
}
