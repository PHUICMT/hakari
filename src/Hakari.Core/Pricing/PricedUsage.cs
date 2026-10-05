using Hakari.Core.Usage;

namespace Hakari.Core.Pricing;

/// <summary>Everything that decides the cost of a group of responses.</summary>
public sealed record PricedUsage(
    string Model,
    string Speed,
    string? InferenceGeography,
    DateOnly UtcDay,
    TokenCounts Tokens,
    long WebSearchRequests)
{
    public static PricedUsage From(UsageRecord record) => new(
        record.Model,
        record.Speed,
        record.InferenceGeography,
        record.UtcDay,
        record.Tokens,
        record.WebSearchRequests);
}
