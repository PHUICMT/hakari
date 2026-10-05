using System.Drawing;
using System.Drawing.Drawing2D;

namespace Hakari.Taskbar.Tray;

/// <summary>The app mark for the notification area: a balance scale on the brand indigo.</summary>
internal static class TrayIconArtwork
{
    private const int Size = 32;
    private const float CornerRadius = 7f;
    private const float StrokeWidth = 2.4f;
    private static readonly Color Background = ColorTranslator.FromHtml("#2f4c8c");
    private static readonly Color Glyph = Color.White;

    /// <summary>Returns an icon handle; the caller destroys it with DestroyIcon.</summary>
    public static IntPtr CreateIcon()
    {
        using var bitmap = new Bitmap(Size, Size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            DrawBackground(graphics);
            DrawScale(graphics);
        }

        return bitmap.GetHicon();
    }

    private static void DrawBackground(Graphics graphics)
    {
        var diameter = CornerRadius * 2;
        var edge = Size - 1 - diameter;
        using var path = new GraphicsPath();
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(edge, 0, diameter, diameter, 270, 90);
        path.AddArc(edge, edge, diameter, diameter, 0, 90);
        path.AddArc(0, edge, diameter, diameter, 90, 90);
        path.CloseFigure();
        using var brush = new SolidBrush(Background);
        graphics.FillPath(brush, path);
    }

    /// <summary>Post, base, beam, and two pans, scaled to the 32 px grid.</summary>
    private static void DrawScale(Graphics graphics)
    {
        using var pen = new Pen(Glyph, StrokeWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        graphics.DrawLine(pen, 16, 7, 16, 25);
        graphics.DrawLine(pen, 11, 25, 21, 25);
        graphics.DrawLine(pen, 7, 10, 25, 10);
        graphics.DrawLine(pen, 9, 10, 6, 17);
        graphics.DrawLine(pen, 9, 10, 12, 17);
        graphics.DrawArc(pen, 5, 13, 8, 7, 0, 180);
        graphics.DrawLine(pen, 23, 10, 20, 17);
        graphics.DrawLine(pen, 23, 10, 26, 17);
        graphics.DrawArc(pen, 19, 13, 8, 7, 0, 180);
    }
}
