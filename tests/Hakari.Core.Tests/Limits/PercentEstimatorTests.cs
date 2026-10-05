using Hakari.Core.Limits;

namespace Hakari.Core.Tests.Limits;

public sealed class PercentEstimatorTests
{
    private static readonly DateTimeOffset ReadAt = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ResetsAt = ReadAt.AddHours(3);

    [Fact]
    public void Places_a_fresh_reading_inside_its_whole_percent()
    {
        // 40% after $20.25: $0.50 per percent at the middle of 40..41, so 40.5% right away.
        var estimate = PercentEstimator.Estimate(
            Session(40),
            ReadAt,
            ReadAt.AddSeconds(1),
            (from, to) => to <= ReadAt ? 20.25m : 0m);

        Assert.Equal(40.5, estimate, precision: 6);
    }

    [Fact]
    public void Rises_with_what_was_spent_since_the_reading()
    {
        var estimate = PercentEstimator.Estimate(
            Session(40),
            ReadAt,
            ReadAt.AddMinutes(10),
            (from, to) => to <= ReadAt ? 20.25m : 0.2m);

        Assert.Equal(40.9, estimate, precision: 6);
    }

    [Fact]
    public void Stays_within_the_whole_percent_read()
    {
        var estimate = PercentEstimator.Estimate(
            Session(40),
            ReadAt,
            ReadAt.AddMinutes(10),
            (from, to) => to <= ReadAt ? 20.25m : 5m);

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

    [Fact]
    public void A_later_reading_of_the_same_percent_does_not_pull_it_back()
    {
        // $20 by the first 40% reading, $0.40 more by a second one ten minutes later.
        var anchored = Session(40) with { PercentSince = ReadAt, RoseAtSince = true };
        var secondReading = ReadAt.AddMinutes(10);
        decimal Cost(DateTimeOffset from, DateTimeOffset to) =>
            to <= ReadAt ? 20m : from >= ReadAt ? 0.4m : 20.4m;

        var estimate = PercentEstimator.Estimate(
            anchored,
            secondReading,
            secondReading.AddSeconds(1),
            Cost);

        Assert.Equal(40.8, estimate, precision: 6);
    }

    private static UsageLimit Session(int percent) =>
        new("session", "session", percent, "normal", ResetsAt, null, IsActive: true);
}
