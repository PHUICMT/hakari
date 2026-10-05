using Hakari.Core.Configuration;
using Hakari.Core.Currency;
using Hakari.Core.Indexing;
using Hakari.Core.Performance;
using Hakari.Core.Presentation.Widget;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Hakari.Core.Sources;
using Hakari.Core.Watching;
using Hakari.Taskbar.Rendering;

namespace Hakari.Feed;

/// <summary>
/// Indexes on one background thread at background priority, polls limits, and publishes what
/// the widget should say. Only this thread touches the database.
/// </summary>
internal sealed class UsageFeed : IDisposable
{
    /// <summary>Time-based values (burn rate, a new day) refresh even without new usage.</summary>
    private static readonly TimeSpan ContentRefreshInterval = TimeSpan.FromSeconds(30);

    /// <summary>How often to look for newly started WSL distributions and new sign-ins.</summary>
    private static readonly TimeSpan SourceCheckInterval = TimeSpan.FromMinutes(1);

    private readonly SettingsStore settingsStore;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Thread worker;
    private readonly AutoResetEvent settingsSignal = new(initialState: false);
    private volatile bool settingsChanged;
    private volatile bool layoutChanged;
    private volatile HakariSettings presentation = new();
    private ChangeTracker? changeTracker;

    public UsageFeed(SettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
        worker = new Thread(RunLoop) { IsBackground = true, Name = "Hakari usage feed" };
    }

    public event Action<WidgetContent>? Updated;

    public FeedDiagnostics Diagnostics { get; } = new();

    public void Start() => worker.Start();

    /// <summary>Thread-safe: re-reads sources, currency and pause on the next pass.</summary>
    public void ReloadSettings()
    {
        settingsChanged = true;
        settingsSignal.Set();
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
        var pricing = PricingTable.LoadBundled();

        while (!cancellation.IsCancellationRequested)
        {
            settingsChanged = false;
            var settings = settingsStore.Load();
            presentation = settings;
            if (settings.Paused)
            {
                Updated?.Invoke(WidgetText.Paused);
                WaitForSettingsChange();
                continue;
            }

            RunUntilSettingsChange(store, pricing, settings);
        }
    }

    private void RunUntilSettingsChange(
        IndexStore store,
        PricingTable pricing,
        HakariSettings settings)
    {
        var discoveryOptions = new SourceDiscoveryOptions(
            settings.WslMode,
            settings.ExtraConfigDirectories);
        var sources = SourceDiscovery.Discover(discoveryOptions);
        var query = new UsageQuery(store, pricing, CreateConverter(store, pricing, settings));
        var indexer = new Indexer(store);
        using var tracker = new ChangeTracker(
            sources,
            ChangeTrackerOptions.Default,
            TimeProvider.System);
        using var limits = new LimitPoller(store, sources, settings.RefreshSignInAutomatically);
        changeTracker = tracker;
        var lastPublished = DateTimeOffset.MinValue;
        WidgetFacts? facts = null;
        WidgetContent? lastPosted = null;
        var nextSourceCheck = DateTimeOffset.UtcNow + SourceCheckInterval;

        while (!cancellation.IsCancellationRequested && !settingsChanged)
        {
            var now = DateTimeOffset.UtcNow;
            if (now >= nextSourceCheck)
            {
                nextSourceCheck = now + SourceCheckInterval;
                if (SourcesChanged(sources, discoveryOptions))
                {
                    // Start over with the new sources, like a settings change.
                    break;
                }
            }

            var recordsChanged = IndexPendingWork(indexer, tracker);
            if (recordsChanged)
            {
                limits.NoteUsage(now);
            }

            var limitsChanged = limits.PollIfDue(now);
            Diagnostics.LimitPolls = limits.Polls;
            var due = now - lastPublished >= ContentRefreshInterval;
            if (facts is null || recordsChanged || limitsChanged || layoutChanged || due)
            {
                layoutChanged = false;
                facts = WidgetText.Facts(query, limits, presentation);
                lastPublished = now;
            }

            // Posting redraws every widget, so only a real change is posted.
            var content = WidgetText.Content(facts, presentation);
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

    /// <summary>Wakes for the next account's turn only while there are turns to take.</summary>
    private TimeSpan RefreshInterval(WidgetFacts facts) =>
        presentation.AccountsMode == MultiAccountMode.TakeTurns && facts.Accounts.Count > 1
            ? presentation.TurnLength
            : ContentRefreshInterval;

    private void WaitForSettingsChange()
    {
        while (!settingsChanged && !cancellation.IsCancellationRequested)
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
