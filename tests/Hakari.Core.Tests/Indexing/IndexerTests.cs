using Hakari.Core.Indexing;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;
using Hakari.Core.Sources;
using Hakari.Core.Tests.Support;

namespace Hakari.Core.Tests.Indexing;

public sealed class IndexerTests : IDisposable
{
    private readonly TemporaryDirectory directory = new();
    private readonly IndexStore store;
    private readonly UsageSource source;
    private readonly string logFile;

    public IndexerTests()
    {
        store = new IndexStore(directory.Combine("index.db"));
        source = new UsageSource(
            Id: "test",
            Kind: SourceKind.ConfigDirectory,
            DisplayName: "Test",
            ConfigDirectory: directory.Combine("config"));
        Directory.CreateDirectory(directory.Combine("config", "projects", "project_a"));
        logFile = directory.Combine("config", "projects", "project_a", "session.jsonl");
    }

    [Fact]
    public void Indexes_only_appended_lines_on_the_second_run()
    {
        AppendLines(SampleLogLines.Assistant(messageId: "msg_1"), SampleLogLines.User);
        var firstRun = Index();

        AppendLines(SampleLogLines.Assistant(messageId: "msg_2"));
        var secondRun = Index();

        Assert.Equal(1, firstRun.RecordsChanged);
        Assert.Equal(1, secondRun.RecordsChanged);
        Assert.Equal(2, MessageCount());
    }

    [Fact]
    public void Skips_files_that_did_not_change()
    {
        AppendLines(SampleLogLines.Assistant());
        Index();

        var secondRun = Index();

        Assert.Equal(0, secondRun.FilesChanged);
    }

    [Fact]
    public void Deduplicates_repeated_messages()
    {
        var line = SampleLogLines.Assistant(messageId: "msg_same", requestId: "req_same");
        AppendLines(line, line, line);

        Index();

        Assert.Equal(1, MessageCount());
    }

    [Fact]
    public void Keeps_the_largest_token_counts_for_a_repeated_message()
    {
        var completeLine = SampleLogLines.Assistant(messageId: "msg_same", requestId: "req_same");
        var emptyUsageLine = completeLine.Replace("\"output_tokens\":433", "\"output_tokens\":0");
        AppendLines(emptyUsageLine, completeLine, emptyUsageLine);

        Index();

        Assert.Equal(433, Total().Tokens.Output);
    }

    [Fact]
    public void Waits_for_a_partially_written_line_to_complete()
    {
        var line = SampleLogLines.Assistant();
        File.AppendAllText(logFile, line[..20]);
        var firstRun = Index();

        File.AppendAllText(logFile, line[20..] + "\n");
        var secondRun = Index();

        Assert.Equal(0, firstRun.RecordsChanged);
        Assert.Equal(1, secondRun.RecordsChanged);
    }

    public void Dispose()
    {
        store.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        directory.Dispose();
    }

    private IndexStatistics Index() => new Indexer(store).Index([source]);

    private long MessageCount() => Total().Messages;

    private UsageSummary Total() =>
        new UsageQuery(store, PricingTable.LoadBundled()).Total(UsageFilter.Everything);

    private void AppendLines(params string[] lines) =>
        File.AppendAllLines(logFile, lines);
}
