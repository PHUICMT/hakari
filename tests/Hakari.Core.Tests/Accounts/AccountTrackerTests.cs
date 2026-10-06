using System.Text.Json.Nodes;
using Hakari.Core.Accounts;
using Hakari.Core.Indexing;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;
using Hakari.Core.Sources;
using Hakari.Core.Tests.Support;
using Microsoft.Data.Sqlite;

namespace Hakari.Core.Tests.Accounts;

public sealed class AccountTrackerTests : IDisposable
{
    private static readonly DateTimeOffset SwitchTime = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly TemporaryDirectory directory = new();
    private readonly IndexStore store;
    private readonly UsageSource source;
    private readonly string accountFile;
    private readonly MovableTime time = new(SwitchTime);

    public AccountTrackerTests()
    {
        store = new IndexStore(directory.Combine("index.db"));
        var configDirectory = directory.Combine("config");
        Directory.CreateDirectory(Path.Combine(configDirectory, "projects", "project_a"));
        accountFile = Path.Combine(configDirectory, ".claude.json");
        source = new UsageSource("test", SourceKind.ConfigDirectory, "Test", configDirectory);
    }

    [Fact]
    public void Counts_the_first_account_only_from_when_it_was_seen()
    {
        WriteAccount("account-a", "a@example.com");

        var started = CreateTracker().Observe([source]);

        Assert.Equal(SwitchTime, Assert.Single(started).StartedAt);
    }

    [Fact]
    public void Gives_usage_from_before_the_first_sighting_to_no_account()
    {
        WriteAccount("account-a", "a@example.com");
        CreateTracker().Observe([source]);
        WriteLog(
            SampleLogLines.Assistant(messageId: "before", timestamp: "2026-10-05T11:00:00Z"),
            SampleLogLines.Assistant(messageId: "after", timestamp: "2026-10-05T13:00:00Z"));
        new Indexer(store).Index([source]);

        var messages = new UsageQuery(store, PricingTable.LoadBundled())
            .Summarize(UsageFilter.Everything, GroupBy.Account)
            .ToDictionary(summary => summary.Key, summary => summary.Messages);

        Assert.Equal(1, messages[string.Empty]);
        Assert.Equal(1, messages["account-a"]);
    }

    [Fact]
    public void Moves_an_old_opening_period_to_the_install_time_but_not_past_the_next()
    {
        var repository = new AccountRepository(store);
        var installed = SwitchTime.AddDays(-1);
        repository.AddPeriod(new AccountPeriod("test", "account-a", DateTimeOffset.UnixEpoch));
        repository.AddPeriod(new AccountPeriod("other", "account-a", DateTimeOffset.UnixEpoch));
        repository.AddPeriod(new AccountPeriod("other", "account-b", installed.AddHours(-2)));

        repository.StartOpeningPeriodsAt(installed);

        Assert.Equal(installed, repository.LatestPeriod("test")!.StartedAt);
        Assert.Equal(installed.AddHours(-2), repository.LatestPeriod("other")!.StartedAt);
    }

    [Fact]
    public void Starts_a_new_period_when_another_account_signs_in()
    {
        var tracker = CreateTracker();
        time.Now = SwitchTime.AddHours(-2);
        WriteAccount("account-a", "a@example.com");
        tracker.Observe([source]);

        time.Now = SwitchTime;
        WriteAccount("account-b", "b@example.com");
        var started = tracker.Observe([source]);

        var period = Assert.Single(started);
        Assert.Equal("account-b", period.AccountId);
        Assert.Equal(SwitchTime, period.StartedAt);
    }

    [Fact]
    public void Ignores_rewrites_that_keep_the_same_account()
    {
        var tracker = CreateTracker();
        WriteAccount("account-a", "a@example.com");
        tracker.Observe([source]);

        WriteAccount("account-a", "a@example.com");

        Assert.Empty(tracker.Observe([source]));
    }

    [Fact]
    public void Splits_usage_between_accounts_at_the_switch()
    {
        var tracker = CreateTracker();
        time.Now = SwitchTime.AddHours(-2);
        WriteAccount("account-a", "a@example.com");
        tracker.Observe([source]);
        time.Now = SwitchTime;
        WriteAccount("account-b", "b@example.com");
        tracker.Observe([source]);
        WriteLog(
            SampleLogLines.Assistant(messageId: "before", timestamp: "2026-10-05T11:00:00Z"),
            SampleLogLines.Assistant(messageId: "after", timestamp: "2026-10-05T13:00:00Z"));
        new Indexer(store).Index([source]);

        var summaries = new UsageQuery(store, PricingTable.LoadBundled())
            .Summarize(UsageFilter.Everything, GroupBy.Account)
            .ToDictionary(summary => summary.Key, summary => summary.Messages);

        Assert.Equal(1, summaries["account-a"]);
        Assert.Equal(1, summaries["account-b"]);
    }

    public void Dispose()
    {
        store.Dispose();
        SqliteConnection.ClearAllPools();
        directory.Dispose();
    }

    private AccountTracker CreateTracker() => new(new AccountRepository(store), time);

    /// <summary>Each write gets a new modified time, as a real sign-in would.</summary>
    private void WriteAccount(string accountId, string email)
    {
        var account = new JsonObject
        {
            ["accountUuid"] = accountId,
            ["emailAddress"] = email,
            ["organizationType"] = "claude_max",
            ["userRateLimitTier"] = "default_claude_max_5x",
        };
        var root = new JsonObject { ["oauthAccount"] = account };
        File.WriteAllText(accountFile, root.ToJsonString());
        File.SetLastWriteTimeUtc(accountFile, DateTime.UtcNow.AddSeconds(writeCount++));
    }

    private int writeCount;

    private void WriteLog(params string[] lines) => File.WriteAllLines(
        Path.Combine(source.ProjectsDirectory, "project_a", "session.jsonl"),
        lines);

    private sealed class MovableTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
