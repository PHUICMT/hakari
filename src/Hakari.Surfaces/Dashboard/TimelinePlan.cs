using Hakari.Core.Localization;
using Hakari.Core.Querying;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// The bars a chart over time has for a filter: hours for today, else days, every one present
/// even with nothing spent so gaps read as gaps. All time shows its latest stretch.
/// </summary>
/// <param name="From">Where the first bar starts.</param>
internal sealed record TimelinePlan(
    GroupBy By,
    DateTimeOffset From,
    IReadOnlyList<TimelineBucket> Buckets)
{
    private const int AllTimeChartDays = 60;
    private const int HoursPerDay = 24;
    private const string DayFormat = "yyyy-MM-dd";
    private const string HourFormat = "yyyy-MM-dd HH:00";
    private static readonly System.Globalization.CultureInfo Invariant =
        System.Globalization.CultureInfo.InvariantCulture;

    public static TimelinePlan For(DashboardFilter filter, DateTimeOffset now)
    {
        var today = TimePeriods.StartOfToday(now);
        if (filter.Period == DashboardPeriod.Today)
        {
            return new TimelinePlan(
                GroupBy.Hour,
                today,
                [
                    .. Enumerable.Range(0, HoursPerDay).Select(hour => today.AddHours(hour))
                        .Select(at => new TimelineBucket(
                            at.ToString(HourFormat, Invariant),
                            at.ToString("HH", null))),
                ]);
        }

        var count = filter.Days ?? AllTimeChartDays;
        var first = today.AddDays(1 - count);
        return new TimelinePlan(
            GroupBy.Day,
            first,
            [
                .. Enumerable.Range(0, count).Select(offset => first.AddDays(offset))
                    .Select(day => new TimelineBucket(
                        day.ToString(DayFormat, Invariant),
                        day.ToString("MMM d", Texts.Culture))),
            ]);
    }
}
