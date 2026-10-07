using Hakari.Core.Indexing;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;
using Hakari.Core.Sources;
using Hakari.Core.Tests.Support;

namespace Hakari.Core.Tests.Querying;

public sealed class ActiveTimeTests : IDisposable
{
    private readonly TemporaryDirectory directory = new();
    private readonly IndexStore store;
    private readonly UsageSource source;

    public ActiveTimeTests()
    {
        store = new IndexStore(directory.Combine("index.db"));
        var configDirectory = directory.Combine("config");
        Directory.CreateDirectory(Path.Combine(configDirectory, "projects", "project_a"));
        source = new UsageSource("test", SourceKind.ConfigDirectory, "Test", configDirectory);
    }

    [Fact]
    public void Adds_up_close_replies_and_leaves_out_days_away()
    {
        File.WriteAllLines(
            Path.Combine(source.ProjectsDirectory, "project_a", "session.jsonl"),
            [
                Reply("first", "2026-10-01T09:00:00Z"),
                Reply("second", "2026-10-01T09:20:00Z"),
                Reply("third", "2026-10-01T09:30:00Z"),
                Reply("resumed", "2026-10-04T15:00:00Z"),
                Reply("after", "2026-10-04T15:05:00Z"),
            ]);
        new Indexer(store).Index([source]);

        var active = new UsageQuery(store, PricingTable.LoadBundled())
            .ActiveTime(UsageFilter.Everything);

        Assert.Equal(TimeSpan.FromMinutes(35), Assert.Single(active).Value);
    }

    private static string Reply(string id, string timestamp) =>
        SampleLogLines.Assistant(messageId: id, requestId: id, timestamp: timestamp);

    public void Dispose()
    {
        store.Dispose();
        directory.Dispose();
    }
}
