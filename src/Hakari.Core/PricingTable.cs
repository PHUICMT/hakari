using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hakari.Core;

public sealed record FastPrice(decimal Input, decimal Output);

public sealed record ModelPrice(
    decimal Input,
    decimal Output,
    decimal CacheWrite5m,
    decimal CacheWrite1h,
    decimal CacheRead,
    FastPrice? Fast = null);

public sealed class PricingTable
{
    const decimal PerToken = 1_000_000m;

    [JsonPropertyName("models")] public Dictionary<string, ModelPrice> Models { get; init; } = [];
    [JsonPropertyName("aliases")] public Dictionary<string, string> Aliases { get; init; } = [];

    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static PricingTable Load(string json) =>
        JsonSerializer.Deserialize<PricingTable>(json, Options) ?? new PricingTable();

    public ModelPrice? Find(string model)
    {
        if (Aliases.TryGetValue(model, out var alias)) model = alias;
        if (Models.TryGetValue(model, out var p)) return p;
        var stripped = System.Text.RegularExpressions.Regex.Replace(model, @"-\d{8}$", "");
        return Models.GetValueOrDefault(stripped);
    }

    public decimal? Cost(UsageRecord r)
    {
        var p = Find(r.Model);
        if (p is null) return null;
        var fast = r.Speed == "fast" ? p.Fast : null;
        return (r.InputTokens * (fast?.Input ?? p.Input)
              + r.OutputTokens * (fast?.Output ?? p.Output)
              + r.CacheWrite5mTokens * p.CacheWrite5m
              + r.CacheWrite1hTokens * p.CacheWrite1h
              + r.CacheReadTokens * p.CacheRead) / PerToken;
    }
}
