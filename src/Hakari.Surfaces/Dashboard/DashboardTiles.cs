using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>Headline tiles in a row, or two by two when the page is narrow.</summary>
internal static class DashboardTiles
{
    private const double Spacing = 12;
    private const double FourAcrossWidth = 760;

    public static Grid Create(IReadOnlyList<FrameworkElement> tiles)
    {
        var grid = new Grid { ColumnSpacing = Spacing, RowSpacing = Spacing };
        foreach (var tile in tiles)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(tile);
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        WidthSteps.Watch(grid, [FourAcrossWidth], level =>
        {
            var across = level >= 1 ? tiles.Count : tiles.Count / 2;
            for (var index = 0; index < tiles.Count; index++)
            {
                Grid.SetColumn(tiles[index], index % across);
                Grid.SetRow(tiles[index], index / across);
                grid.ColumnDefinitions[index].Width = index < across
                    ? new GridLength(1, GridUnitType.Star)
                    : new GridLength(0);
            }
        });
        return grid;
    }
}
