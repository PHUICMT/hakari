using Hakari.Core.Sources;

namespace Hakari.Core.Watching;

/// <summary>
/// Decides what the indexer should look at next, so idle time costs nothing: watched folders
/// report the files that changed, unwatchable ones are polled on a slow timer.
/// </summary>
public sealed class ChangeTracker : IDisposable
{
    private const string NetworkPathPrefix = @"\\";
    private const int WatcherBufferBytes = 64 * 1024;

    private readonly ChangeTrackerOptions options;
    private readonly TimeProvider timeProvider;
    private readonly List<FileSystemWatcher> watchers = [];
    private readonly Dictionary<UsageSource, DateTimeOffset> nextFullScan = [];
    private readonly Dictionary<UsageSource, HashSet<string>> changedFiles = [];
    private readonly AutoResetEvent wakeUp = new(initialState: false);
    private readonly object stateLock = new();
    private DateTimeOffset lastChange = DateTimeOffset.MinValue;

    private readonly Func<UsageSource, bool> canTouch;

    /// <param name="canTouch">Whether a source may be scanned now; a WSL one only while its
    /// distribution runs. Its scan waits for the next turn when it may not.</param>
    public ChangeTracker(
        IEnumerable<UsageSource> sources,
        ChangeTrackerOptions options,
        TimeProvider timeProvider,
        Func<UsageSource, bool>? canTouch = null)
    {
        this.options = options;
        this.timeProvider = timeProvider;
        this.canTouch = canTouch ?? (_ => true);
        var now = timeProvider.GetUtcNow();

        foreach (var source in sources)
        {
            nextFullScan[source] = now;
            if (IsWatchable(source) && Directory.Exists(source.ProjectsDirectory))
            {
                watchers.Add(CreateWatcher(source));
            }
        }
    }

    public static bool IsWatchable(UsageSource source) =>
        !source.ProjectsDirectory.StartsWith(NetworkPathPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Ends a <see cref="WaitForWork"/> early, such as after a settings change. Safe after
    /// <see cref="Dispose"/>: a watcher's event already on its way still lands here.
    /// </summary>
    public void Wake()
    {
        try
        {
            wakeUp.Set();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>Blocks until something may need indexing, or the timeout passes.</summary>
    public void WaitForWork(TimeSpan timeout, CancellationToken cancellationToken) =>
        WaitHandle.WaitAny([wakeUp, cancellationToken.WaitHandle], timeout);

    public PendingWork TakeWork()
    {
        var now = timeProvider.GetUtcNow();
        lock (stateLock)
        {
            var fullScans = TakeDueFullScans(now);
            var settled = now - lastChange >= options.Debounce;
            var changes = settled ? TakeChangedFiles(fullScans) : [];
            return new PendingWork(fullScans, changes);
        }
    }

    /// <summary>Earliest moment new work can appear without a file event.</summary>
    public TimeSpan TimeUntilNextScan()
    {
        var now = timeProvider.GetUtcNow();
        lock (stateLock)
        {
            var earliest = nextFullScan.Values.DefaultIfEmpty(now).Min();
            var pendingChanges = changedFiles.Count > 0 ? options.Debounce : TimeSpan.MaxValue;
            var untilScan = earliest > now ? earliest - now : TimeSpan.Zero;
            return untilScan < pendingChanges ? untilScan : pendingChanges;
        }
    }

    public void Dispose()
    {
        foreach (var watcher in watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        wakeUp.Dispose();
    }

    private List<UsageSource> TakeDueFullScans(DateTimeOffset now)
    {
        var due = nextFullScan
            .Where(entry => entry.Value <= now)
            .Select(entry => entry.Key)
            .ToList();
        foreach (var source in due)
        {
            var interval = IsWatchable(source)
                ? options.LocalSafetyScanInterval
                : options.RemotePollInterval;
            nextFullScan[source] = now + interval;
        }

        return [.. due.Where(canTouch)];
    }

    private Dictionary<UsageSource, IReadOnlyCollection<string>> TakeChangedFiles(
        List<UsageSource> alreadyFullyScanned)
    {
        var taken = changedFiles
            .Where(entry => !alreadyFullyScanned.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => (IReadOnlyCollection<string>)entry.Value);
        changedFiles.Clear();
        return taken;
    }

    private FileSystemWatcher CreateWatcher(UsageSource source)
    {
        var watcher = new FileSystemWatcher(
            source.ProjectsDirectory,
            ClaudeConfigNames.LogFileSearchPattern)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            InternalBufferSize = WatcherBufferBytes,
        };

        watcher.Changed += (_, change) => RecordChange(source, change.FullPath);
        watcher.Created += (_, change) => RecordChange(source, change.FullPath);
        watcher.Renamed += (_, change) => RecordChange(source, change.FullPath);
        watcher.Error += (_, _) => ScheduleFullScan(source);
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private void RecordChange(UsageSource source, string path)
    {
        lock (stateLock)
        {
            if (!changedFiles.TryGetValue(source, out var files))
            {
                files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                changedFiles[source] = files;
            }

            files.Add(path);
            lastChange = timeProvider.GetUtcNow();
        }

        Wake();
    }

    /// <summary>The watcher overflowed and may have missed events, so rescan everything.</summary>
    private void ScheduleFullScan(UsageSource source)
    {
        lock (stateLock)
        {
            nextFullScan[source] = timeProvider.GetUtcNow();
        }

        Wake();
    }
}
