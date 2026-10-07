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
    private const int MostColumns = 2;
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
            // At most two columns: wider entries are cut short, with the whole in the tooltip.
            var span = Math.Clamp(
                (int)Math.Ceiling(item.DesiredSize.Width / ItemWidth), 1, MostColumns);
            VariableSizedWrapGrid.SetColumnSpan(item, span);
            item.MaxWidth = ItemWidth * span;
            legend.Children.Add(item);
        }

        return legend;
    }

    private const double LineSampleWidth = 18;
    private const double LineSampleThickness = 2;
    private const double DashLength = 2;

    /// <summary>
    /// For line charts: each entry shows a short stretch of its line, solid or dashed as
    /// drawn, so lines are told apart by more than color.
    /// </summary>
    public static VariableSizedWrapGrid CreateLines(
        IEnumerable<(string BrushKey, string Text, bool IsDashed)> items)
    {
        var legend = Create(items.Select(item => (item.BrushKey, item.Text)));
        foreach (var (item, entry) in legend.Children.OfType<Grid>().Zip(items))
        {
            var sample = new Microsoft.UI.Xaml.Shapes.Line
            {
                X2 = LineSampleWidth,
                Stroke = DashboardCard.Brush(entry.BrushKey),
                StrokeThickness = LineSampleThickness,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (entry.IsDashed)
            {
                sample.StrokeDashArray = new Microsoft.UI.Xaml.Media.DoubleCollection
                {
                    DashLength,
                    DashLength,
                };
            }

            item.Children[0] = sample;
        }

        return legend;
    }

    /// <summary>A grid, not a row, so a capped width cuts the text short.</summary>
    private static Grid Item(string brushKey, string text)
    {
        var item = new Grid { ColumnSpacing = 8 };
        item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        item.ColumnDefinitions.Add(new ColumnDefinition());
        item.Children.Add(new Border
        {
            Width = Swatch,
            Height = Swatch,
            CornerRadius = new CornerRadius(2),
            Background = DashboardCard.Brush(brushKey),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var label = new TextBlock
        {
            Text = text,
            FontSize = TextSize,
            Foreground = DashboardCard.Brush("HakariInkMutedBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(label, 1);
        item.Children.Add(label);
        ToolTipService.SetToolTip(item, text);
        return item;
    }
}
