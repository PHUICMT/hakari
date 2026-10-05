using Hakari.Core.Limits;

namespace Hakari.Core.Tests.Limits;

public sealed class PercentAnchorsTests
{
    private static readonly DateTimeOffset FirstRead = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ResetsAt = FirstRead.AddHours(3);
    private static readonly DateTimeOffset SecondRead = FirstRead.AddMinutes(2);

    [Fact]
    public void Keeps_the_first_moment_while_the_percent_stays()
    {
        var first = PercentAnchors.Carry(null, Snapshot(40, FirstRead));
        var second = PercentAnchors.Carry(first, Snapshot(40, SecondRead));

        Assert.Equal(FirstRead, second.Limits[0].PercentSince);
        Assert.False(second.Limits[0].RoseAtSince);
    }

    [Fact]
    public void Marks_a_rise_of_one_percent()
    {
        var first = PercentAnchors.Carry(null, Snapshot(40, FirstRead));
        var second = PercentAnchors.Carry(first, Snapshot(41, SecondRead));

        Assert.Equal(SecondRead, second.Limits[0].PercentSince);
        Assert.True(second.Limits[0].RoseAtSince);
    }

    [Fact]
    public void Starts_over_in_a_new_window()
    {
        var first = PercentAnchors.Carry(null, Snapshot(40, FirstRead));
        var fresh = Snapshot(41, SecondRead, ResetsAt.AddHours(5));
        var second = PercentAnchors.Carry(first, fresh);

        Assert.Equal(SecondRead, second.Limits[0].PercentSince);
        Assert.False(second.Limits[0].RoseAtSince);
    }

    private static LimitSnapshot Snapshot(
        int percent,
        DateTimeOffset fetchedAt,
        DateTimeOffset? resetsAt = null) =>
        new(
            [new UsageLimit("session", "session", percent, "normal", resetsAt ?? ResetsAt, null,
                IsActive: true)],
            null,
            fetchedAt,
            LimitFreshness.Live);
}
