using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Makes a plot of side-by-side bars answer the pointer: the bar under it is lit with a soft
/// band, and a readout says what it holds. Leaving the plot clears both.
/// </summary>
internal static class ChartHover
{
    /// <param name="plot">A grid with one column per bar.</param>
    /// <param name="describe">The readout for the bar at an index.</param>
    public static void Attach(
        Grid plot,
        int count,
        Func<int, (string Title, IReadOnlyList<ReadoutRow> Rows)> describe)
    {
        if (count <= 0)
        {
            return;
        }

        var readout = new ChartReadout();
        var band = new Border
        {
            Background = DashboardCard.Brush("HakariHoverBrush"),
            CornerRadius = new CornerRadius(2),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        plot.Background = new SolidColorBrush(Colors.Transparent);
        plot.Children.Add(band);
        plot.PointerMoved += (_, args) =>
        {
            var point = args.GetCurrentPoint(plot).Position;
            var index = Math.Clamp((int)(point.X / plot.ActualWidth * count), 0, count - 1);
            Grid.SetColumn(band, index);
            band.Visibility = Visibility.Visible;
            var (title, rows) = describe(index);
            readout.Show(plot, point, title, rows);
        };
        plot.PointerExited += (_, _) =>
        {
            band.Visibility = Visibility.Collapsed;
            readout.Hide();
        };
    }
}
