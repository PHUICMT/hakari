using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// Lays its children out left to right and starts a new line only between them, so a short
/// phrase such as "updated a moment ago" is never split. Thai has no spaces between words, so
/// a text block alone would break inside a phrase.
/// </summary>
public sealed partial class FlowPanel : Panel
{
    public double Spacing { get; set; } = 4;

    public double LineSpacing { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        var lineWidth = 0.0;
        var lineHeight = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            var size = child.DesiredSize;
            if (lineWidth > 0 && lineWidth + Spacing + size.Width > availableSize.Width)
            {
                width = Math.Max(width, lineWidth);
                height += lineHeight + LineSpacing;
                lineWidth = 0;
                lineHeight = 0;
            }

            lineWidth += (lineWidth > 0 ? Spacing : 0) + size.Width;
            lineHeight = Math.Max(lineHeight, size.Height);
        }

        return new Size(Math.Max(width, lineWidth), height + lineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        var y = 0.0;
        var lineHeight = 0.0;
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            if (x > 0 && x + Spacing + size.Width > finalSize.Width)
            {
                x = 0;
                y += lineHeight + LineSpacing;
                lineHeight = 0;
            }

            var left = x > 0 ? x + Spacing : 0;
            child.Arrange(new Rect(left, y, Math.Min(size.Width, finalSize.Width), size.Height));
            x = left + size.Width;
            lineHeight = Math.Max(lineHeight, size.Height);
        }

        return finalSize;
    }
}
