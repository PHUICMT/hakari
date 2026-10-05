using Hakari.Taskbar.Interop;
using Hakari.Taskbar.Placement;
using Hakari.Taskbar.Rendering;

namespace Hakari.Taskbar;

/// <summary>
/// Owns the widgets on every taskbar through a hidden top-level window, which is what receives
/// "TaskbarCreated" and keeps the timer running while Explorer restarts. Every method runs on
/// the creating thread; other threads use <see cref="PostContent"/>.
/// </summary>
public sealed class TaskbarWidgetHost : IDisposable
{
    private const string ClassName = "HakariTaskbarHost";
    private const string TaskbarCreatedMessageName = "TaskbarCreated";
    private const uint ContentChangedMessage = WindowMessages.Application + 1;
    private const uint LayoutChangedMessage = WindowMessages.Application + 2;
    private const uint PlacementTimerInterval = 1000;

    /// <summary>The taskbar repaints after a theme change, so its color is checked again.</summary>
    private const uint ThemeSettleDelay = 1500;

    private static readonly IntPtr PlacementTimerId = new(1);
    private static readonly IntPtr ThemeSettleTimerId = new(2);

    private readonly TaskbarWidgetHostOptions options;
    private readonly WidgetRenderer renderer = new();
    private readonly TaskbarLayoutMonitor layoutMonitor = new();
    private readonly List<WidgetWindow> widgets = [];
    private readonly uint taskbarCreatedMessage;
    private readonly object pendingLock = new();
    private readonly IntPtr hostHandle;

    private WidgetContent content;
    private WidgetContent? pendingContent;

    public TaskbarWidgetHost(TaskbarWidgetHostOptions options, WidgetContent initialContent)
    {
        this.options = options;
        content = initialContent;
        taskbarCreatedMessage = User32.RegisterWindowMessage(TaskbarCreatedMessageName);
        WindowClassRegistry.Register(ClassName, HandleMessage);
        hostHandle = CreateHiddenHostWindow();
        layoutMonitor.LayoutChanged += (_, _) =>
            User32.PostMessage(hostHandle, LayoutChangedMessage, IntPtr.Zero, IntPtr.Zero);

        SynchronizeWidgets(renderAll: true);
        User32.SetTimer(hostHandle, PlacementTimerId, PlacementTimerInterval, IntPtr.Zero);
    }

    public event EventHandler? Clicked;

    public event EventHandler? RightClicked;

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

    /// <summary>Thread-safe: queues new content and wakes the widget thread.</summary>
    public void PostContent(WidgetContent newContent)
    {
        lock (pendingLock)
        {
            pendingContent = newContent;
        }

        User32.PostMessage(hostHandle, ContentChangedMessage, IntPtr.Zero, IntPtr.Zero);
    }

    public void Dispose()
    {
        foreach (var widget in widgets)
        {
            widget.Dispose();
        }

        widgets.Clear();
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

    /// <summary>
    /// Creates widgets for new or restarted taskbars and drops dead ones. Only widgets that
    /// are new, changed DPI, or are asked for (<paramref name="renderAll"/>) are redrawn;
    /// the rest are just kept in place, so the timer costs almost nothing.
    /// </summary>
    private void SynchronizeWidgets(bool renderAll)
    {
        foreach (var deadWidget in widgets.Where(widget => !widget.IsAlive).ToList())
        {
            deadWidget.Dispose();
            widgets.Remove(deadWidget);
            Diagnostics.WidgetsRecreated++;
        }

        foreach (var target in WantedTargets())
        {
            var taskbarHandle = target.Resolve();
            var exists = widgets.Any(widget => widget.Target == target);
            if (taskbarHandle != IntPtr.Zero && !exists)
            {
                widgets.Add(CreateWidget(target, taskbarHandle));
            }
        }

        foreach (var widget in widgets)
        {
            var taskbar = TaskbarLocator.Describe(widget.Target, layoutMonitor);
            if (taskbar is null)
            {
                continue;
            }

            var needsRender = renderAll || widget.RenderedDpi != taskbar.Dpi;
            if (needsRender)
            {
                Render(widget, taskbar);
            }

            Place(widget, taskbar);
        }
    }

    private IEnumerable<TaskbarTarget> WantedTargets() =>
        options.ShowOnSecondaryTaskbars ? TaskbarTarget.All() : [TaskbarTarget.Primary];

    private WidgetWindow CreateWidget(TaskbarTarget target, IntPtr taskbarHandle)
    {
        var widget = WidgetWindow.Create(target, taskbarHandle, options.Mode);
        widget.Clicked += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
        widget.RightClicked += (_, _) => RightClicked?.Invoke(this, EventArgs.Empty);
        widget.HoverChanged += (_, _) => RenderAndPlace(widget);
        return widget;
    }

    private void RenderAndPlace(WidgetWindow widget)
    {
        var taskbar = TaskbarLocator.Describe(widget.Target, layoutMonitor);
        if (taskbar is null)
        {
            return;
        }

        Render(widget, taskbar);
        Place(widget, taskbar);
    }

    private void Render(WidgetWindow widget, TaskbarInfo taskbar)
    {
        var palette = TaskbarTheme.IsLight(taskbar)
            ? WidgetPalette.LightTaskbar
            : WidgetPalette.DarkTaskbar;
        using var bitmap = renderer.Render(content, palette, taskbar.Scale, widget.IsHovered);
        widget.Present(bitmap, taskbar.Dpi);
        Diagnostics.Renders++;
    }

    private void Place(WidgetWindow widget, TaskbarInfo taskbar)
    {
        var overlayHidden = options.Mode == AttachMode.TopMostOverlay
            && FullScreenDetector.IsSomethingFullScreen();
        var target = WidgetPlacement.LeftOfNotificationArea(taskbar, widget.RenderedSize);
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

        SynchronizeWidgets(renderAll: true);
    }

    private IntPtr HandleMessage(
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
                layoutMonitor.RequestRefresh();
                SynchronizeWidgets(renderAll: true);
                User32.SetTimer(hostHandle, ThemeSettleTimerId, ThemeSettleDelay, IntPtr.Zero);
                return IntPtr.Zero;
            default:
                return User32.DefWindowProc(windowHandle, message, wordParameter, longParameter);
        }
    }
}
