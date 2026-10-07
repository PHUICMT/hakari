using Hakari.Core.Indexing;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;
using Hakari.Core.Sources;
using Hakari.Core.Tests.Support;

namespace Hakari.Core.Tests.Querying;

public sealed class AllTimeCostTests : IDisposable
{
    private readonly TemporaryDirectory directory = new();
    private readonly IndexStore store;
    private readonly UsageSource source;

    public AllTimeCostTests()
    {
        store = new IndexStore(directory.Combine("index.db"));
        var configDirectory = directory.Combine("config");
        Directory.CreateDirectory(Path.Combine(configDirectory, "projects", "project_a"));
        source = new UsageSource("test", SourceKind.ConfigDirectory, "Test", configDirectory);
    }

    [Fact]
    public void Matches_the_full_total_and_follows_a_late_older_record()
    {
        var now = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        WriteReplies(("old", "2026-10-01T09:00:00Z"), ("today", "2026-10-05T09:00:00Z"));
        var query = new UsageQuery(store, PricingTable.LoadBundled());

        Assert.Equal(query.Total(UsageFilter.Everything).Cost, AllTimeCost.Of(query, now));

        WriteReplies(
            ("old", "2026-10-01T09:00:00Z"),
            ("today", "2026-10-05T09:00:00Z"),
            ("late", "2026-10-02T09:00:00Z"));

        Assert.Equal(query.Total(UsageFilter.Everything).Cost, AllTimeCost.Of(query, now));
    }

    private void WriteReplies(params (string Id, string Timestamp)[] replies)
    {
        File.WriteAllLines(
            Path.Combine(source.ProjectsDirectory, "project_a", "session.jsonl"),
            replies.Select(reply => SampleLogLines.Assistant(
                messageId: reply.Id, requestId: reply.Id, timestamp: reply.Timestamp)));
        new Indexer(store).Index([source]);
    }

    public void Dispose()
    {
        store.Dispose();
        directory.Dispose();
    }
}
