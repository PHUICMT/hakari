using Hakari.Core.Limits;
using Hakari.Core.Presentation;

namespace Hakari.Core.Tests.Limits;

public sealed class EvenPaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static UsageLimit Limit(string group, DateTimeOffset? resetsAt) =>
        new("weekly_all", group, 40, "normal", resetsAt, null, IsActive: true);

    [Fact]
    public void A_week_half_gone_is_paced_at_half()
    {
        var pace = EvenPace.Of(Limit("weekly", Now.AddDays(3.5)), Now);

        Assert.Equal(0.5, pace!.Value, precision: 6);
    }

    [Fact]
    public void Only_weekly_limits_with_a_reset_have_a_pace()
    {
        Assert.Null(EvenPace.Of(Limit("session", Now.AddHours(2)), Now));
        Assert.Null(EvenPace.Of(Limit("weekly", null), Now));
    }

    [Fact]
    public void The_month_is_projected_from_its_part_so_far()
    {
        var halfway = new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero).ToLocalTime();
        var start = new DateTimeOffset(halfway.Year, halfway.Month, 1, 0, 0, 0, halfway.Offset);
        var projected = MonthProjection.Of(100m, start.AddDays(15));

        Assert.Equal(200m, decimal.Round(projected!.Value));
    }

    [Fact]
    public void The_first_day_of_a_month_projects_nothing()
    {
        var start = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero).ToLocalTime();
        var morning = new DateTimeOffset(start.Year, start.Month, 1, 9, 0, 0, start.Offset);

        Assert.Null(MonthProjection.Of(50m, morning));
    }
}
