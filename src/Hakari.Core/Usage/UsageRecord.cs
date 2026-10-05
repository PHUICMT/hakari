namespace Hakari.Core.Usage;

public sealed record UsageRecord(
    string MessageId,
    string? RequestId,
    DateTimeOffset Timestamp,
    string Model,
    string SessionId,
    string? WorkingDirectory,
    string? GitBranch,
    bool IsSidechain,
    string Speed,
    TokenCounts Tokens)
{
    private const char DeduplicationSeparator = '|';

    public string DeduplicationKey => $"{MessageId}{DeduplicationSeparator}{RequestId}";
}
