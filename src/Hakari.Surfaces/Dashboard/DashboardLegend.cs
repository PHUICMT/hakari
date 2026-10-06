using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>Colored squares with what they stand for, wrapping onto lines as needed.</summary>
internal static class DashboardLegend
{
    private const double ItemWidth = 220;
    private const double Swatch = 10;
    private const double TextSize = 12;
    private static readonly Thickness LegendMargin = new(0, 12, 0, 0);

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
            legend.Children.Add(Item(brushKey, text));
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
        });
        return item;
    }
}
