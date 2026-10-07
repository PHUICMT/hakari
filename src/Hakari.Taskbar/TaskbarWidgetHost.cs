using System.Diagnostics;
using Hakari.Taskbar.Interop;
using Hakari.Taskbar.Motion;
using Hakari.Taskbar.Placement;
using Hakari.Taskbar.Rendering;

namespace Hakari.Taskbar;

/// <summary>
/// Owns the widgets on every taskbar through a hidden top-level window, which is what receives
/// "TaskbarCreated" and keeps the timers running while Explorer restarts. Every method runs on
/// the creating thread; other threads use <see cref="PostContent"/>.
/// </summary>
public sealed class TaskbarWidgetHost : IDisposable
{
    private const string ClassName = "HakariTaskbarHost";
    private const string TaskbarCreatedMessageName = "TaskbarCreated";
    private const uint ContentChangedMessage = WindowMessages.Application + 1;
    private const uint LayoutChangedMessage = WindowMessages.Application + 2;
    private const uint ActionPostedMessage = WindowMessages.Application + 3;
    private const uint PlacementTimerInterval = 1000;

    /// <summary>The taskbar repaints after a theme change, so its color is checked again.</summary>
    private const uint ThemeSettleDelay = 1500;

    /// <summary>About 60 frames a second, and only while something is moving.</summary>
    private const uint AnimationFrameInterval = 16;

    private static readonly IntPtr PlacementTimerId = new(1);
    private static readonly IntPtr ThemeSettleTimerId = new(2);
    private static readonly IntPtr AnimationTimerId = new(3);

    private TaskbarWidgetHostOptions options;
    private readonly WidgetRenderer renderer = new();
    private readonly TaskbarLayoutMonitor layoutMonitor = new();
    private readonly List<WidgetWindow> widgets = [];
    private readonly Dictionary<WidgetWindow, WidgetState> states = [];
    private readonly uint taskbarCreatedMessage;
    private readonly object pendingLock = new();
    private readonly IntPtr hostHandle;
    private readonly Queue<Action> pendingActions = new();

    private WidgetContent content;
    private WidgetContent? pendingContent;
    private MotionTokens motion;
    private bool animationTimerRunning;

    public TaskbarWidgetHost(TaskbarWidgetHostOptions options, WidgetContent initialContent)
    {
        this.options = options;
        content = initialContent;
        motion = ResolveMotion();
        taskbarCreatedMessage = User32.RegisterWindowMessage(TaskbarCreatedMessageName);
        WindowClassRegistry.Register(ClassName, HandleMessage);
        hostHandle = CreateHiddenHostWindow();
        layoutMonitor.LayoutChanged += (_, _) =>
            User32.PostMessage(hostHandle, LayoutChangedMessage, IntPtr.Zero, IntPtr.Zero);

        SynchronizeWidgets(renderAll: true);
        User32.SetTimer(hostHandle, PlacementTimerId, PlacementTimerInterval, IntPtr.Zero);
    }

    public event EventHandler<WidgetClickedEventArgs>? Clicked;

    public event EventHandler<WidgetClickedEventArgs>? RightClicked;

    /// <summary>The wheel turned over a widget: positive away from the user.</summary>
    public event EventHandler<int>? Scrolled;

    /// <summary>The pointer came onto a widget; for the hover card.</summary>
    public event EventHandler<WidgetClickedEventArgs>? HoverStarted;

    public event EventHandler? HoverEnded;

    public WidgetDiagnostics Diagnostics { get; } = new();

    public int LayoutRefreshes => layoutMonitor.Refreshes;

    public int WidgetCount => widgets.Count;

    /// <summary>One line per widget, for diagnostics. Call on the widget thread.</summary>
    public IReadOnlyList<string> DescribeWidgets() =>
    [
        .. widgets.Select(widget =>
        {
            var name = widget.Target.IsPrimary
                ? "primary"
                : $"secondary {widget.Target.SecondaryIndex}";
            return $"{name}: {widget.Describe()}";
        }),
    ];

    /// <summary>Applies a new animation choice; null follows Windows.</summary>
    public void SetMotion(MotionPreference? preference)
    {
        options = options with { Motion = preference };
        motion = ResolveMotion();
    }

    /// <summary>Changes which displays get a widget (see TaskbarWidgetHostOptions).</summary>
    public void SetDisplayFilter(Func<string, bool>? showOnDisplay)
    {
        options = options with { ShowOnDisplay = showOnDisplay };
        SynchronizeWidgets(renderAll: true);
    }

    /// <summary>Thread-safe: queues new content and wakes the widget thread.</summary>
    public void PostContent(WidgetContent newContent)
    {
        lock (pendingLock)
        {
            pendingContent = newContent;
        }

        User32.PostMessage(hostHandle, ContentChangedMessage, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>Thread-safe: runs <paramref name="action"/> on the widget thread.</summary>
    public void PostAction(Action action)
    {
        lock (pendingLock)
        {
            pendingActions.Enqueue(action);
        }

        User32.PostMessage(hostHandle, ActionPostedMessage, IntPtr.Zero, IntPtr.Zero);
    }

    public void Dispose()
    {
        foreach (var widget in widgets)
        {
            widget.Dispose();
        }

        widgets.Clear();
        states.Clear();
        layoutMonitor.Dispose();
        renderer.Dispose();
        User32.DestroyWindow(hostHandle);
    }

    private static IntPtr CreateHiddenHostWindow() => User32.CreateWindowEx(
        ExtendedWindowStyles.ToolWindow,
        ClassName,
        string.Empty,
        WindowStyles.Popup,
        0,
        0,
        0,
        0,
        IntPtr.Zero,
        IntPtr.Zero,
        Kernel32.GetModuleHandle(null),
        IntPtr.Zero);

    private MotionTokens ResolveMotion() =>
        MotionTokens.For(MotionSettings.Resolve(options.Motion));

    /// <summary>
    /// Creates widgets for new or restarted taskbars and drops dead ones. Only widgets that
    /// are new, changed DPI, or are asked for (<paramref name="renderAll"/>) are redrawn;
    /// the rest are just kept in place, so the timer costs almost nothing.
    /// </summary>
    /// <param name="sampleTheme">Read the taskbar's color again; defaults to a full render.</param>
    private void SynchronizeWidgets(bool renderAll, bool? sampleTheme = null)
    {
        RemoveDeadWidgets();
        var wanted = WantedTargets();
        RemoveUnwantedWidgets(wanted);
        AddMissingWidgets(wanted);

        foreach (var widget in widgets)
        {
            var taskbar = TaskbarLocator.Describe(widget.Target, layoutMonitor);
            if (taskbar is null)
            {
                continue;
            }

            if (renderAll || widget.RenderedDpi != taskbar.Dpi)
            {
                RenderFull(widget, taskbar, sampleTheme ?? renderAll);
            }

            Place(widget, taskbar);
        }
    }

    private void RemoveDeadWidgets()
    {
        foreach (var deadWidget in widgets.Where(widget => !widget.IsAlive).ToList())
        {
            deadWidget.Dispose();
            widgets.Remove(deadWidget);
            states.Remove(deadWidget);
            Diagnostics.WidgetsRecreated++;
        }
    }

    private void AddMissingWidgets(IReadOnlyList<TaskbarTarget> wanted)
    {
        foreach (var target in wanted)
        {
            var taskbarHandle = target.Resolve();
            var exists = widgets.Any(widget => widget.Target == target);
            if (taskbarHandle != IntPtr.Zero && !exists)
            {
                var widget = CreateWidget(target, taskbarHandle);
                widgets.Add(widget);
                states[widget] = new WidgetState(content);
            }
        }
    }

    private IReadOnlyList<TaskbarTarget> WantedTargets()
    {
        var all = TaskbarTarget.All();
        if (options.ShowOnDisplay is not { } showOnDisplay)
        {
            return all;
        }

        var wanted = all
            .Where(target => target.DisplayDeviceName() is { } device && showOnDisplay(device))
            .ToList();
        return wanted.Count > 0 ? wanted : [TaskbarTarget.Primary];
    }

    private void RemoveUnwantedWidgets(IReadOnlyList<TaskbarTarget> wanted)
    {
        foreach (var unwanted in widgets.Where(widget => !wanted.Contains(widget.Target)).ToList())
        {
            unwanted.Dispose();
            widgets.Remove(unwanted);
            states.Remove(unwanted);
        }
    }

    private WidgetWindow CreateWidget(TaskbarTarget target, IntPtr taskbarHandle)
    {
        var widget = WidgetWindow.Create(target, taskbarHandle, options.Mode);
        widget.Clicked += (_, _) => RaiseClicked(widget);
        widget.RightClicked += (_, _) => Raise(RightClicked, widget);
        widget.Scrolled += (_, delta) => Scrolled?.Invoke(this, delta);
        widget.HoverChanged += (_, _) =>
        {
            StartHoverTransition(widget);
            if (widget.IsHovered)
            {
                Raise(HoverStarted, widget);
            }
            else
            {
                HoverEnded?.Invoke(this, EventArgs.Empty);
            }
        };
        return widget;
    }

    /// <summary>Middle of the primary widget on its taskbar's top, for tray clicks.</summary>
    public System.Drawing.Point? PrimaryWidgetAnchor()
    {
        var widget = widgets.FirstOrDefault(candidate => candidate.Target.IsPrimary);
        var taskbar = widget is null ? null : TaskbarLocator.Describe(widget.Target, layoutMonitor);
        return widget is null || taskbar is null
            ? null
            : new System.Drawing.Point(
                widget.ScreenBounds.Left + widget.ScreenBounds.Width / 2,
                taskbar.Bounds.Top);
    }

    private void RaiseClicked(WidgetWindow widget) => Raise(Clicked, widget);

    private void Raise(EventHandler<WidgetClickedEventArgs>? handler, WidgetWindow widget)
    {
        var taskbar = TaskbarLocator.Describe(widget.Target, layoutMonitor);
        if (taskbar is not null)
        {
            var pointerX = Interop.User32.GetCursorPos(out var cursor)
                ? cursor.X
                : widget.ScreenBounds.Left + widget.ScreenBounds.Width / 2;
            handler?.Invoke(
                this,
                new WidgetClickedEventArgs(widget.ScreenBounds, taskbar.Bounds, pointerX));
        }
    }

    /// <summary>Samples the taskbar's color again, then draws the current frame.</summary>
    /// <summary>
    /// Draws the current frame, sampling the taskbar's color first only when asked or never
    /// done for this widget: reading the screen is slow, and content updates do not need it.
    /// </summary>
    private void RenderFull(WidgetWindow widget, TaskbarInfo taskbar, bool sampleTheme)
    {
        var state = states[widget];
        if (sampleTheme || !state.PaletteSampled)
        {
            state.Palette = TaskbarTheme.IsLight(taskbar)
                ? WidgetPalette.LightTaskbar
                : WidgetPalette.DarkTaskbar;
            state.PaletteSampled = true;
        }

        RenderFrame(widget, taskbar, state);
    }

    private void RenderFrame(WidgetWindow widget, TaskbarInfo taskbar, WidgetState state)
    {
        var frame = state.Animation.FrameAt(Stopwatch.GetTimestamp(), motion);

        // GDI+ can refuse an odd shape mid-animation; that frame is skipped, not the widget.
        try
        {
            using var bitmap = renderer.Render(frame, state.Palette, taskbar.Scale);
            widget.Present(bitmap, taskbar.Dpi);
            Diagnostics.Renders++;
        }
        catch (Exception exception) when (
            exception is System.Runtime.InteropServices.ExternalException
                or ArgumentException or OutOfMemoryException or InvalidOperationException)
        {
            Diagnostics.SkippedFrames++;
        }
    }

    private void Place(WidgetWindow widget, TaskbarInfo taskbar)
    {
        var overlayHidden = options.Mode == AttachMode.TopMostOverlay
            && FullScreenDetector.IsSomethingFullScreen();
        var target = WidgetPlacement.LeftOfNotificationArea(taskbar, widget.RenderedSize);
        if (!overlayHidden && target is null && TryCompact(widget, taskbar))
        {
            target = WidgetPlacement.LeftOfNotificationArea(taskbar, widget.RenderedSize);
        }

        if (overlayHidden || target is not { } screenBounds)
        {
            Diagnostics.Collisions += overlayHidden ? 0 : 1;
            widget.Hide();
            return;
        }

        if (widget.Place(taskbar, screenBounds))
        {
            Diagnostics.Moves++;
        }
    }

    /// <summary>
    /// No room for the full widget: it shrinks to its first block's ring and top line, as the
    /// minimal template does, before giving up and hiding. The next content tries full again.
    /// </summary>
    private bool TryCompact(WidgetWindow widget, TaskbarInfo taskbar)
    {
        var state = states[widget];
        if (state.IsCompact)
        {
            return false;
        }

        state.IsCompact = true;
        state.Animation.ChangeContent(
            content.Compact(),
            Stopwatch.GetTimestamp(),
            MotionTokens.For(MotionPreference.Off));
        RenderFrame(widget, taskbar, state);
        return true;
    }

    private void RunPendingActions()
    {
        while (TakePendingAction() is { } action)
        {
            // One failing action never stops the ones queued after it.
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Faulted?.Invoke(exception);
            }
        }
    }

    private Action? TakePendingAction()
    {
        lock (pendingLock)
        {
            return pendingActions.TryDequeue(out var action) ? action : null;
        }
    }

    private void ApplyPendingContent()
    {
        lock (pendingLock)
        {
            if (pendingContent is null)
            {
                return;
            }

            content = pendingContent;
            pendingContent = null;
        }

        motion = ResolveMotion();
        var now = Stopwatch.GetTimestamp();
        var spoken = "Hakari: " + content.Spoken();
        foreach (var widget in widgets)
        {
            widget.SetAccessibleName(spoken);
        }

        foreach (var state in states.Values)
        {
            state.IsCompact = false;
            state.Animation.ChangeContent(content, now, motion);
        }

        SynchronizeWidgets(renderAll: true, sampleTheme: false);
        StartAnimationTimerIfNeeded();
    }

    private void StartHoverTransition(WidgetWindow widget)
    {
        if (!states.TryGetValue(widget, out var state))
        {
            return;
        }

        state.Animation.ChangeHover(widget.IsHovered, Stopwatch.GetTimestamp(), motion);
        StartAnimationTimerIfNeeded();
        DrawAnimationFrames();
    }

    private void StartAnimationTimerIfNeeded()
    {
        var now = Stopwatch.GetTimestamp();
        var anyMoving = states.Values.Any(state => state.Animation.NeedsFrame(now, motion));
        if (anyMoving && !animationTimerRunning)
        {
            User32.SetTimer(hostHandle, AnimationTimerId, AnimationFrameInterval, IntPtr.Zero);
            animationTimerRunning = true;
        }
    }

    /// <summary>Draws a frame for every moving widget; stops the timer once all rest.</summary>
    private void DrawAnimationFrames()
    {
        var now = Stopwatch.GetTimestamp();
        var anyMoving = false;
        foreach (var widget in widgets)
        {
            var state = states[widget];
            if (!state.Animation.NeedsFrame(now, motion))
            {
                continue;
            }

            var taskbar = TaskbarLocator.Describe(widget.Target, layoutMonitor);
            if (taskbar is null)
            {
                continue;
            }

            RenderFrame(widget, taskbar, state);
            Place(widget, taskbar);
            anyMoving |= state.Animation.NeedsFrame(Stopwatch.GetTimestamp(), motion);
        }

        if (!anyMoving && animationTimerRunning)
        {
            User32.KillTimer(hostHandle, AnimationTimerId);
            animationTimerRunning = false;
        }
    }

    private void ScheduleThemeRecheck() =>
        User32.SetTimer(hostHandle, ThemeSettleTimerId, ThemeSettleDelay, IntPtr.Zero);

    /// <summary>Anything that goes wrong in here is told, never thrown into Windows.</summary>
    public event Action<Exception>? Faulted;

    /// <summary>
    /// Called by Windows: an exception that escaped would end Hakari, so each message is
    /// handled on its own and a failure is reported and skipped.
    /// </summary>
    private IntPtr HandleMessage(
        IntPtr windowHandle,
        uint message,
        IntPtr wordParameter,
        IntPtr longParameter)
    {
        try
        {
            return Dispatch(windowHandle, message, wordParameter, longParameter);
        }
        catch (Exception exception)
        {
            Faulted?.Invoke(exception);
            return IntPtr.Zero;
        }
    }

    private IntPtr Dispatch(
        IntPtr windowHandle,
        uint message,
        IntPtr wordParameter,
        IntPtr longParameter)
    {
        if (message == taskbarCreatedMessage)
        {
            Diagnostics.ExplorerRestarts++;
            layoutMonitor.RequestRefresh();
            SynchronizeWidgets(renderAll: true);
            return IntPtr.Zero;
        }

        switch (message)
        {
            case ContentChangedMessage:
                ApplyPendingContent();
                return IntPtr.Zero;
            case ActionPostedMessage:
                RunPendingActions();
                return IntPtr.Zero;
            case WindowMessages.Timer when wordParameter == AnimationTimerId:
                DrawAnimationFrames();
                return IntPtr.Zero;
            case WindowMessages.Timer when wordParameter == ThemeSettleTimerId:
                User32.KillTimer(hostHandle, ThemeSettleTimerId);
                SynchronizeWidgets(renderAll: true);
                return IntPtr.Zero;
            case LayoutChangedMessage:
            case WindowMessages.Timer:
                SynchronizeWidgets(renderAll: false);
                return IntPtr.Zero;
            case WindowMessages.DisplayChange:
            case WindowMessages.SettingChange:
                motion = ResolveMotion();
                layoutMonitor.RequestRefresh();
                SynchronizeWidgets(renderAll: true);
                ScheduleThemeRecheck();
                return IntPtr.Zero;
            default:
                return User32.DefWindowProc(windowHandle, message, wordParameter, longParameter);
        }
    }
}
