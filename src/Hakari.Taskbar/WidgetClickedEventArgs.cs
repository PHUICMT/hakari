using System.Drawing;

namespace Hakari.Taskbar;

/// <summary>
/// Where the clicked widget and its taskbar are, and across where the pointer was, in
/// physical screen pixels.
/// </summary>
public sealed class WidgetClickedEventArgs(
    Rectangle widgetBounds,
    Rectangle taskbarBounds,
    int pointerX) : EventArgs
{
    public int PointerX { get; } = pointerX;

    public Rectangle WidgetBounds { get; } = widgetBounds;

    public Rectangle TaskbarBounds { get; } = taskbarBounds;
}
