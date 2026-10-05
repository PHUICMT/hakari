using Hakari.Core.Accounts;
using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Sources;

namespace Hakari.TaskbarSpike;

/// <summary>
/// Asks for limits often while Claude Code is in use and rarely while idle, so an idle PC
/// makes almost no requests. Runs on the feed thread, which owns the database.
/// </summary>
internal sealed class LimitPoller : IDisposable
{
    private static readonly TimeSpan ActiveInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan IdleInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly IReadOnlyList<UsageSource> sources;
    private readonly HttpClient httpClient = new() { Timeout = RequestTimeout };
    private readonly AccountTracker accountTracker;
    private readonly LimitService limitService;
    private DateTimeOffset nextPoll = DateTimeOffset.MinValue;
    private DateTimeOffset lastUsage = DateTimeOffset.MinValue;

    public LimitPoller(IndexStore store, IReadOnlyList<UsageSource> sources)
    {
        this.sources = sources;
        accountTracker = new AccountTracker(new AccountRepository(store), TimeProvider.System);
        limitService = new LimitService(
            new UsageLimitClient(httpClient, TimeProvider.System),
            new LimitCache(store),
            new ClaudeCodeActivity(),
            LimitServiceOptions.Default,
            TimeProvider.System);
    }

    /// <summary>The most pressing limit across every signed-in account.</summary>
    public LimitResult? Latest { get; private set; }

    public int Polls { get; private set; }

    public void NoteUsage(DateTimeOffset now) => lastUsage = now;

    /// <summary>Returns true when new limits were read.</summary>
    public bool PollIfDue(DateTimeOffset now)
    {
        if (now < nextPoll)
        {
            return false;
        }

        accountTracker.Observe(sources);
        var results = accountTracker.SourcesByCurrentAccount(sources)
            .Select(account => limitService
                .GetForAccountAsync(account.Key, account.Value, CancellationToken.None)
                .GetAwaiter()
                .GetResult())
            .ToList();

        Latest = results
            .Where(result => result.Snapshot is not null)
            .OrderByDescending(result => PressingRank(result.Snapshot!))
            .FirstOrDefault();

        var active = now - lastUsage < ActiveWindow;
        nextPoll = now + (active ? ActiveInterval : IdleInterval);
        Polls++;
        return true;
    }

    public void Dispose() => httpClient.Dispose();

    private static int PressingRank(LimitSnapshot snapshot) =>
        LimitPriority.MostPressing(snapshot) is { } limit
            ? LimitPriority.SeverityRank(limit.Severity) * 1000 + limit.Percent
            : -1;
}
