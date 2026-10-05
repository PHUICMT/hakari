using Hakari.Core.Settings;

namespace Hakari.Settings;

/// <summary>
/// Notices when the settings window (another process) saves settings. Changes are batched,
/// because one save can raise several file events.
/// </summary>
internal sealed class SettingsWatcher : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);

    private readonly FileSystemWatcher watcher;
    private readonly Timer debounceTimer;

    public SettingsWatcher(SettingsStore store)
    {
        var directory = Path.GetDirectoryName(store.Path)!;
        Directory.CreateDirectory(directory);
        debounceTimer = new Timer(_ => Changed?.Invoke(this, EventArgs.Empty));
        watcher = new FileSystemWatcher(directory, Path.GetFileName(store.Path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        watcher.Changed += (_, _) => Schedule();
        watcher.Created += (_, _) => Schedule();
        watcher.Renamed += (_, _) => Schedule();
        watcher.EnableRaisingEvents = true;
    }

    /// <summary>Raised on a thread-pool thread.</summary>
    public event EventHandler? Changed;

    public void Dispose()
    {
        watcher.Dispose();
        debounceTimer.Dispose();
    }

    private void Schedule() => debounceTimer.Change(Debounce, Timeout.InfiniteTimeSpan);
}
