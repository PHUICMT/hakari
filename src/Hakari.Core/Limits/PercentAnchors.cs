namespace Hakari.Core.Limits;

/// <summary>
/// Remembers, per limit, since when the server has reported the same whole percent, and
/// whether that percent was seen rising from the one below. The estimate builds on that
/// fixed moment, so a fresh reading of the same percent cannot pull it back.
/// </summary>
public static class PercentAnchors
{
    /// <summary>Reset times read a moment apart can differ by a little; same window.</summary>
    private static readonly TimeSpan SameWindowTolerance = TimeSpan.FromMinutes(1);

    public static LimitSnapshot Carry(LimitSnapshot? previous, LimitSnapshot fetched) =>
        fetched with
        {
            Limits =
            [
                .. fetched.Limits.Select(limit => Anchor(previous, limit, fetched.FetchedAt)),
            ],
        };

    private static UsageLimit Anchor(
        LimitSnapshot? previous,
        UsageLimit limit,
        DateTimeOffset fetchedAt)
    {
        var before = previous?.Limits.FirstOrDefault(earlier =>
            earlier.Kind == limit.Kind && SameWindow(earlier, limit));
        if (before is null || before.Percent > limit.Percent)
        {
            return limit with { PercentSince = fetchedAt, RoseAtSince = false };
        }

        if (before.Percent == limit.Percent)
        {
            return limit with
            {
                PercentSince = before.PercentSince ?? previous!.FetchedAt,
                RoseAtSince = before.RoseAtSince,
            };
        }

        var roseByOne = before.Percent == limit.Percent - 1;
        return limit with { PercentSince = fetchedAt, RoseAtSince = roseByOne };
    }

    private static bool SameWindow(UsageLimit earlier, UsageLimit limit) =>
        earlier.ResetsAt is { } earlierReset
        && limit.ResetsAt is { } reset
        && (earlierReset - reset).Duration() <= SameWindowTolerance;
}
