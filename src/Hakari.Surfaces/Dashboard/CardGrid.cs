using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Cards of different heights in as many columns as fit, each at least a minimum width. Every
/// card goes to the column with the least on it so far, so the columns end about level and no
/// gap opens under a short card. Rearranges only when the number of columns changes.
/// </summary>
internal static class CardGrid
{
    private const double Gap = 12;

    /// <param name="cards">Each card with a rough height, such as its number of lines.</param>
    public static Grid Create(
        IReadOnlyList<(FrameworkElement Card, double Weight)> cards,
        int mostAcross,
        double minWidth)
    {
        var grid = new Grid { ColumnSpacing = Gap };
        var across = 0;
        grid.SizeChanged += (_, args) =>
        {
            var fit = (int)Math.Clamp((args.NewSize.Width + Gap) / (minWidth + Gap), 1, mostAcross);
            if (fit != across)
            {
                across = fit;
                Arrange(grid, cards, across);
            }
        };
        return grid;
    }

    private static void Arrange(
        Grid grid,
        IReadOnlyList<(FrameworkElement Card, double Weight)> cards,
        int across)
    {
        foreach (var column in grid.Children.OfType<StackPanel>())
        {
            column.Children.Clear();
        }

        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        var columns = new List<StackPanel>();
        var loads = new double[across];
        for (var index = 0; index < across; index++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var column = new StackPanel { Spacing = Gap };
            Grid.SetColumn(column, index);
            grid.Children.Add(column);
            columns.Add(column);
        }

        foreach (var (card, weight) in cards)
        {
            var lightest = Array.IndexOf(loads, loads.Min());
            columns[lightest].Children.Add(card);
            loads[lightest] += weight;
        }
    }
}
