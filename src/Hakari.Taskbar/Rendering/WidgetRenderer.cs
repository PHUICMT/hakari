using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Hakari.Taskbar.Rendering;

/// <summary>
/// Draws the widget, including frames in the middle of a transition. Content is clipped to an
/// inset rectangle, so moving text never touches an edge.
/// </summary>
public sealed class WidgetRenderer : IDisposable
{
    private readonly FontFamily fontFamily = PickFontFamily();
    private readonly Bitmap measuringSurface = new(1, 1);
    private readonly Graphics measuringGraphics;
    private Font? primaryFont;
    private Font? secondaryFont;
    private double fontScale;

    public WidgetRenderer()
    {
        measuringGraphics = Graphics.FromImage(measuringSurface);
        measuringGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    }

    public Bitmap Render(
        WidgetContent content,
        WidgetPalette palette,
        double scale,
        bool hovered) =>
        Render(WidgetFrame.Settled(content, hovered ? 1 : 0), palette, scale);

    /// <summary>Draws one frame into a premultiplied-alpha bitmap the caller disposes.</summary>
    public Bitmap Render(WidgetFrame frame, WidgetPalette palette, double scale)
    {
        EnsureFonts(scale);
        var lines = WidgetLines.From(frame);
        var primaryHeight = Measure(frame.Current.PrimaryText, primaryFont!).Height;
        var secondaryHeight = Measure(frame.Current.SecondaryText, secondaryFont!).Height;

        var padding = (float)(WidgetMetrics.HorizontalPadding * scale);
        var width = (int)Math.Ceiling(MeasureWidestText(lines) + padding * 2);
        var height = (int)Math.Round(WidgetMetrics.Height * scale);

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        DrawBackground(graphics, palette, width, height, scale, frame.HoverAmount);

        var inset = (float)(WidgetMetrics.MinimumInset * scale);
        graphics.SetClip(new RectangleF(inset, inset, width - inset * 2, height - inset * 2));

        var lineGap = (float)(WidgetMetrics.LineGap * scale);
        var top = (height - (primaryHeight + lineGap + secondaryHeight)) / 2f;
        var primaryColors = (palette.PrimaryText, palette.PrimaryText);
        DrawLine(graphics, frame, lines.Primary, primaryFont!, primaryColors, padding, top);

        var secondaryColors = SecondaryColors(frame, palette);
        var secondaryTop = top + primaryHeight + lineGap;
        DrawLine(
            graphics,
            frame,
            lines.Secondary,
            secondaryFont!,
            secondaryColors,
            padding,
            secondaryTop);
        return bitmap;
    }

    public void Dispose()
    {
        primaryFont?.Dispose();
        secondaryFont?.Dispose();
        measuringGraphics.Dispose();
        measuringSurface.Dispose();
        fontFamily.Dispose();
    }

    private static (Color From, Color To) SecondaryColors(
        WidgetFrame frame,
        WidgetPalette palette)
    {
        var target = palette.ForTone(frame.Current.SecondaryTone);
        var origin = frame.Previous is { } previous
            ? palette.ForTone(previous.SecondaryTone)
            : target;
        return (origin, target);
    }

    /// <summary>
    /// An unchanged line is drawn once. A changed line draws the old text leaving and the new
    /// text arriving: sliding upward when motion is full, cross-fading in place otherwise.
    /// </summary>
    private static void DrawLine(
        Graphics graphics,
        WidgetFrame frame,
        LineChange line,
        Font font,
        (Color From, Color To) colors,
        float left,
        float top)
    {
        var color = ColorBlend.Mix(colors.From, colors.To, frame.ToneProgress);
        if (line.Previous is null)
        {
            DrawText(graphics, line.Current, font, color, left, top);
            return;
        }

        var progress = frame.ValueProgress;
        var travel = frame.MovesText ? font.GetHeight(graphics) * WidgetMetrics.ValueTravel : 0f;
        var leavingTop = top - travel * (float)progress;
        var arrivingTop = top + travel * (float)(1 - progress);

        var leavingColor = ColorBlend.WithOpacity(colors.From, 1 - progress);
        var arrivingColor = ColorBlend.WithOpacity(color, progress);
        DrawText(graphics, line.Previous, font, leavingColor, left, leavingTop);
        DrawText(graphics, line.Current, font, arrivingColor, left, arrivingTop);
    }

    private float MeasureWidestText(WidgetLines lines) =>
        new[]
        {
            Measure(lines.Primary.Current, primaryFont!).Width,
            Measure(lines.Primary.Previous ?? string.Empty, primaryFont!).Width,
            Measure(lines.Secondary.Current, secondaryFont!).Width,
            Measure(lines.Secondary.Previous ?? string.Empty, secondaryFont!).Width,
        }.Max();

    private static void DrawBackground(
        Graphics graphics,
        WidgetPalette palette,
        int width,
        int height,
        double scale,
        double hoverAmount)
    {
        var restingFill = Color.FromArgb(WidgetMetrics.ClickableBackgroundAlpha, palette.HoverFill);
        var fill = ColorBlend.Mix(restingFill, palette.HoverFill, hoverAmount);
        var radius = (float)(WidgetMetrics.CornerRadius * scale);
        using var path = RoundedRectangle(new RectangleF(0, 0, width, height), radius);
        using var brush = new SolidBrush(fill);
        graphics.FillPath(brush, path);
    }

    private static void DrawText(
        Graphics graphics,
        string text,
        Font font,
        Color color,
        float left,
        float top)
    {
        if (color.A == 0 || text.Length == 0)
        {
            return;
        }

        using var brush = new SolidBrush(color);
        graphics.DrawString(text, font, brush, left, top, StringFormat.GenericTypographic);
    }

    private SizeF Measure(string text, Font font) =>
        measuringGraphics.MeasureString(text, font, int.MaxValue, StringFormat.GenericTypographic);

    private void EnsureFonts(double scale)
    {
        if (primaryFont is not null && Math.Abs(fontScale - scale) < double.Epsilon)
        {
            return;
        }

        primaryFont?.Dispose();
        secondaryFont?.Dispose();
        primaryFont = CreateFont((float)(WidgetMetrics.PrimaryFontPixels * scale), FontStyle.Bold);
        secondaryFont = CreateFont(
            (float)(WidgetMetrics.SecondaryFontPixels * scale),
            FontStyle.Regular);
        fontScale = scale;
    }

    private Font CreateFont(float pixels, FontStyle style) =>
        new(fontFamily, pixels, style, GraphicsUnit.Pixel);

    private static FontFamily PickFontFamily()
    {
        using var installed = new InstalledFontCollection();
        var installedNames = installed.Families.Select(family => family.Name).ToHashSet();
        var name = WidgetMetrics.FontFamilies.FirstOrDefault(installedNames.Contains);
        return name is null ? FontFamily.GenericSansSerif : new FontFamily(name);
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        var right = bounds.Right - diameter;
        var bottom = bounds.Bottom - diameter;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(right, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(right, bottom, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bottom, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
