using System.Text.Json;

namespace Hakari.Core;

public static class UsageLineParser
{
    static ReadOnlySpan<byte> AssistantMarker => "\"type\":\"assistant\""u8;

    public static UsageRecord? TryParse(ReadOnlySpan<byte> line)
    {
        if (line.IndexOf(AssistantMarker) < 0) return null;

        try
        {
            using var doc = JsonDocument.Parse(line.ToArray());
            var root = doc.RootElement;
            if (!root.TryGetProperty("message", out var msg)) return null;
            if (!msg.TryGetProperty("usage", out var usage)) return null;
            if (!msg.TryGetProperty("id", out var id)) return null;

            var model = msg.TryGetProperty("model", out var m) ? m.GetString() ?? "unknown" : "unknown";
            if (model == "<synthetic>") return null;

            long cacheCreation = Long(usage, "cache_creation_input_tokens");
            long w5m = cacheCreation, w1h = 0;
            if (usage.TryGetProperty("cache_creation", out var cc))
            {
                w5m = Long(cc, "ephemeral_5m_input_tokens");
                w1h = Long(cc, "ephemeral_1h_input_tokens");
            }

            return new UsageRecord(
                MessageId: id.GetString()!,
                RequestId: Str(root, "requestId"),
                Timestamp: root.GetProperty("timestamp").GetDateTimeOffset(),
                Model: model,
                SessionId: Str(root, "sessionId") ?? "",
                Cwd: Str(root, "cwd"),
                GitBranch: Str(root, "gitBranch"),
                IsSidechain: root.TryGetProperty("isSidechain", out var sc) && sc.ValueKind == JsonValueKind.True,
                Speed: Str(usage, "speed") ?? "standard",
                InputTokens: Long(usage, "input_tokens"),
                OutputTokens: Long(usage, "output_tokens"),
                CacheWrite5mTokens: w5m,
                CacheWrite1hTokens: w1h,
                CacheReadTokens: Long(usage, "cache_read_input_tokens"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static long Long(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
