using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// A chart's value labels, each centred on its gridline. Drawn on a canvas, which never cuts
/// a child off, so the top label can stand half above the plot instead of losing its top.
/// </summary>
internal static class ChartAxis
{
    private const double LineHeightFactor = 1.4;

    /// <param name="labels">Each label and its height as a share of the plot, 0 to 1.</param>
    public static Canvas Create(
        IEnumerable<(string Text, double Fraction)> labels,
        double plotHeight,
        double width,
        double labelSize)
    {
        var lineHeight = labelSize * LineHeightFactor;
        var axis = new Canvas
        {
            Width = width,
            Height = plotHeight,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        foreach (var (text, fraction) in labels)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = labelSize,
                LineHeight = lineHeight,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                Width = width,
                TextAlignment = TextAlignment.Right,
                Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
            };
            Canvas.SetTop(label, plotHeight * (1 - fraction) - lineHeight / 2);
            axis.Children.Add(label);
        }

        return axis;
    }
}
