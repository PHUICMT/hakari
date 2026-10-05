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
    private const char ThaiBlockStart = '฀';
    private const char ThaiBlockEnd = '๿';

    /// <summary>At this share or more a ring is full; a hair below still shows as a ring.</summary>
    private const double FullFraction = 0.999;

    /// <summary>Typographic layout that keeps trailing spaces, so pieces join up.</summary>
    private static readonly StringFormat PieceFormat = CreatePieceFormat();

    private readonly FontFamily fontFamily = PickFontFamily(WidgetMetrics.FontFamilies);
    private readonly FontFamily thaiFontFamily = PickFontFamily(WidgetMetrics.ThaiFontFamilies);
    private readonly Bitmap measuringSurface = new(1, 1);
    private readonly Graphics measuringGraphics;
    private Font? primaryFont;
    private Font? secondaryFont;
    private Font? primaryThaiFont;
    private Font? secondaryThaiFont;
    private double fontScale;

    public WidgetRenderer()
    {
        measuringGraphics = Graphics.FromImage(measuringSurface);
        measuringGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;
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
        var textWidth = MeasureWidestText(lines);
        var dotsSpace = DotsWidth(frame.Current, scale);
        var width = (int)Math.Ceiling(textWidth + ringSpace + dotsSpace + padding * 2);
        var height = (int)Math.Round(WidgetMetrics.Height * scale);

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
        DrawBackground(graphics, palette, width, height, scale, frame.HoverAmount);

        var inset = (float)(WidgetMetrics.MinimumInset * scale);
        graphics.SetClip(new RectangleF(inset, inset, width - inset * 2, height - inset * 2));

        var lineGap = (float)(WidgetMetrics.LineGap * scale);
        var top = (height - (primaryHeight + lineGap + secondaryHeight)) / 2f;
        DrawRing(graphics, frame, palette, scale, padding, height);
        var dotsLeft = padding + ringSpace + textWidth;
        DrawTurnDots(graphics, frame.Current, palette, scale, dotsLeft, height);
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
        primaryThaiFont?.Dispose();
        secondaryThaiFont?.Dispose();
        thaiFontFamily.Dispose();
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
        var stroke = (float)(WidgetMetrics.RingStroke * scale);
        var outerDiameter = (float)(WidgetMetrics.RingDiameter * scale);
        var center = new PointF(left + outerDiameter / 2, height / 2f);
        var outer = new RingPass(
            Lerp(previous.Fraction, ring.Fraction, frame.ValueProgress),
            ColorBlend.Mix(
                palette.ForRingTone(previous.Tone),
                palette.ForRingTone(ring.Tone),
                frame.ToneProgress));
        DrawOneRing(graphics, palette, center, outerDiameter, stroke, outer, scale);

        if (ring.InnerFraction is not { } innerFraction || outer.Fraction >= FullFraction)
        {
            return;
        }

        var innerStroke = (float)(WidgetMetrics.InnerRingStroke * scale);
        var spacing = (float)(WidgetMetrics.RingSpacing * scale);
        var innerDiameter = outerDiameter - 2 * (stroke + spacing);
        var inner = new RingPass(
            Lerp(previous.InnerFraction ?? innerFraction, innerFraction, frame.ValueProgress),
            ColorBlend.Mix(
                palette.ForRingTone(previous.InnerTone),
                palette.ForRingTone(ring.InnerTone),
                frame.ToneProgress));
        DrawOneRing(graphics, palette, center, innerDiameter, innerStroke, inner, scale);
    }

    private readonly record struct RingPass(double Fraction, Color Color);

    /// <summary>
    /// A track with the filled share clockwise from the top. A full ring becomes a solid disc
    /// with a bar across, like a stop sign, so "full" never reads as "almost full".
    /// </summary>
    private static void DrawOneRing(
        Graphics graphics,
        WidgetPalette palette,
        PointF center,
        float diameter,
        float stroke,
        RingPass pass,
        double scale)
    {
        if (pass.Fraction >= FullFraction)
        {
            DrawStopDisc(graphics, palette, center, diameter, pass.Color, scale);
            return;
        }

        var size = diameter - stroke;
        var bounds = new RectangleF(center.X - size / 2, center.Y - size / 2, size, size);
        using var trackPen = new Pen(palette.RingTrack, stroke);
        graphics.DrawEllipse(trackPen, bounds);

        var sweep = (float)(Math.Clamp(pass.Fraction, 0, 1) * FullCircleDegrees);
        if (sweep <= 0)
        {
            return;
        }

        using var fillPen = new Pen(pass.Color, stroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        graphics.DrawArc(fillPen, bounds, TopDegrees, sweep);
    }

    private static void DrawStopDisc(
        Graphics graphics,
        WidgetPalette palette,
        PointF center,
        float diameter,
        Color color,
        double scale)
    {
        using var discBrush = new SolidBrush(color);
        graphics.FillEllipse(
            discBrush,
            center.X - diameter / 2,
            center.Y - diameter / 2,
            diameter,
            diameter);

        var barHalfWidth = diameter * WidgetMetrics.StopBarShare / 2;
        using var barPen = new Pen(palette.OnTone, (float)(WidgetMetrics.RingStroke * scale))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        graphics.DrawLine(
            barPen,
            center.X - barHalfWidth,
            center.Y,
            center.X + barHalfWidth,
            center.Y);
    }

    private static double Lerp(double from, double to, double progress) =>
        from + (to - from) * progress;

    /// <summary>Room for the take-turns dots: a gap, then one dot per account.</summary>
    private static float DotsWidth(WidgetContent content, double scale) =>
        content.TurnCount < 2
            ? 0
            : (float)((WidgetMetrics.DotsGap + content.TurnCount * WidgetMetrics.DotSize
                + (content.TurnCount - 1) * WidgetMetrics.DotSpacing) * scale);

    /// <summary>One dot per account in turn; the current account's is solid.</summary>
    private static void DrawTurnDots(
        Graphics graphics,
        WidgetContent content,
        WidgetPalette palette,
        double scale,
        float left,
        int height)
    {
        if (content.TurnCount < 2)
        {
            return;
        }

        var size = (float)(WidgetMetrics.DotSize * scale);
        var step = size + (float)(WidgetMetrics.DotSpacing * scale);
        var x = left + (float)(WidgetMetrics.DotsGap * scale);
        var y = (height - size) / 2f;
        for (var index = 0; index < content.TurnCount; index++)
        {
            var alpha = index == content.TurnIndex ? byte.MaxValue : WidgetMetrics.OtherDotAlpha;
            using var brush = new SolidBrush(Color.FromArgb(alpha, palette.SecondaryText));
            graphics.FillEllipse(brush, x + index * step, y, size, size);
        }
    }

    private static (Color From, Color To) PrimaryColors(        WidgetFrame frame,
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
    private void DrawLine(
        Graphics graphics,
        WidgetFrame frame,
        LineChange line,
        Font font,
        (Color From, Color To) colors,
        float left,
        float top)
    {
        font = FontFor(font, line.Current + line.Previous);
        var color = ColorBlend.Mix(colors.From, colors.To, frame.ToneProgress);
        if (line.Previous is null)
        {
            DrawText(graphics, line.Current, font, color, left, top);
            return;
        }

        var diff = TextDiff.Between(line.Previous, line.Current);
        if (!diff.SharesAnything)
        {
            DrawRolling(
                graphics, frame, font, colors, color, line.Previous, line.Current, left, top);
            return;
        }

        // Only the changed middle rolls; the end of the line slides to its new place.
        var prefixWidth = MeasurePiece(diff.Prefix, font);
        DrawText(graphics, diff.Prefix, font, color, left, top);
        var middleLeft = left + prefixWidth;
        DrawRolling(
            graphics, frame, font, colors, color, diff.OldMiddle, diff.NewMiddle, middleLeft, top);

        var oldWidth = MeasurePiece(diff.OldMiddle, font);
        var newWidth = MeasurePiece(diff.NewMiddle, font);
        var suffixLeft = middleLeft + oldWidth + (newWidth - oldWidth) * (float)frame.ValueProgress;
        DrawText(graphics, diff.Suffix, font, color, suffixLeft, top);
    }

    /// <summary>The old text leaves upward as the new arrives from below, or they fade.</summary>
    private static void DrawRolling(
        Graphics graphics,
        WidgetFrame frame,
        Font font,
        (Color From, Color To) colors,
        Color color,
        string leaving,
        string arriving,
        float left,
        float top)
    {
        var progress = frame.ValueProgress;
        var travel = frame.MovesText ? font.GetHeight(graphics) * WidgetMetrics.ValueTravel : 0f;
        var leavingTop = top - travel * (float)progress;
        var arrivingTop = top + travel * (float)(1 - progress);

        var leavingColor = ColorBlend.WithOpacity(colors.From, 1 - progress);
        var arrivingColor = ColorBlend.WithOpacity(color, progress);
        DrawText(graphics, leaving, font, leavingColor, left, leavingTop);
        DrawText(graphics, arriving, font, arrivingColor, left, arrivingTop);
    }

    /// <summary>Counts trailing spaces, so the piece after one is placed correctly.</summary>
    private float MeasurePiece(string text, Font font) =>
        text.Length == 0
            ? 0
            : measuringGraphics.MeasureString(text, font, int.MaxValue, PieceFormat).Width;

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
        graphics.DrawString(text, font, brush, left, top, PieceFormat);
    }

    private static StringFormat CreatePieceFormat()
    {
        var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        return format;
    }

    private SizeF Measure(string text, Font font) => measuringGraphics.MeasureString(
        text,
        FontFor(font, text),
        int.MaxValue,
        StringFormat.GenericTypographic);

    /// <summary>
    /// Thai has no glyphs in Segoe UI, and the fallback GDI+ picks looks rough, so a line
    /// with Thai in it is drawn whole in the Windows Thai UI font.
    /// </summary>
    private Font FontFor(Font font, string text)
    {
        var alreadyThai = ReferenceEquals(font, primaryThaiFont)
            || ReferenceEquals(font, secondaryThaiFont);
        if (alreadyThai || !text.Any(IsThai))
        {
            return font;
        }

        return ReferenceEquals(font, primaryFont) ? primaryThaiFont! : secondaryThaiFont!;
    }

    private static bool IsThai(char character) =>
        character >= ThaiBlockStart && character <= ThaiBlockEnd;

    private void EnsureFonts(double scale)
    {
        if (primaryFont is not null && Math.Abs(fontScale - scale) < double.Epsilon)
        {
            return;
        }

        primaryFont?.Dispose();
        secondaryFont?.Dispose();
        primaryThaiFont?.Dispose();
        secondaryThaiFont?.Dispose();
        primaryThaiFont = new Font(
            thaiFontFamily,
            (float)(WidgetMetrics.PrimaryFontPixels * scale),
            FontStyle.Bold,
            GraphicsUnit.Pixel);
        secondaryThaiFont = new Font(
            thaiFontFamily,
            (float)(WidgetMetrics.SecondaryFontPixels * scale),
            FontStyle.Regular,
            GraphicsUnit.Pixel);
        primaryFont = CreateFont((float)(WidgetMetrics.PrimaryFontPixels * scale), FontStyle.Bold);
        secondaryFont = CreateFont(
            (float)(WidgetMetrics.SecondaryFontPixels * scale),
            FontStyle.Regular);
        fontScale = scale;
    }

    private Font CreateFont(float pixels, FontStyle style) =>
        new(fontFamily, pixels, style, GraphicsUnit.Pixel);

    private static FontFamily PickFontFamily(string[] preferred)
    {
        using var installed = new InstalledFontCollection();
        var installedNames = installed.Families.Select(family => family.Name).ToHashSet();
        var name = preferred.FirstOrDefault(installedNames.Contains);
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
