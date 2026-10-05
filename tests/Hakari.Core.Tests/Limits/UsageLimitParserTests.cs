using Hakari.Core.Limits;
using Hakari.Core.Tests.Support;

namespace Hakari.Core.Tests.Limits;

public class UsageLimitParserTests
{
    private static readonly DateTimeOffset FetchedAt = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reads_every_limit_with_its_reset_time()
    {
        var snapshot = UsageLimitParser.Parse(SampleLimits.UsageResponse, FetchedAt);

        Assert.Equal(3, snapshot.Limits.Count);
        var weekly = snapshot.Limits.Single(limit => limit.Kind == "weekly_all");
        Assert.Equal(91, weekly.Percent);
        Assert.Equal("warning", weekly.Severity);
        Assert.Equal(new DateTimeOffset(2026, 10, 8, 15, 0, 0, TimeSpan.Zero), weekly.ResetsAt);
    }

    [Fact]
    public void Names_the_model_of_a_scoped_limit()
    {
        var snapshot = UsageLimitParser.Parse(SampleLimits.UsageResponse, FetchedAt);

        var scoped = snapshot.Limits.Single(limit => limit.Kind == "weekly_scoped");

        Assert.Equal("Fable", scoped.ScopeName);
    }

    [Fact]
    public void Converts_extra_usage_from_minor_units()
    {
        var extra = UsageLimitParser.Parse(SampleLimits.UsageResponse, FetchedAt).ExtraUsage;

        Assert.Equal(12.50m, extra?.Used);
        Assert.Equal(50.00m, extra?.MonthlyLimit);
        Assert.Equal(0.25, extra!.Utilization, 3);
    }

    [Fact]
    public void Shows_a_window_past_its_reset_as_restarted()
    {
        var snapshot = UsageLimitParser.Parse(SampleLimits.UsageResponse, FetchedAt);

        var afterSessionReset = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var projected = snapshot.ProjectedTo(afterSessionReset);

        var session = projected.Limits.Single(limit => limit.Kind == "session");
        Assert.Equal(0, session.Percent);
        Assert.Null(session.ResetsAt);
        Assert.Equal(91, projected.Limits.Single(limit => limit.Kind == "weekly_all").Percent);
    }
}
