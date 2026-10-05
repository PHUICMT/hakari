using System.Drawing;

namespace Hakari.Taskbar.Placement;

/// <summary>Screen rectangles of one taskbar, in physical pixels.</summary>
public sealed record TaskbarInfo(
    IntPtr Handle,
    bool IsPrimary,
    Rectangle Bounds,
    Rectangle NotificationArea,
    Rectangle AppButtons,
    uint Dpi,
    bool HasXamlLayout)
{
    private const double DefaultDpi = 96.0;

    public double Scale => Dpi / DefaultDpi;
}
