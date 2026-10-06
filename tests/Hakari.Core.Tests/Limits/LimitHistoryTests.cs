using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Tests.Support;
using Microsoft.Data.Sqlite;

namespace Hakari.Core.Tests.Limits;

public sealed class LimitHistoryTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    private readonly TemporaryDirectory directory = new();
    private readonly IndexStore store;
    private readonly LimitHistory history;

    public LimitHistoryTests()
    {
        store = new IndexStore(directory.Combine("index.db"));
        history = new LimitHistory(store);
    }

    [Fact]
    public void Keeps_a_reading_when_the_percent_changes()
    {
        history.Record("a", Snapshot(40, Start));
        history.Record("a", Snapshot(41, Start.AddMinutes(2)));

        Assert.Equal(2, Load("session").Count);
    }

    [Fact]
    public void Skips_an_unchanged_reading_until_an_hour_has_passed()
    {
        history.Record("a", Snapshot(40, Start));
        history.Record("a", Snapshot(40, Start.AddMinutes(10)));
        history.Record("a", Snapshot(40, Start.AddMinutes(70)));

        Assert.Equal(2, Load("session").Count);
    }

    [Fact]
    public void Ignores_readings_that_are_not_live()
    {
        history.Record("a", Snapshot(40, Start) with { Freshness = LimitFreshness.LastKnown });

        Assert.Empty(Load("session"));
    }

    [Fact]
    public void Prunes_readings_older_than_ninety_days()
    {
        history.Record("a", Snapshot(40, Start.AddDays(-100)));
        history.Record("a", Snapshot(50, Start));

        history.Prune(Start);

        Assert.Equal(50, Assert.Single(Load("session", Start.AddDays(-200))).Percent);
    }

    public void Dispose()
    {
        store.Dispose();
        SqliteConnection.ClearAllPools();
        directory.Dispose();
    }

    private IReadOnlyList<LimitReading> Load(string kind, DateTimeOffset? since = null) =>
        history.Load("a", kind, since ?? Start.AddDays(-1));

    private static LimitSnapshot Snapshot(int percent, DateTimeOffset at) => new(
        [new UsageLimit("session", "session", percent, "normal", at.AddHours(3), null, true)],
        null,
        at,
        LimitFreshness.Live);
}
