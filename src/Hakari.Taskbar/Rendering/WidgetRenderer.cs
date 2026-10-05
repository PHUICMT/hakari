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
    private const float FullCircleDegrees = 360f;
    private const float TopDegrees = -90f;

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
        var current = frame.Current.Panels;
        if (current.Count == 1)
        {
            return RenderPanel(frame, palette, scale);
        }

        var previous = frame.Previous?.Panels;
        var panels = current
            .Select((panel, index) => RenderPanel(
                frame with
                {
                    Current = panel,
                    Previous = previous is not null && index < previous.Count
                        ? previous[index]
                        : null,
                    HoverAmount = 0,
                },
                palette,
                scale))
            .ToList();
        try
        {
            return Combine(panels, palette, scale, frame.HoverAmount);
        }
        finally
        {
            panels.ForEach(panel => panel.Dispose());
        }
    }

    /// <summary>Blocks left to right with a faint divider between them, one shared hover.</summary>
    private static Bitmap Combine(
        List<Bitmap> panels,
        WidgetPalette palette,
        double scale,
        double hoverAmount)
    {
        var gap = (float)(WidgetMetrics.PanelGap * scale);
        var width = (int)Math.Ceiling(panels.Sum(panel => panel.Width) + gap * (panels.Count - 1));
        var height = panels[0].Height;
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        DrawBackground(graphics, palette, width, height, scale, hoverAmount);

        var dividerInset = (float)(WidgetMetrics.DividerInset * scale);
        using var dividerPen = new Pen(palette.RingTrack, (float)scale);
        var left = 0f;
        for (var index = 0; index < panels.Count; index++)
        {
            if (index > 0)
            {
                var middle = left - gap / 2;
                graphics.DrawLine(dividerPen, middle, dividerInset, middle, height - dividerInset);
            }

            graphics.DrawImageUnscaled(panels[index], (int)Math.Round(left), 0);
            left += panels[index].Width + gap;
        }

        return bitmap;
    }

    private Bitmap RenderPanel(WidgetFrame frame, WidgetPalette palette, double scale)
    {
        EnsureFonts(scale);
        var lines = WidgetLines.From(frame);
        var primaryHeight = Measure(frame.Current.PrimaryText, primaryFont!).Height;
        var secondaryHeight = Measure(frame.Current.SecondaryText, secondaryFont!).Height;

        var padding = (float)(WidgetMetrics.HorizontalPadding * scale);
        var ringSpace = frame.Current.Ring is null
            ? 0f
            : (float)((WidgetMetrics.RingDiameter + WidgetMetrics.RingGap) * scale);
        var width = (int)Math.Ceiling(MeasureWidestText(lines) + ringSpace + padding * 2);
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
        DrawRing(graphics, frame, palette, scale, padding, height);
        var textLeft = padding + ringSpace;
        var primaryColors = PrimaryColors(frame, palette);
        DrawLine(graphics, frame, lines.Primary, primaryFont!, primaryColors, textLeft, top);

        var secondaryColors = SecondaryColors(frame, palette);
        var secondaryTop = top + primaryHeight + lineGap;
        DrawLine(
            graphics,
            frame,
            lines.Secondary,
            secondaryFont!,
            secondaryColors,
            textLeft,
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

    /// <summary>
    /// A track circle with the filled share drawn clockwise from the top. While content
    /// changes, the fill and its color move from the old value to the new one.
    /// </summary>
    private static void DrawRing(
        Graphics graphics,
        WidgetFrame frame,
        WidgetPalette palette,
        double scale,
        float left,
        int height)
    {
        if (frame.Current.Ring is not { } ring)
        {
            return;
        }

        var previous = frame.Previous?.Ring ?? ring;
        var fraction = previous.Fraction
            + (ring.Fraction - previous.Fraction) * frame.ValueProgress;
        var color = ColorBlend.Mix(
            palette.ForRingTone(previous.Tone),
            palette.ForRingTone(ring.Tone),
            frame.ToneProgress);

        var stroke = (float)(WidgetMetrics.RingStroke * scale);
        var diameter = (float)(WidgetMetrics.RingDiameter * scale) - stroke;
        var bounds = new RectangleF(
            left + stroke / 2,
            (height - diameter) / 2f,
            diameter,
            diameter);
        using var trackPen = new Pen(palette.RingTrack, stroke);
        graphics.DrawEllipse(trackPen, bounds);

        var sweep = (float)(Math.Clamp(fraction, 0, 1) * FullCircleDegrees);
        if (sweep <= 0)
        {
            return;
        }

        using var fillPen = new Pen(color, stroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        graphics.DrawArc(fillPen, bounds, TopDegrees, sweep);
    }

    private static (Color From, Color To) PrimaryColors(
        WidgetFrame frame,
        WidgetPalette palette)
    {
        var target = palette.ForPrimaryTone(frame.Current.PrimaryTone);
        var origin = frame.Previous is { } previous
            ? palette.ForPrimaryTone(previous.PrimaryTone)
            : target;
        return (origin, target);
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
