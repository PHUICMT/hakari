using System.Globalization;
using Hakari.Core.Configuration;
using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Performance;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;
using Hakari.Core.Sources;
using Hakari.Core.Watching;
using Hakari.Taskbar.Rendering;

namespace Hakari.TaskbarSpike;

/// <summary>
/// Indexes on a background thread at background priority and reports today's totals.
/// The widget thread never touches the database.
/// </summary>
internal sealed class UsageFeed : IDisposable
{
    /// <summary>Time-based values (burn rate, a new day) refresh even without new usage.</summary>
    private static readonly TimeSpan ContentRefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private readonly CancellationTokenSource cancellation = new();
    private readonly Thread worker;

    public UsageFeed()
    {
        worker = new Thread(RunLoop) { IsBackground = true, Name = "Hakari usage feed" };
    }

    public event Action<WidgetContent>? Updated;

    public TimeSpan LastIndexDuration { get; private set; }

    public int IndexPasses { get; private set; }

    public int LimitPolls { get; private set; }

    public void Start() => worker.Start();

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void RunLoop()
    {
        using var backgroundMode = BackgroundThreadMode.Enter();
        using var store = new IndexStore(HakariPaths.DefaultIndexPath);
        var sources = SourceDiscovery.Discover(SourceDiscoveryOptions.Default);
        var indexer = new Indexer(store);
        var query = new UsageQuery(store, PricingTable.LoadBundled());
        using var tracker = new ChangeTracker(
            sources,
            ChangeTrackerOptions.Default,
            TimeProvider.System);
        using var limits = new LimitPoller(store, sources);
        var lastPublished = DateTimeOffset.MinValue;

        while (!cancellation.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var recordsChanged = IndexPendingWork(indexer, tracker);
            if (recordsChanged)
            {
                limits.NoteUsage(now);
            }

            var limitsChanged = limits.PollIfDue(now);
            LimitPolls = limits.Polls;
            var refreshDue = now - lastPublished >= ContentRefreshInterval;
            if (recordsChanged || limitsChanged || refreshDue)
            {
                Updated?.Invoke(BuildContent(query, limits.Latest));
                lastPublished = now;
            }

            var untilScan = tracker.TimeUntilNextScan();
            var wait = untilScan < ContentRefreshInterval ? untilScan : ContentRefreshInterval;
            tracker.WaitForWork(wait, cancellation.Token);
        }
    }

    private bool IndexPendingWork(Indexer indexer, ChangeTracker tracker)
    {
        var work = tracker.TakeWork();
        if (work.IsEmpty)
        {
            return false;
        }

        var statistics = indexer.Index(work, cancellation.Token);
        LastIndexDuration = statistics.Elapsed;
        IndexPasses++;
        return statistics.RecordsChanged > 0;
    }

    /// <summary>Money on top; below, the most pressing limit, else the burn rate.</summary>
    private static WidgetContent BuildContent(UsageQuery query, LimitResult? limits)
    {
        var now = DateTimeOffset.Now;
        var today = query.Total(new UsageFilter(From: TimePeriods.StartOfToday(now)));
        var primary = string.Create(Culture, $"${today.Cost:N2} today");

        if (limits is not null && LimitLine.From(limits, now) is var (text, tone))
        {
            return new WidgetContent(primary, text, tone);
        }

        var lastHour = query.Total(new UsageFilter(From: now.AddHours(-1)));
        var month = query.Total(new UsageFilter(From: TimePeriods.StartOfMonth(now)));
        var secondary = string.Create(
            Culture,
            $"${lastHour.Cost:N1}/h · month ${month.Cost:N0}");
        return new WidgetContent(primary, secondary);
    }
}
