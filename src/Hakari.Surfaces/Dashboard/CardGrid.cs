using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Cards in rows of as many as fit, each at least a minimum width, otherwise one under
/// another. Cards in a row are as tall as the tallest, so their edges line up. Rearranges only
/// when the number that fit changes.
/// </summary>
internal static class CardGrid
{
    private const double Gap = 12;

    public static Grid Create(
        IReadOnlyList<FrameworkElement> cards,
        int mostAcross,
        double minWidth)
    {
        var grid = new Grid { ColumnSpacing = Gap, RowSpacing = Gap };
        foreach (var card in cards)
        {
            card.VerticalAlignment = VerticalAlignment.Stretch;
            grid.Children.Add(card);
        }

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

    private static void Arrange(Grid grid, IReadOnlyList<FrameworkElement> cards, int across)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        for (var column = 0; column < across; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        for (var row = 0; row < (cards.Count + across - 1) / across; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var index = 0; index < cards.Count; index++)
        {
            Grid.SetColumn(cards[index], index % across);
            Grid.SetRow(cards[index], index / across);
        }
    }
}
