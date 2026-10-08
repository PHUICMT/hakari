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

    [Fact]
    public void Tells_subagent_responses_from_the_main_thread()
    {
        AppendLines(
            SampleLogLines.Assistant(messageId: "msg_main", requestId: "req_main"),
            SampleLogLines.Assistant(
                messageId: "msg_sub", requestId: "req_sub", isSidechain: true));
        Index();
        var query = new UsageQuery(store, PricingTable.LoadBundled());

        var subagents = query.Total(new UsageFilter(IsSidechain: true));
        var mainThread = query.Total(new UsageFilter(IsSidechain: false));

        Assert.Equal(1, subagents.Messages);
        Assert.Equal(1, mainThread.Messages);
    }

    [Fact]
    public void Counts_what_the_cache_saved_against_sending_the_tokens_as_input()
    {
        AppendLines(SampleLogLines.Assistant());
        Index();
        var pricing = PricingTable.LoadBundled();
        var price = pricing.Find("claude-opus-5", new DateOnly(2026, 10, 5))!;
        var expected = 32048m * (price.Input - price.CacheRead) / 1_000_000m;

        var saved = new UsageQuery(store, pricing).CacheSavings(UsageFilter.Everything);

        Assert.Equal(expected, saved);
        Assert.True(saved > 0);
    }

    [Fact]
    public void Keeps_session_titles_only_when_asked_to()
    {
        var title = """{"type":"ai-title","aiTitle":"Fix the sync bug","sessionId":"session_1"}""";
        var custom = """{"type":"custom-title","customTitle":"Meter","sessionId":"session_1"}""";
        AppendLines(SampleLogLines.Assistant(), title, custom);

        new Indexer(store).Index([source]);
        Assert.Empty(SessionTitles.Load(store));

        store.DeleteAll();
        new Indexer(store, collectSessionTitles: true).Index([source]);

        Assert.Equal("Meter", SessionTitles.Load(store)["session_1"]);
    }

    [Fact]
    public void Reports_progress_for_a_large_scan_ending_at_the_whole()
    {
        AppendLines(SampleLogLines.Assistant(messageId: "a"), SampleLogLines.Assistant("b", "r2"));
        var reports = new List<IndexProgress>();
        var indexer = new Indexer(store, progressFromBytes: 1);
        indexer.Progressed += reports.Add;

        indexer.Index([source]);

        var last = Assert.Single(reports);
        Assert.Equal(1.0, last.Fraction);
        Assert.True(last.BytesTotal > 0);
    }

    [Fact]
    public void Reports_no_progress_for_a_small_scan()
    {
        AppendLines(SampleLogLines.Assistant());
        var reports = new List<IndexProgress>();
        var indexer = new Indexer(store);
        indexer.Progressed += reports.Add;

        indexer.Index([source]);

        Assert.Empty(reports);
    }

    [Fact]
    public void Groups_responses_by_local_weekday_and_hour()
    {
        AppendLines(SampleLogLines.Assistant());
        Index();
        var query = new UsageQuery(store, PricingTable.LoadBundled());

        var cells = query.Summarize(UsageFilter.Everything, GroupBy.WeekdayHour);

        var local = DateTimeOffset.Parse("2026-10-05T06:45:53.637Z").ToLocalTime();
        var expected = $"{(int)local.DayOfWeek} {local.Hour:00}";
        Assert.Equal(expected, Assert.Single(cells).Key);
    }

    [Fact]
    public void Marks_the_log_format_changed_when_most_responses_cannot_be_read()
    {
        var unread = """{"type": "assistant", "message": {"usage": {"tokens": 5}}}""";
        AppendLines([.. Enumerable.Repeat(unread, (int)LogFormatWatch.SampleSize)]);

        Index();

        Assert.True(LogFormatWatch.SeemsChanged(store));
    }

    [Fact]
    public void Clears_the_mark_once_a_later_sample_reads_fine()
    {
        var unread = """{"type": "assistant", "message": {"usage": {"tokens": 5}}}""";
        AppendLines([.. Enumerable.Repeat(unread, (int)LogFormatWatch.SampleSize)]);
        Index();

        AppendLines([.. Enumerable.Range(0, (int)LogFormatWatch.SampleSize)
            .Select(index => SampleLogLines.Assistant(messageId: $"msg_{index}"))]);
        Index();

        Assert.False(LogFormatWatch.SeemsChanged(store));
    }

    [Fact]
    public void Waits_for_a_full_sample_before_judging_the_format()
    {
        AppendLines("""{"type": "assistant", "message": {"usage": {}}}""");

        Index();

        Assert.False(LogFormatWatch.SeemsChanged(store));
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
