using Hakari.Core.Querying;

namespace Hakari.Core.Presentation;

/// <summary>
/// What the month will come to if the rest of it goes like the part so far. Too early in the
/// month one busy day would decide everything, so it waits for a day of data.
/// </summary>
public static class MonthProjection
{
    private static readonly TimeSpan Settled = TimeSpan.FromDays(1);

    /// <returns>The projected total, or null while the month is too young to say.</returns>
    public static decimal? Of(decimal spentSoFar, DateTimeOffset now)
    {
        var start = TimePeriods.StartOfMonth(now);
        var elapsed = now - start;
        if (elapsed < Settled)
        {
            return null;
        }

        var length = start.AddMonths(1) - start;
        return spentSoFar * (decimal)(length / elapsed);
    }

}
