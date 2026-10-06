using System.Drawing;
using System.Drawing.Drawing2D;
using Hakari.Taskbar.Rendering;

namespace Hakari.Taskbar.Tray;

/// <summary>The app mark for the notification area: a balance scale on the brand indigo.</summary>
internal static class TrayIconArtwork
{
    private const int Size = 32;
    private const float CornerRadius = 7f;
    private const float StrokeWidth = 2.4f;
    private static readonly Color Background = ColorTranslator.FromHtml("#2f4c8c");
    private static readonly Color Glyph = Color.White;

    private const float BadgeRadius = 6f;
    private const float BadgeFontPixels = 17f;
    private const float StopBarHalfWidth = 7f;
    private const float StopBarStroke = 4f;
    private const string BadgeFont = "Segoe UI";
    private static readonly Color NormalBadge = ColorTranslator.FromHtml("#2f4c8c");
    private static readonly Color WarningBadge = ColorTranslator.FromHtml("#c98200");
    private static readonly Color WarningInk = ColorTranslator.FromHtml("#1a1205");
    private static readonly Color CriticalBadge = ColorTranslator.FromHtml("#c0322b");

    /// <summary>
    /// The percent on its tone color, or a bar across when full, as in the design's tray
    /// fallback. Drawn at 32 px; Windows scales it to the tray size.
    /// </summary>
    public static IntPtr CreateBadge(TrayBadge badge)
    {
        using var bitmap = new Bitmap(Size, Size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            var (fill, ink) = ColorsOf(badge);
            using var path = RoundedSquare(BadgeRadius);
            using var brush = new SolidBrush(fill);
            graphics.FillPath(brush, path);
            if (badge.Amount is { } amount)
            {
                DrawAmount(graphics, amount, badge.Unit ?? string.Empty, ink);
            }
            else if (badge.IsFull)
            {
                DrawStopBar(graphics, ink);
            }
            else
            {
                DrawNumber(graphics, Math.Clamp(badge.Percent, 0, TrayBadge.FullPercent - 1), ink);
            }
        }

        return bitmap.GetHicon();
    }

    private static (Color Fill, Color Ink) ColorsOf(TrayBadge badge) =>
        badge.IsFull || badge.Tone == WidgetTone.Critical ? (CriticalBadge, Glyph)
            : badge.Tone == WidgetTone.Warning ? (WarningBadge, WarningInk)
            : (NormalBadge, Glyph);

    private static void DrawNumber(Graphics graphics, int percent, Color ink)
    {
        using var font = new Font(BadgeFont, BadgeFontPixels, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(ink);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        var text = percent.ToString(System.Globalization.CultureInfo.InvariantCulture);
        graphics.DrawString(text, font, brush, new RectangleF(0, 0, Size, Size), format);
    }

    private const float UnitFontPixels = 9f;
    private const float AmountFontPixels = 15f;
    private const float UnitBand = 11f;

    /// <summary>The currency code small along the top, the amount large below it.</summary>
    private static void DrawAmount(Graphics graphics, string amount, string unit, Color ink)
    {
        using var unitFont = new Font(
            BadgeFont, UnitFontPixels, FontStyle.Bold, GraphicsUnit.Pixel);
        using var amountFont = new Font(
            BadgeFont, AmountFontPixels, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(ink);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        graphics.DrawString(unit, unitFont, brush, new RectangleF(0, 1, Size, UnitBand), format);
        graphics.DrawString(
            amount,
            amountFont,
            brush,
            new RectangleF(0, UnitBand - 1, Size, Size - UnitBand),
            format);
    }

    private static void DrawStopBar(Graphics graphics, Color ink)
    {
        using var pen = new Pen(ink, StopBarStroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        var middle = Size / 2f;
        var left = middle - StopBarHalfWidth;
        graphics.DrawLine(pen, left, middle, middle + StopBarHalfWidth, middle);
    }

    private static GraphicsPath RoundedSquare(float radius)
    {
        var diameter = radius * 2;
        var edge = Size - 1 - diameter;
        var path = new GraphicsPath();
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(edge, 0, diameter, diameter, 270, 90);
        path.AddArc(edge, edge, diameter, diameter, 0, 90);
        path.AddArc(0, edge, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

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
