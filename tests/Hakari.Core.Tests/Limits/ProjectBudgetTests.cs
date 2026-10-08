using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;
using Hakari.Core.Sources;
using Hakari.Core.Tests.Support;
using Microsoft.Data.Sqlite;

namespace Hakari.Core.Tests.Limits;

public sealed class ProjectBudgetTests : IDisposable
{
    private const string Project = @"D:\work";
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly TemporaryDirectory directory = new();
    private readonly IndexStore store;

    public ProjectBudgetTests()
    {
        store = new IndexStore(directory.Combine("index.db"));
        var source = new UsageSource(
            Id: "test",
            Kind: SourceKind.ConfigDirectory,
            DisplayName: "Test",
            ConfigDirectory: directory.Combine("config"));
        var folder = directory.Combine("config", "projects", "project_a");
        Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder, "session.jsonl"), [SampleLogLines.Assistant()]);
        new Indexer(store).Index([source]);
    }

    [Fact]
    public void Tells_once_a_month_when_a_project_passes_its_budget()
    {
        var watch = new SpendWatch(store);

        var first = Check(watch, 0.0001m);
        var second = Check(watch, 0.0001m);

        var alert = Assert.Single(first);
        Assert.Equal(SpendAlertKind.ProjectBudget, alert.Kind);
        Assert.Equal(Project, alert.Session);
        Assert.Empty(second);
    }

    [Fact]
    public void Stays_quiet_under_the_budget() =>
        Assert.Empty(Check(new SpendWatch(store), 1_000_000m));

    public void Dispose()
    {
        store.Dispose();
        SqliteConnection.ClearAllPools();
        directory.Dispose();
    }

    private IReadOnlyList<SpendAlert> Check(SpendWatch watch, decimal budget) => watch.Check(
        new UsageQuery(store, PricingTable.LoadBundled()),
        dailyBudget: null,
        monthlyBudget: null,
        watchSessions: false,
        Now,
        new Dictionary<string, decimal> { [Project] = budget });
}
