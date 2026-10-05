using Hakari.Core.Usage;

namespace Hakari.Core.Querying;

public sealed record UsageSummary(
    string Key,
    long Messages,
    TokenCounts Tokens,
    decimal Cost,
    bool HasUnpricedModels,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen)
{
    public static UsageSummary Empty { get; } = new(
        Key: string.Empty,
        Messages: 0,
        Tokens: default,
        Cost: 0,
        HasUnpricedModels: false,
        FirstSeen: DateTimeOffset.MinValue,
        LastSeen: DateTimeOffset.MinValue);

    public UsageSummary Merge(UsageSummary other) => new(
        Key: Key,
        Messages: Messages + other.Messages,
        Tokens: Tokens + other.Tokens,
        Cost: Cost + other.Cost,
        HasUnpricedModels: HasUnpricedModels || other.HasUnpricedModels,
        FirstSeen: FirstSeen < other.FirstSeen ? FirstSeen : other.FirstSeen,
        LastSeen: LastSeen > other.LastSeen ? LastSeen : other.LastSeen);
}
