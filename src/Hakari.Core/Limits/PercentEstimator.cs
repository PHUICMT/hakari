namespace Hakari.Core.Limits;

/// <summary>
/// The server reports limits in whole percent. The finer share is estimated from the
/// account's own spending in the window: what one percent cost so far, applied to all that
/// was spent up to now. It stays within the whole percent last read, so it can never get
/// ahead of (or fall behind) the next real reading.
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

        var spentByReading = (double)costBetween(resetsAt - window, readAt);
        if (spentByReading <= 0)
        {
            return limit.Percent;
        }

        // A whole-percent reading means somewhere in [P, P+1); taking the middle keeps the
        // estimate from sitting on ".0" every time a fresh reading arrives.
        var costPerPercent = spentByReading / (limit.Percent + ReadingMidpoint);
        var spentNow = spentByReading + (double)costBetween(readAt, now);
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
