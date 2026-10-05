using Hakari.Core.Limits;

namespace Hakari.Core.Tests.Limits;

public sealed class LimitForecasterTests
{
    private const string AccountId = "account-1";
    private static readonly DateTimeOffset Start = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ResetsAt = Start.AddHours(4);

    [Fact]
    public void Projects_the_recent_pace_to_full()
    {
        var forecaster = new LimitForecaster();
        forecaster.Record(AccountId, Snapshot(Start, 40));
        forecaster.Record(AccountId, Snapshot(Start.AddMinutes(10), 50));

        var fullAt = forecaster.FullAt(AccountId, Session(50), Start.AddMinutes(10));

        Assert.Equal(Start.AddMinutes(60), fullAt);
    }

    [Fact]
    public void Stays_silent_when_the_limit_resets_before_it_fills()
    {
        var forecaster = new LimitForecaster();
        forecaster.Record(AccountId, Snapshot(Start, 10));
        forecaster.Record(AccountId, Snapshot(Start.AddMinutes(10), 11));

        Assert.Null(forecaster.FullAt(AccountId, Session(11), Start.AddMinutes(10)));
    }

    [Fact]
    public void Needs_readings_far_enough_apart()
    {
        var forecaster = new LimitForecaster();
        forecaster.Record(AccountId, Snapshot(Start, 40));
        forecaster.Record(AccountId, Snapshot(Start.AddMinutes(2), 45));

        Assert.Null(forecaster.FullAt(AccountId, Session(45), Start.AddMinutes(2)));
    }

    [Fact]
    public void Forgets_readings_from_the_previous_window()
    {
        var forecaster = new LimitForecaster();
        forecaster.Record(AccountId, Snapshot(Start, 40));
        var nextWindow = Snapshot(Start.AddMinutes(10), 50) with
        {
            Limits = [Session(50) with { ResetsAt = ResetsAt.AddHours(5) }],
        };
        forecaster.Record(AccountId, nextWindow);

        Assert.Null(forecaster.FullAt(AccountId, nextWindow.Limits[0], Start.AddMinutes(10)));
    }

    [Fact]
    public void Ignores_last_known_readings()
    {
        var forecaster = new LimitForecaster();
        forecaster.Record(AccountId, Snapshot(Start, 40) with
        {
            Freshness = LimitFreshness.LastKnown,
        });
        forecaster.Record(AccountId, Snapshot(Start.AddMinutes(10), 50));

        Assert.Null(forecaster.FullAt(AccountId, Session(50), Start.AddMinutes(10)));
    }

    private static UsageLimit Session(int percent) =>
        new("session", "session", percent, "normal", ResetsAt, null, IsActive: true);

    private static LimitSnapshot Snapshot(DateTimeOffset at, int percent) =>
        new([Session(percent)], null, at, LimitFreshness.Live);
}
