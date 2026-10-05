using System.Drawing;

namespace Hakari.Taskbar;

/// <summary>Where the clicked widget and its taskbar are, in physical screen pixels.</summary>
public sealed class WidgetClickedEventArgs(Rectangle widgetBounds, Rectangle taskbarBounds)
    : EventArgs
{
    public Rectangle WidgetBounds { get; } = widgetBounds;

    public Rectangle TaskbarBounds { get; } = taskbarBounds;
}
