namespace Hakari.Core.Limits;

/// <summary>
/// Where a weekly limit would be by now if it were used evenly across its week: a day and a
/// half in, an even pace is at about 21%. Above that mark the week will run out early.
/// </summary>
public static class EvenPace
{
    private const string WeeklyGroup = "weekly";
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);

    /// <summary>0 to 1 through the week; null unless weekly with a reset.</summary>
    public static double? Of(UsageLimit limit, DateTimeOffset now)
    {
        if (limit.Group != WeeklyGroup || limit.ResetsAt is not { } resetsAt)
        {
            return null;
        }

        var elapsed = now - (resetsAt - Week);
        return Math.Clamp(elapsed / Week, 0, 1);
    }
}
