using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Colored squares with what they stand for, wrapping onto lines as needed. A long entry takes
/// as many columns as it needs; cut short in a narrow window, its tooltip says it in full.
/// </summary>
internal static class DashboardLegend
{
    private const double ItemWidth = 220;
    private const double Swatch = 10;
    private const double TextSize = 12;
    private static readonly Thickness LegendMargin = new(0, 12, 0, 0);
    private static readonly Windows.Foundation.Size Unbounded =
        new(double.PositiveInfinity, double.PositiveInfinity);

    public static VariableSizedWrapGrid Create(IEnumerable<(string BrushKey, string Text)> items)
    {
        var legend = new VariableSizedWrapGrid
        {
            Orientation = Orientation.Horizontal,
            ItemWidth = ItemWidth,
            Margin = LegendMargin,
        };
        foreach (var (brushKey, text) in items)
        {
            var item = Item(brushKey, text);
            item.Measure(Unbounded);
            var span = (int)Math.Ceiling(item.DesiredSize.Width / ItemWidth);
            VariableSizedWrapGrid.SetColumnSpan(item, Math.Max(1, span));
            legend.Children.Add(item);
        }

        return legend;
    }

    private static StackPanel Item(string brushKey, string text)
    {
        var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        item.Children.Add(new Border
        {
            Width = Swatch,
            Height = Swatch,
            CornerRadius = new CornerRadius(2),
            Background = DashboardCard.Brush(brushKey),
            VerticalAlignment = VerticalAlignment.Center,
        });
        item.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = TextSize,
            Foreground = DashboardCard.Brush("HakariInkMutedBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        ToolTipService.SetToolTip(item, text);
        return item;
    }
}
