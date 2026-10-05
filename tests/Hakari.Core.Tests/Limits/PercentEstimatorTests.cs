using Hakari.Core.Limits;

namespace Hakari.Core.Tests.Limits;

public sealed class PercentEstimatorTests
{
    private static readonly DateTimeOffset ReadAt = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ResetsAt = ReadAt.AddHours(3);

    [Fact]
    public void Adds_what_was_spent_since_the_reading()
    {
        // 40% cost $20, so $0.50 per percent; $0.20 since is 0.4% more.
        var estimate = PercentEstimator.Estimate(
            Session(40),
            ReadAt,
            ReadAt.AddMinutes(10),
            (from, to) => to <= ReadAt ? 20m : 0.2m);

        Assert.Equal(40.4, estimate, precision: 6);
    }

    [Fact]
    public void Never_runs_a_whole_percent_ahead()
    {
        var estimate = PercentEstimator.Estimate(
            Session(40),
            ReadAt,
            ReadAt.AddMinutes(10),
            (from, to) => to <= ReadAt ? 20m : 5m);

        Assert.Equal(40.99, estimate, precision: 6);
    }

    [Fact]
    public void Keeps_the_reading_when_it_cannot_tell()
    {
        Assert.Equal(0, PercentEstimator.Estimate(Session(0), ReadAt, ReadAt, (_, _) => 1m));
        Assert.Equal(
            40,
            PercentEstimator.Estimate(Session(40), ReadAt, ReadAt.AddMinutes(5), (_, _) => 0m));
    }

    private static UsageLimit Session(int percent) =>
        new("session", "session", percent, "normal", ResetsAt, null, IsActive: true);
}
