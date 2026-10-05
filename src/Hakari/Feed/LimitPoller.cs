using Hakari.Core.Accounts;
using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Presentation.Widget;
using Hakari.Core.Sources;

namespace Hakari.Feed;

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

    /// <summary>An account unseen for longer than this is no longer shown.</summary>
    private static readonly TimeSpan RememberFor = TimeSpan.FromDays(7);

    private readonly IReadOnlyList<UsageSource> sources;
    private readonly HttpClient httpClient = new() { Timeout = RequestTimeout };
    private readonly AccountTracker accountTracker;
    private readonly LimitService limitService;
    private readonly AccountRepository accountRepository;
    private readonly LimitForecaster forecaster = new();
    private readonly LimitCache limitCache;
    private DateTimeOffset nextPoll = DateTimeOffset.MinValue;
    private DateTimeOffset lastUsage = DateTimeOffset.MinValue;

    public LimitPoller(
        IndexStore store,
        IReadOnlyList<UsageSource> sources,
        bool refreshSignInAutomatically)
    {
        this.sources = sources;
        accountRepository = new AccountRepository(store);
        limitCache = new LimitCache(store);
        accountTracker = new AccountTracker(accountRepository, TimeProvider.System);
        limitService = new LimitService(
            new UsageLimitClient(httpClient, TimeProvider.System),
            new LimitCache(store),
            new ClaudeCodeActivity(),
            new LimitServiceOptions(refreshSignInAutomatically),
            TimeProvider.System);
    }

    /// <summary>Every signed-in account with known limits, the most pressing first.</summary>
    public IReadOnlyList<WidgetAccount> Accounts { get; private set; } = [];

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
        var details = accountRepository.ListAccounts()
            .ToDictionary(account => account.AccountId);
        var live = accountTracker.SourcesByCurrentAccount(sources)
            .Select(account => Read(account.Key, account.Value, details))
            .OfType<WidgetAccount>()
            .ToList();
        Accounts =
        [
            .. live.Concat(Remembered(live, details.Values, now))
                .OrderByDescending(account => LimitPriority.Rank(account.Snapshot)),
        ];

        var active = now - lastUsage < ActiveWindow;
        nextPoll = now + (active ? ActiveInterval : IdleInterval);
        Polls++;
        return true;
    }

    /// <summary>When the recent pace fills the limit before it resets, else null.</summary>
    public DateTimeOffset? FullAt(string accountId, UsageLimit limit, DateTimeOffset now) =>
        forecaster.FullAt(accountId, limit, now);

    public void Dispose() => httpClient.Dispose();

    /// <summary>
    /// Accounts seen recently whose source is not here now, such as one signed in inside a
    /// WSL distribution that has since stopped: their last limits, marked as last known.
    /// </summary>
    private IEnumerable<WidgetAccount> Remembered(
        IReadOnlyList<WidgetAccount> live,
        IEnumerable<AccountInfo> known,
        DateTimeOffset now) =>
        known
            .Where(account => live.All(current => current.AccountId != account.AccountId))
            .Select(account => limitCache.Load(account.AccountId) is { } snapshot
                && now - snapshot.FetchedAt < RememberFor
                    ? new WidgetAccount(
                        account.AccountId,
                        account,
                        snapshot with { Freshness = LimitFreshness.LastKnown })
                    : null)
            .OfType<WidgetAccount>();

    private WidgetAccount? Read(
        string accountId,
        IReadOnlyList<UsageSource> accountSources,
        IReadOnlyDictionary<string, AccountInfo> details)
    {
        var result = limitService
            .GetForAccountAsync(accountId, accountSources, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        if (result.Snapshot is not { } snapshot)
        {
            return null;
        }

        forecaster.Record(accountId, snapshot);
        return new WidgetAccount(accountId, details.GetValueOrDefault(accountId), snapshot);
    }
}
