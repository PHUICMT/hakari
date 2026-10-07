using Hakari.Core.Configuration;
using Hakari.Core.Currency;
using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Performance;
using Hakari.Core.Presentation;
using Hakari.Core.Presentation.Widget;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Hakari.Core.Sources;
using Hakari.Core.Watching;
using Hakari.Taskbar.Rendering;
using Hakari.Taskbar.Tray;

namespace Hakari.Feed;

/// <summary>
/// Indexes on one background thread at background priority, polls limits, and publishes what
/// the widget should say. Only this thread touches the database.
/// </summary>
internal sealed class UsageFeed : IDisposable
{
    /// <summary>Time-based values (burn rate, a new day) refresh even without new usage.</summary>
    private static readonly TimeSpan ContentRefreshInterval = TimeSpan.FromSeconds(30);

    /// <summary>How often spending is held against the budgets and usual sessions.</summary>
    private static readonly TimeSpan SpendCheckInterval = TimeSpan.FromMinutes(1);

    /// <summary>How often to look for newly started WSL distributions and new sign-ins.</summary>
    private static readonly TimeSpan SourceCheckInterval = TimeSpan.FromMinutes(1);

    private readonly SettingsStore settingsStore;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Thread worker;
    private readonly AutoResetEvent settingsSignal = new(initialState: false);
    private volatile bool settingsChanged;
    private volatile bool layoutChanged;
    private volatile bool refreshRequested;
    private volatile HakariSettings presentation = new();
    private ChangeTracker? changeTracker;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);
    private long lastProgressShown;
    private LimitAlertEngine? alertEngine;
    private DateTime pricesVersion;
    private (int Warn, int Critical) alertLevels;

    public UsageFeed(SettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
        worker = new Thread(RunLoop) { IsBackground = true, Name = "Hakari usage feed" };
    }

    public event Action<WidgetContent>? Updated;

    /// <summary>The limit for the tray icon, raised with each new set of facts.</summary>
    public event Action<TrayBadge?>? BadgeUpdated;

    /// <summary>Today's spending and its currency, with each new set of facts.</summary>
    public event Action<decimal, string>? CostUpdated;

    /// <summary>A limit passed a level or reset.</summary>
    public event Action<LimitAlertMessage>? AlertRaised;

    public FeedDiagnostics Diagnostics { get; } = new();

    public void Start() => worker.Start();

    /// <summary>Thread-safe: re-reads sources, currency and pause on the next pass.</summary>
    public void ReloadSettings()
    {
        settingsChanged = true;
        settingsSignal.Set();
        changeTracker?.Wake();
    }

    /// <summary>Thread-safe: asks for limits now instead of at the next scheduled poll.</summary>
    public void RefreshNow()
    {
        refreshRequested = true;
        changeTracker?.Wake();
    }

    private volatile int turnShift;

    /// <summary>
    /// Thread-safe: the wheel over the widget moves the turn to the next or the previous
    /// account at once, while accounts take turns.
    /// </summary>
    public void ShiftTurn(int by)
    {
        if (presentation.AccountsMode != MultiAccountMode.TakeTurns)
        {
            return;
        }

        turnShift += by;
        layoutChanged = true;
        changeTracker?.Wake();
    }

    /// <summary>Thread-safe: redraws for a new layout, names or language.</summary>
    public void SetPresentation(HakariSettings settings)
    {
        presentation = settings;
        layoutChanged = true;
        changeTracker?.Wake();
    }

    public void Dispose()
    {
        cancellation.Cancel();
        settingsSignal.Set();
        changeTracker?.Wake();
    }

    private void RunLoop()
    {
        using var backgroundMode = BackgroundThreadMode.Enter();
        using var store = new IndexStore(HakariPaths.DefaultIndexPath);

        while (!cancellation.IsCancellationRequested)
        {
            settingsChanged = false;

            // Read each pass: "check now" or the user's own prices may have changed it.
            var pricing = PricingSources.LoadCurrent();
            pricesVersion = PricingSources.Version();
            var settings = settingsStore.Load();
            presentation = settings;
            if (settings.IsPausedAt(DateTimeOffset.UtcNow))
            {
                Updated?.Invoke(WidgetText.Paused(settings));
                WaitForSettingsChange(settings.Paused ? null : settings.PausedUntil);
                continue;
            }

            try
            {
                RunUntilSettingsChange(store, pricing, settings);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The widget says something is wrong rather than showing old numbers as new,
                // and the feed starts over after a pause.
                ErrorLog.Write(exception);
                Updated?.Invoke(WidgetText.Error());
                settingsSignal.WaitOne(ErrorRetryDelay);
            }
        }
    }

    private static readonly TimeSpan ErrorRetryDelay = TimeSpan.FromSeconds(30);

    private void RunUntilSettingsChange(
        IndexStore store,
        PricingTable pricing,
        HakariSettings settings)
    {
        var discoveryOptions = new SourceDiscoveryOptions(
            settings.WslMode,
            settings.ExtraConfigDirectories);
        var sources = SourceDiscovery.Discover(discoveryOptions);
        CurrentSources.Save(store, sources);
        var converter = CreateConverter(store, pricing, settings);
        var query = new UsageQuery(store, pricing, converter);
        Func<decimal, decimal> fromDollars = dollars => converter is null
            ? dollars
            : converter.Convert(dollars, DateOnly.FromDateTime(DateTime.Now));
        SyncSessionTitles(store, settings.ShowSessionTitles);
        var indexer = new Indexer(store, settings.ShowSessionTitles);
        indexer.Progressed += ShowIndexProgress;
        using var tracker = new ChangeTracker(
            sources,
            ChangeTrackerOptions.Default,
            TimeProvider.System);
        using var limits = new LimitPoller(
            store,
            sources,
            settings.RefreshSignInAutomatically,
            (accountId, readBefore) => presentation.MayReadLimits(accountId, readBefore));
        changeTracker = tracker;
        var lastPublished = DateTimeOffset.MinValue;
        WidgetFacts? facts = null;
        WidgetContent? lastPosted = null;
        var nextSourceCheck = DateTimeOffset.UtcNow + SourceCheckInterval;
        var nextSpendCheck = DateTimeOffset.UtcNow + SpendCheckInterval;
        var spendWatch = new SpendWatch(store);

        while (!cancellation.IsCancellationRequested && !settingsChanged)
        {
            var now = DateTimeOffset.UtcNow;
            if (now >= nextSourceCheck)
            {
                nextSourceCheck = now + SourceCheckInterval;
                if (SourcesChanged(sources, discoveryOptions)
                    || PricingSources.Version() != pricesVersion)
                {
                    // Start over with the new sources or prices, like a settings change.
                    break;
                }
            }

            var recordsChanged = IndexPendingWork(indexer, tracker);
            if (recordsChanged)
            {
                limits.NoteUsage(now);
            }

            limits.PollSoonIfSignInChanged();
            if (refreshRequested)
            {
                refreshRequested = false;
                limits.PollSoon();
            }

            if (now >= nextSpendCheck)
            {
                nextSpendCheck = now + SpendCheckInterval;
                RaiseSpendAlerts(spendWatch, query, now);
            }

            var limitsChanged = limits.PollIfDue(now);
            Diagnostics.LimitPolls = limits.Polls;
            if (limitsChanged)
            {
                RaiseAlerts(limits, now);
            }

            var due = now - lastPublished >= ContentRefreshInterval;
            if (facts is null || recordsChanged || limitsChanged || layoutChanged || due)
            {
                layoutChanged = false;
                facts = WidgetText.Facts(query, limits, presentation, fromDollars);
                BadgeUpdated?.Invoke(WidgetText.Badge(facts));
                CostUpdated?.Invoke(facts.CostToday, facts.Currency);
                lastPublished = now;
            }

            // Posting redraws every widget, so only a real change is posted.
            var content = WidgetText.Content(facts, presentation, turnShift);
            if (content != lastPosted)
            {
                Updated?.Invoke(content);
                lastPosted = content;
            }

            var untilScan = tracker.TimeUntilNextScan();
            var refresh = RefreshInterval(facts);
            var wait = untilScan < refresh ? untilScan : refresh;
            tracker.WaitForWork(wait, cancellation.Token);
        }

        changeTracker = null;
    }

    /// <summary>
    /// Titles are only kept while the user wants them. Turning them on or off changes what the
    /// index holds, and the logs are read again from the start so it holds the right thing.
    /// </summary>
    private static void SyncSessionTitles(IndexStore store, bool wanted)
    {
        const string Option = "sessionTitles";
        var kept = store.GetOption(Option) == bool.TrueString;
        if (kept == wanted)
        {
            return;
        }

        store.DeleteAll();
        store.SetOption(Option, wanted.ToString());
    }

    /// <summary>
    /// Tells of limits that passed their warning or critical level, or reset, by the levels
    /// set in the layout. Only fresh readings count, and a hidden account stays quiet.
    /// </summary>
    private void RaiseAlerts(LimitPoller limits, DateTimeOffset now)
    {
        var settings = presentation;
        if (!settings.NotifyOnLimits)
        {
            return;
        }

        var accounts = limits.Accounts;

        var levels = (settings.Widget.WarnAt, settings.Widget.CriticalAt);
        if (alertEngine is null || alertLevels != levels)
        {
            alertEngine = new LimitAlertEngine(levels.WarnAt, levels.CriticalAt);
            alertLevels = levels;
        }

        var shown = accounts
            .Where(account => !settings.HiddenAccounts.Contains(account.AccountId))
            .ToList();
        foreach (var account in shown)
        {
            if (account.Snapshot.Freshness == LimitFreshness.LastKnown)
            {
                continue;
            }

            var name = shown.Count > 1 ? NameOf(account, settings) : string.Empty;
            foreach (var limit in account.Snapshot.ProjectedTo(now).Limits)
            {
                foreach (var alert in alertEngine.Observe(
                    account.AccountId, limit, account.PercentOf(limit)))
                {
                    // The engine keeps watching while muted, so nothing piles up for later.
                    if (settings.AlertsMutedAt(now)
                        || (alert.Kind == LimitAlertKind.Reset && !settings.NotifyOnResets))
                    {
                        continue;
                    }

                    AlertRaised?.Invoke(LimitAlertText.Compose(
                        alert,
                        name,
                        now,
                        limits.FullAt(account.AccountId, limit, now),
                        RoomElsewhere(shown, account, limit, settings, now)));
                }
            }
        }
    }

    /// <summary>Budgets passed and costly sessions; quiet while alerts are muted.</summary>
    private void RaiseSpendAlerts(SpendWatch watch, UsageQuery query, DateTimeOffset now)
    {
        var settings = presentation;
        if (settings.AlertsMutedAt(now)
            || (settings.DailyBudget is null && settings.MonthlyBudget is null
                && !settings.NotifyOnUnusualSessions))
        {
            return;
        }

        foreach (var alert in watch.Check(
            query,
            settings.DailyBudget,
            settings.MonthlyBudget,
            settings.NotifyOnUnusualSessions,
            now.ToLocalTime()))
        {
            AlertRaised?.Invoke(SpendAlertText.Compose(alert, query.Currency));
        }
    }

    private static string NameOf(WidgetAccount account, HakariSettings settings) =>
        AccountLabels.Full(account.Account, settings.NicknameOf(account.AccountId));

    /// <summary>The other account with the most room left on the same limit, if any.</summary>
    private static (string Name, double Percent)? RoomElsewhere(
        IReadOnlyList<WidgetAccount> shown,
        WidgetAccount account,
        UsageLimit limit,
        HakariSettings settings,
        DateTimeOffset now)
    {
        const double FullPercent = 100;
        var best = shown
            .Where(other => other.AccountId != account.AccountId)
            .Select(other => (Account: other, Limit: other.Snapshot.ProjectedTo(now).Limits
                .FirstOrDefault(candidate => candidate.Kind == limit.Kind
                    && candidate.ScopeName == limit.ScopeName)))
            .Where(entry => entry.Limit is not null)
            .Select(entry => (entry.Account, Percent: entry.Account.PercentOf(entry.Limit!)))
            .Where(entry => entry.Percent < FullPercent)
            .OrderBy(entry => entry.Percent)
            .FirstOrDefault();
        return best.Account is null ? null : (NameOf(best.Account, settings), best.Percent);
    }

    /// <summary>
    /// Catches a WSL distribution started, or a config folder signed in, after Hakari began.
    /// Never under WslScanMode.All: looking inside a stopped distribution would start it.
    /// </summary>
    private static bool SourcesChanged(
        IReadOnlyList<UsageSource> sources,
        SourceDiscoveryOptions options)
    {
        if (options.WslMode == WslScanMode.All)
        {
            return false;
        }

        var current = SourceDiscovery.Discover(options).Select(source => source.Id).Order();
        return !current.SequenceEqual(sources.Select(source => source.Id).Order());
    }

    /// <summary>
    /// Wakes for the next account's turn or the next cycled slot, only while there is one to
    /// show; otherwise at the usual pace.
    /// </summary>
    private TimeSpan RefreshInterval(WidgetFacts facts)
    {
        var interval = ContentRefreshInterval;
        if (presentation.AccountsMode == MultiAccountMode.TakeTurns && facts.Accounts.Count > 1)
        {
            interval = TimeSpan.FromTicks(Math.Min(interval.Ticks, presentation.TurnLength.Ticks));
        }

        var layout = presentation.Widget;
        if (layout.CycleSeconds > 0 && layout.Slots.Count > 1)
        {
            var cycle = TimeSpan.FromSeconds(layout.CycleSeconds);
            interval = TimeSpan.FromTicks(Math.Min(interval.Ticks, cycle.Ticks));
        }

        return interval;
    }

    /// <param name="resumeAt">When a timed pause ends, or null to wait for a change.</param>
    private void WaitForSettingsChange(DateTimeOffset? resumeAt)
    {
        while (!settingsChanged && !cancellation.IsCancellationRequested
            && (resumeAt is null || DateTimeOffset.UtcNow < resumeAt))
        {
            settingsSignal.WaitOne(ContentRefreshInterval);
        }
    }

    private static CurrencyConverter? CreateConverter(
        IndexStore store,
        PricingTable pricing,
        HakariSettings settings)
    {
        if (settings.Currency == CurrencyCodes.Dollar)
        {
            return null;
        }

        var service = new ExchangeRateService(
            new ExchangeRateRepository(store),
            ExchangeRateService.DefaultProviders,
            TimeProvider.System);
        var firstUsageDay = new UsageQuery(store, pricing).FirstUsageDay()
            ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return service
            .CreateConverterAsync(settings.Currency, settings.RateMode, firstUsageDay)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// A first run or a rebuild reads gigabytes: the widget says how far it has got, a few
    /// times a second at most, instead of showing zeros.
    /// </summary>
    private void ShowIndexProgress(IndexProgress progress)
    {
        var now = Environment.TickCount64;
        if (now - lastProgressShown < ProgressInterval.TotalMilliseconds
            && progress.Fraction < 1)
        {
            return;
        }

        lastProgressShown = now;
        Updated?.Invoke(WidgetText.Indexing(progress));
    }

    private bool IndexPendingWork(Indexer indexer, ChangeTracker tracker)
    {
        var work = tracker.TakeWork();
        if (work.IsEmpty)
        {
            return false;
        }

        var statistics = indexer.Index(work, cancellation.Token);
        Diagnostics.LastIndexDuration = statistics.Elapsed;
        Diagnostics.IndexPasses++;
        return statistics.RecordsChanged > 0;
    }
}
