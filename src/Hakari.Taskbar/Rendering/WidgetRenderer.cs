using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Hakari.Taskbar.Rendering;

public sealed class WidgetRenderer : IDisposable
{
    private readonly FontFamily fontFamily = PickFontFamily();
    private Font? primaryFont;
    private Font? secondaryFont;
    private double fontScale;

    /// <summary>Draws the widget into a premultiplied-alpha bitmap the caller disposes.</summary>
    public Bitmap Render(
        WidgetContent content,
        WidgetPalette palette,
        double scale,
        bool hovered)
    {
        EnsureFonts(scale);
        var primarySize = Measure(content.PrimaryText, primaryFont!);
        var secondarySize = Measure(content.SecondaryText, secondaryFont!);

        var padding = (float)(WidgetMetrics.HorizontalPadding * scale);
        var textWidth = Math.Max(primarySize.Width, secondarySize.Width);
        var width = (int)Math.Ceiling(textWidth + padding * 2);
        var height = (int)Math.Round(WidgetMetrics.Height * scale);

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        DrawBackground(graphics, palette, width, height, scale, hovered);

        var lineGap = (float)(WidgetMetrics.LineGap * scale);
        var textHeight = primarySize.Height + lineGap + secondarySize.Height;
        var top = (height - textHeight) / 2f;
        DrawText(graphics, content.PrimaryText, primaryFont!, palette.PrimaryText, padding, top);
        var secondaryColor = palette.ForTone(content.SecondaryTone);
        var secondaryTop = top + primarySize.Height + lineGap;
        DrawText(
            graphics,
            content.SecondaryText,
            secondaryFont!,
            secondaryColor,
            padding,
            secondaryTop);
        return bitmap;
    }

    public void Dispose()
    {
        primaryFont?.Dispose();
        secondaryFont?.Dispose();
        fontFamily.Dispose();
    }

    private static void DrawBackground(
        Graphics graphics,
        WidgetPalette palette,
        int width,
        int height,
        double scale,
        bool hovered)
    {
        var fill = hovered
            ? palette.HoverFill
            : Color.FromArgb(WidgetMetrics.ClickableBackgroundAlpha, palette.PrimaryText);
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
        using var brush = new SolidBrush(color);
        graphics.DrawString(text, font, brush, left, top, StringFormat.GenericTypographic);
    }

    private static SizeF Measure(string text, Font font)
    {
        using var probe = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(probe);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        return graphics.MeasureString(text, font, int.MaxValue, StringFormat.GenericTypographic);
    }

    private void EnsureFonts(double scale)
    {
        if (primaryFont is not null && Math.Abs(fontScale - scale) < double.Epsilon)
        {
            return;
        }

        primaryFont?.Dispose();
        secondaryFont?.Dispose();
        var primaryPixels = (float)(WidgetMetrics.PrimaryFontPixels * scale);
        var secondaryPixels = (float)(WidgetMetrics.SecondaryFontPixels * scale);
        primaryFont = CreateFont(primaryPixels, FontStyle.Bold);
        secondaryFont = CreateFont(secondaryPixels, FontStyle.Regular);
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
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
