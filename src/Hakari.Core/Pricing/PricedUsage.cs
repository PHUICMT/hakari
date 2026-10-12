using Hakari.Core.Usage;

namespace Hakari.Core.Pricing;

/// <summary>Everything that decides the cost of a group of responses.</summary>
/// <param name="PromptOver">The band each prompt in the group is in, as
/// <see cref="PromptSize.Over"/> gives it; 0 below the first threshold.</param>
public sealed record PricedUsage(
    string Model,
    string Speed,
    string? InferenceGeography,
    DateOnly UtcDay,
    TokenCounts Tokens,
    long WebSearchRequests,
    long PromptOver = 0)
{
    public static PricedUsage From(UsageRecord record) => new(
        record.Model,
        record.Speed,
        record.InferenceGeography,
        record.UtcDay,
        record.Tokens,
        record.WebSearchRequests,
        PromptSize.Over(record.Tokens.TotalInput));
}
