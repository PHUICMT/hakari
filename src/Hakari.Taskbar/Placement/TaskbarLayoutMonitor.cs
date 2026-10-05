using System.Collections.Concurrent;
using System.Windows.Automation;

namespace Hakari.Taskbar.Placement;

/// <summary>
/// Keeps every taskbar's layout fresh on its own thread. It re-reads only when the taskbar's
/// structure changes (apps open or close, tray icons come and go), plus a slow safety refresh.
/// Widget threads just read the cached result.
/// </summary>
public sealed class TaskbarLayoutMonitor : IDisposable
{
    private static readonly TimeSpan SafetyRefreshInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ChangeDebounce = TimeSpan.FromMilliseconds(400);

    /// <summary>A taskbar still starting up is re-read quickly until it reports a layout.</summary>
    private static readonly TimeSpan MissingLayoutRetryInterval = TimeSpan.FromSeconds(1.5);

    private readonly ConcurrentDictionary<IntPtr, TaskbarLayout> layouts = new();
    private readonly Dictionary<IntPtr, AutomationElement> subscribed = [];
    private readonly AutoResetEvent changed = new(initialState: false);
    private readonly CancellationTokenSource cancellation = new();
    private readonly Thread worker;
    private readonly StructureChangedEventHandler structureChangedHandler;

    public TaskbarLayoutMonitor()
    {
        structureChangedHandler = (_, _) => changed.Set();
        worker = new Thread(RunLoop) { IsBackground = true, Name = "Hakari taskbar layout" };
        worker.Start();
    }

    public event EventHandler? LayoutChanged;

    public int Refreshes { get; private set; }

    public TaskbarLayout? TryGet(IntPtr taskbarHandle) =>
        layouts.TryGetValue(taskbarHandle, out var layout) ? layout : null;

    /// <summary>Asks for a re-read soon, for example after Explorer restarted.</summary>
    public void RequestRefresh() => changed.Set();

    public void Dispose()
    {
        cancellation.Cancel();
        changed.Set();
        worker.Join(TimeSpan.FromSeconds(2));
        Unsubscribe();
        changed.Dispose();
        cancellation.Dispose();
    }

    private void RunLoop()
    {
        while (!cancellation.IsCancellationRequested)
        {
            var allRead = RefreshAll();
            changed.WaitOne(allRead ? SafetyRefreshInterval : MissingLayoutRetryInterval);
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            Thread.Sleep(ChangeDebounce);
            changed.Reset();
        }
    }

    /// <summary>Returns false while any taskbar has no trustworthy layout yet.</summary>
    private bool RefreshAll()
    {
        var anyChanged = false;
        var allRead = true;
        var liveHandles = new HashSet<IntPtr>();
        foreach (var target in TaskbarTarget.All())
        {
            var handle = target.Resolve();
            if (handle == IntPtr.Zero)
            {
                continue;
            }

            liveHandles.Add(handle);
            EnsureSubscribed(handle);
            var layout = TaskbarLayoutReader.Read(handle);
            allRead &= layout is not null;
            anyChanged |= Store(handle, layout);
        }

        foreach (var staleHandle in layouts.Keys.Where(key => !liveHandles.Contains(key)))
        {
            layouts.TryRemove(staleHandle, out _);
        }

        Refreshes++;
        if (anyChanged)
        {
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        return allRead;
    }

    /// <summary>An unreadable layout clears the old one, so stale positions are not used.</summary>
    private bool Store(IntPtr handle, TaskbarLayout? layout)
    {
        var previous = TryGet(handle);
        if (layout is null)
        {
            return layouts.TryRemove(handle, out _);
        }

        layouts[handle] = layout;
        return previous != layout;
    }

    private void EnsureSubscribed(IntPtr handle)
    {
        if (subscribed.ContainsKey(handle))
        {
            return;
        }

        try
        {
            var element = AutomationElement.FromHandle(handle);
            Automation.AddStructureChangedEventHandler(
                element,
                TreeScope.Subtree,
                structureChangedHandler);
            subscribed[handle] = element;
        }
        catch (Exception exception) when (TaskbarLayoutReader.IsAutomationProblem(exception))
        {
        }
    }

    private void Unsubscribe()
    {
        foreach (var element in subscribed.Values)
        {
            try
            {
                Automation.RemoveStructureChangedEventHandler(element, structureChangedHandler);
            }
            catch (Exception exception) when (TaskbarLayoutReader.IsAutomationProblem(exception))
            {
            }
        }

        subscribed.Clear();
    }
}
