using Hakari.Core.Interprocess;
using Hakari.Taskbar;

namespace Hakari.Surfaces;

/// <summary>
/// The widget's hover card and right-click menu, both drawn by the window process. Pointing
/// at the widget starts that process early, so the card and menu appear without a wait.
/// </summary>
internal sealed class WidgetSurfaces : IDisposable
{
    /// <summary>As in the design: long enough not to flash while passing over the widget.</summary>
    private static readonly TimeSpan TooltipDelay = TimeSpan.FromMilliseconds(600);

    private readonly Timer tooltipTimer;
    private (int X, int Y) anchor;
    private volatile bool tooltipShown;

    public WidgetSurfaces(TaskbarWidgetHost widgets)
    {
        tooltipTimer = new Timer(_ => ShowTooltip());
        widgets.HoverStarted += (_, hover) => OnHoverStarted(AnchorOf(hover));
        widgets.HoverEnded += (_, _) => OnHoverEnded();
        widgets.RightClicked += (_, click) => OpenMenu(AnchorOf(click));
    }

    public void Dispose() => tooltipTimer.Dispose();

    private static (int X, int Y) AnchorOf(WidgetClickedEventArgs place) =>
        (place.WidgetBounds.Right, place.TaskbarBounds.Top);

    private void OnHoverStarted((int X, int Y) widgetAnchor)
    {
        anchor = widgetAnchor;
        Send(new SurfaceCommand(SurfaceKind.Warm, 0, 0));
        tooltipTimer.Change(TooltipDelay, Timeout.InfiniteTimeSpan);
    }

    private void OnHoverEnded()
    {
        tooltipTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        if (tooltipShown)
        {
            tooltipShown = false;
            Send(new SurfaceCommand(SurfaceKind.HideTooltip, 0, 0));
        }
    }

    private void ShowTooltip()
    {
        tooltipShown = true;
        SurfacesLauncher.Show(new SurfaceCommand(SurfaceKind.Tooltip, anchor.X, anchor.Y));
    }

    /// <summary>The menu takes focus so a click elsewhere closes it, like the flyout.</summary>
    public void OpenMenu((int X, int Y) menuAnchor)
    {
        OnHoverEnded();
        ForegroundPermission.GrantForNextWindow();
        Send(new SurfaceCommand(SurfaceKind.Menu, menuAnchor.X, menuAnchor.Y));
    }

    /// <summary>Off the widget thread: connecting can wait up to 150 ms.</summary>
    private static void Send(SurfaceCommand command) =>
        Task.Run(() => SurfacesLauncher.Show(command));
}
