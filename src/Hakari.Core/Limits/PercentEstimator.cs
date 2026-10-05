namespace Hakari.Core.Limits;

/// <summary>
/// The server reports limits in whole percent. Between readings, the share used is estimated
/// from the account's own spending: what one percent cost so far in this window, applied to
/// what was spent since the reading. It never runs a whole percent past the last reading, so
/// it cannot get ahead of the next real one.
/// </summary>
public static class PercentEstimator
{
    private const string SessionGroup = "session";
    private const string WeeklyAllKind = "weekly_all";
    private const double FullPercent = 100;

    /// <summary>Stays below the next whole percent until a reading confirms it.</summary>
    private const double MaximumGain = 0.99;

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

        var costPerPercent = spentByReading / limit.Percent;
        var spentSince = (double)costBetween(readAt, now);
        var gain = Math.Clamp(spentSince / costPerPercent, 0, MaximumGain);
        return Math.Min(limit.Percent + gain, FullPercent);
    }

    /// <summary>Only the windows whose length is known: the 5 hours and the full week.</summary>
    public static TimeSpan? WindowOf(UsageLimit limit) =>
        limit.Group == SessionGroup ? SessionWindow
            : limit.Kind == WeeklyAllKind ? WeeklyWindow
            : null;
}
