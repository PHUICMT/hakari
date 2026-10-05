namespace Hakari.Core;

public sealed record UsageRecord(
    string MessageId,
    string? RequestId,
    DateTimeOffset Timestamp,
    string Model,
    string SessionId,
    string? Cwd,
    string? GitBranch,
    bool IsSidechain,
    string Speed,
    long InputTokens,
    long OutputTokens,
    long CacheWrite5mTokens,
    long CacheWrite1hTokens,
    long CacheReadTokens)
{
    public string DedupeKey => $"{MessageId}|{RequestId}";
}
