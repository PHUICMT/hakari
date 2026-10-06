using Windows.Graphics;
using Microsoft.UI.Windowing;

namespace Hakari.Surfaces.Flyout;

/// <summary>
/// A small gap above the taskbar, kept on screen: menus and tooltips end at the anchor, the
/// flyout centers on it, so it opens over the spot that was clicked.
/// </summary>
internal static class FlyoutPlacement
{
    private const int GapAboveTaskbar = 12;
    private const int ScreenMargin = 8;

    public static RectInt32 Above(
        int anchorX,
        int anchorY,
        SizeInt32 size,
        double scale,
        bool centered = false)
    {
        var gap = (int)Math.Round(GapAboveTaskbar * scale);
        var margin = (int)Math.Round(ScreenMargin * scale);
        var workArea = DisplayArea.GetFromPoint(
            new PointInt32(anchorX, anchorY),
            DisplayAreaFallback.Primary).WorkArea;

        var hasAnchor = anchorX != 0 || anchorY != 0;
        var right = !hasAnchor ? workArea.X + workArea.Width - margin
            : centered ? anchorX + size.Width / 2
            : anchorX;
        var bottom = hasAnchor ? anchorY - gap : workArea.Y + workArea.Height - gap;

        var left = Math.Clamp(
            right - size.Width,
            workArea.X + margin,
            workArea.X + workArea.Width - size.Width - margin);
        var top = Math.Max(workArea.Y + margin, bottom - size.Height);
        return new RectInt32(left, top, size.Width, size.Height);
    }
}
