using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

internal static class DashboardHeader
{
    private const double OneLineWidth = 820;
    private const double TwoLineBarWidth = 600;
    private const double WrappedGap = 12;

    /// <summary>The filter bar sits beside the title, or under it when narrow.</summary>
    public static void WrapWhenNarrow(Grid header, DashboardFilterBar filterBar)
    {
        header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        WidthSteps.Watch(header, [TwoLineBarWidth, OneLineWidth], level =>
        {
            var oneLine = level >= 2;
            filterBar.SetTwoLines(level == 0);
            Grid.SetRow(filterBar, oneLine ? 0 : 1);
            filterBar.HorizontalAlignment = oneLine
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;
            filterBar.Margin = new Thickness(0, oneLine ? 0 : WrappedGap, 0, 0);
        });
    }
}