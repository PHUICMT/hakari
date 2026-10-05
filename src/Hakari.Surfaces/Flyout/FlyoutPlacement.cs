using Windows.Graphics;
using Microsoft.UI.Windowing;

namespace Hakari.Surfaces.Flyout;

/// <summary>Right-aligned with the widget, a small gap above the taskbar, kept on screen.</summary>
internal static class FlyoutPlacement
{
    private const int GapAboveTaskbar = 12;
    private const int ScreenMargin = 8;

    public static RectInt32 Above(int anchorX, int anchorY, SizeInt32 size, double scale)
    {
        var gap = (int)Math.Round(GapAboveTaskbar * scale);
        var margin = (int)Math.Round(ScreenMargin * scale);
        var workArea = DisplayArea.GetFromPoint(
            new PointInt32(anchorX, anchorY),
            DisplayAreaFallback.Primary).WorkArea;

        var hasAnchor = anchorX != 0 || anchorY != 0;
        var right = hasAnchor ? anchorX : workArea.X + workArea.Width - margin;
        var bottom = hasAnchor ? anchorY - gap : workArea.Y + workArea.Height - gap;

        var left = Math.Clamp(
            right - size.Width,
            workArea.X + margin,
            workArea.X + workArea.Width - size.Width - margin);
        var top = Math.Max(workArea.Y + margin, bottom - size.Height);
        return new RectInt32(left, top, size.Width, size.Height);
    }
}
