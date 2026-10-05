namespace Hakari.Core.Limits;

public sealed record LimitSnapshot(
    IReadOnlyList<UsageLimit> Limits,
    ExtraUsage? ExtraUsage,
    DateTimeOffset FetchedAt,
    LimitFreshness Freshness)
{
    /// <summary>
    /// A window whose reset time has passed has restarted at zero, which is known without
    /// asking the account again.
    /// </summary>
    public LimitSnapshot ProjectedTo(DateTimeOffset now) => this with
    {
        Limits = [.. Limits.Select(limit => HasReset(limit, now) ? Restarted(limit) : limit)],
    };

    private static bool HasReset(UsageLimit limit, DateTimeOffset now) =>
        limit.ResetsAt is { } resetsAt && resetsAt <= now;

    private static UsageLimit Restarted(UsageLimit limit) =>
        limit with { Percent = 0, ResetsAt = null, Severity = UsageLimitParser.NormalSeverity };
}
