namespace Hakari.Core.Limits;

/// <summary>
/// The server reports limits in whole percent. The finer share is estimated from the
/// account's own spending in the window: what one percent cost up to the moment this whole
/// percent was first read, applied to all that was spent up to now. It stays within the
/// whole percent last read, so it can never get ahead of the next real reading.
/// </summary>
public static class PercentEstimator
{
    private const string SessionGroup = "session";
    private const string WeeklyAllKind = "weekly_all";
    private const double FullPercent = 100;

    /// <summary>Stays below the next whole percent until a reading confirms it.</summary>
    private const double MaximumGain = 0.99;
    private const double ReadingMidpoint = 0.5;

    private static readonly TimeSpan SessionWindow = TimeSpan.FromHours(5);
    private static readonly TimeSpan WeeklyWindow = TimeSpan.FromDays(7);

    /// <param name="costBetween">This account's spending between two moments.</param>
    public static double Estimate(
        UsageLimit limit,
        DateTimeOffset readAt,
        DateTimeOffset now,
        Func<DateTimeOffset, DateTimeOffset, decimal> costBetween)
    {
        if (limit.ResetsAt is not { } resetsAt
            || limit.Percent <= 0
            || limit.Percent >= FullPercent
            || WindowOf(limit) is not { } window
            || now <= readAt)
        {
            return limit.Percent;
        }

        var anchor = limit.PercentSince is { } since && since <= readAt ? since : readAt;
        var spentByAnchor = (double)costBetween(resetsAt - window, anchor);
        if (spentByAnchor <= 0)
        {
            return limit.Percent;
        }

        // Seen rising from the percent below, the share had just reached P at the anchor.
        // Otherwise it was somewhere in [P, P+1), and the middle is the fairest guess. The
        // anchor stays put while the percent does, so the estimate only ever climbs.
        var shareAtAnchor = limit.Percent + (limit.RoseAtSince ? 0 : ReadingMidpoint);
        var costPerPercent = spentByAnchor / shareAtAnchor;
        var spentNow = spentByAnchor + (double)costBetween(anchor, now);
        var estimate = spentNow / costPerPercent;
        var ceiling = Math.Min(limit.Percent + MaximumGain, FullPercent);
        return Math.Clamp(estimate, limit.Percent, ceiling);
    }

    /// <summary>Only the windows whose length is known: the 5 hours and the full week.</summary>
    public static TimeSpan? WindowOf(UsageLimit limit) =>
        limit.Group == SessionGroup ? SessionWindow
            : limit.Kind == WeeklyAllKind ? WeeklyWindow
            : null;
}
