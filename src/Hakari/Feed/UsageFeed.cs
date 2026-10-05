using Hakari.Core.Configuration;
using Hakari.Core.Currency;
using Hakari.Core.Indexing;
using Hakari.Core.Performance;
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

    private readonly SettingsStore settingsStore;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Thread worker;
    private readonly AutoResetEvent settingsSignal = new(initialState: false);
    private volatile bool settingsChanged;
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

        while (!cancellation.IsCancellationRequested && !settingsChanged)
        {
            var now = DateTimeOffset.UtcNow;
            var recordsChanged = IndexPendingWork(indexer, tracker);
            if (recordsChanged)
            {
                limits.NoteUsage(now);
            }

            var limitsChanged = limits.PollIfDue(now);
            Diagnostics.LimitPolls = limits.Polls;
            if (recordsChanged || limitsChanged || now - lastPublished >= ContentRefreshInterval)
            {
                Updated?.Invoke(WidgetText.Build(query, limits));
                lastPublished = now;
            }

            var untilScan = tracker.TimeUntilNextScan();
            var wait = untilScan < ContentRefreshInterval ? untilScan : ContentRefreshInterval;
            tracker.WaitForWork(wait, cancellation.Token);
        }

        changeTracker = null;
    }

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
