using Windows.Graphics;
using Microsoft.UI.Windowing;

namespace Hakari.Surfaces.Flyout;

/// <summary>Which part of a window lines up with the anchor across the screen.</summary>
public enum AnchorSide
{
    /// <summary>Ends at the anchor, as the hover card does at the widget's edge.</summary>
    End,

    /// <summary>Centered on it, as the flyout is on the spot that was clicked.</summary>
    Center,

    /// <summary>Starts at it, as a menu does at the pointer.</summary>
    Start,
}

/// <summary>A small gap above the taskbar, lined up with the anchor, kept on screen.</summary>
internal static class FlyoutPlacement
{
    private const int GapAboveTaskbar = 12;
    private const int ScreenMargin = 8;

    public static RectInt32 Above(
        int anchorX,
        int anchorY,
        SizeInt32 size,
        double scale,
        AnchorSide side = AnchorSide.End)
    {
        var gap = (int)Math.Round(GapAboveTaskbar * scale);
        var margin = (int)Math.Round(ScreenMargin * scale);
        var workArea = DisplayArea.GetFromPoint(
            new PointInt32(anchorX, anchorY),
            DisplayAreaFallback.Primary).WorkArea;

        var hasAnchor = anchorX != 0 || anchorY != 0;
        var right = !hasAnchor ? workArea.X + workArea.Width - margin
            : side switch
            {
                AnchorSide.Center => anchorX + size.Width / 2,
                AnchorSide.Start => anchorX + size.Width,
                _ => anchorX,
            };
        var bottom = hasAnchor ? anchorY - gap : workArea.Y + workArea.Height - gap;

        var left = Math.Clamp(
            right - size.Width,
            workArea.X + margin,
            workArea.X + workArea.Width - size.Width - margin);
        var top = Math.Max(workArea.Y + margin, bottom - size.Height);
        return new RectInt32(left, top, size.Width, size.Height);
    }
}
