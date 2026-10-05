using Hakari.Core.Querying;

namespace Hakari.Core.Tests.Querying;

public class TimePeriodsTests
{
    private static readonly DateTimeOffset Wednesday =
        new(2026, 10, 7, 15, 30, 0, TimeSpan.FromHours(7));

    [Fact]
    public void Today_starts_at_local_midnight() =>
        Assert.Equal(
            new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.FromHours(7)),
            TimePeriods.ToFilter(TimePeriods.Today, Wednesday).From);

    [Fact]
    public void Week_starts_on_monday() =>
        Assert.Equal(DayOfWeek.Monday, TimePeriods.StartOfWeek(Wednesday).DayOfWeek);

    [Fact]
    public void Day_count_includes_today() =>
        Assert.Equal(
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(7)),
            TimePeriods.ToFilter("7d", Wednesday).From);

    [Fact]
    public void All_has_no_lower_bound() =>
        Assert.Null(TimePeriods.ToFilter(TimePeriods.All, Wednesday).From);

    [Fact]
    public void Rejects_unknown_periods() =>
        Assert.Throws<ArgumentException>(() => TimePeriods.ToFilter("forever", Wednesday));
}
