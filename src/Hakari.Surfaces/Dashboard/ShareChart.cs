using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Stacked bars that each fill the same height, so what shows is each series' share of that
/// bar, as with the models in a day's cost. Bars grow up from the base, one after another.
/// </summary>
internal static class ShareChart
{
    private const double ChartHeight = 160;
    private const double AxisWidth = 36;
    private const double BarGap = 2;
    private const double LabelSize = 10;
    private const int LabelEvery = 7;
    private const double GrowStaggerMilliseconds = 12;
    private static readonly int[] AxisSteps = [0, 50, 100];

    public static Grid Create(IReadOnlyList<string> labels, IReadOnlyList<MixSeries> series)
    {
        var chart = new Grid { ColumnSpacing = 8, Padding = new Thickness(0, LabelSize, 0, 0) };
        chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(AxisWidth) });
        chart.ColumnDefinitions.Add(new ColumnDefinition());
        chart.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ChartHeight) });
        chart.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        chart.Children.Add(Axis());
        var plot = Plot(labels.Count, series);
        Grid.SetColumn(plot, 1);
        chart.Children.Add(plot);
        var captions = Captions(labels);
        Grid.SetColumn(captions, 1);
        Grid.SetRow(captions, 1);
        chart.Children.Add(captions);
        return chart;
    }

    private static Grid Axis()
    {
        var axis = new Grid();
        foreach (var step in AxisSteps)
        {
            axis.Children.Add(new TextBlock
            {
                Text = $"{step}%",
                FontSize = LabelSize,
                Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, ChartHeight * step / 100 - LabelSize / 2),
            });
        }

        return axis;
    }

    private static Grid Plot(int bars, IReadOnlyList<MixSeries> series)
    {
        var plot = new Grid { ColumnSpacing = BarGap };
        foreach (var step in AxisSteps)
        {
            var line = new Border
            {
                Height = 1,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, (ChartHeight - 1) * step / 100),
                Background = DashboardCard.Brush("HakariLineBrush"),
            };
            Grid.SetColumnSpan(line, Math.Max(1, bars));
            plot.Children.Add(line);
        }

        var storyboard = new Storyboard();
        var animate = SurfaceMotion.Current() == AnimationSetting.Full;
        for (var index = 0; index < bars; index++)
        {
            plot.ColumnDefinitions.Add(new ColumnDefinition());
            var bar = Bar(index, series);
            Grid.SetColumn(bar, index);
            plot.Children.Add(bar);
            if (animate && bar.RenderTransform is ScaleTransform grow)
            {
                grow.ScaleY = 0;
                var delay = TimeSpan.FromMilliseconds(GrowStaggerMilliseconds * index);
                storyboard.Children.Add(
                    SurfaceMotion.Animate(grow, "ScaleY", 0, 1, SurfaceMotion.Entrance, delay));
            }
        }

        plot.Loaded += (_, _) => storyboard.Begin();
        return plot;
    }

    /// <summary>The first series sits at the bottom, as in the legend's order.</summary>
    private static Grid Bar(int index, IReadOnlyList<MixSeries> series)
    {
        var bar = new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            RenderTransformOrigin = new Windows.Foundation.Point(0.5, 1),
            RenderTransform = new ScaleTransform(),
        };
        var total = series.Sum(part => (double)part.Costs[index]);
        if (total <= 0)
        {
            return bar;
        }

        var row = 0;
        foreach (var part in series.Reverse())
        {
            var share = (double)part.Costs[index] / total;
            if (share <= 0)
            {
                continue;
            }

            bar.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(share, GridUnitType.Star),
            });
            var piece = new Border { Background = DashboardCard.Brush(part.BrushKey) };
            Grid.SetRow(piece, row++);
            bar.Children.Add(piece);
        }

        return bar;
    }

    private static Grid Captions(IReadOnlyList<string> labels)
    {
        var captions = new Grid { ColumnSpacing = BarGap, Margin = new Thickness(0, 6, 0, 0) };
        var every = Math.Max(1, labels.Count > LabelEvery * 2 ? LabelEvery : labels.Count / 4);
        for (var index = 0; index < labels.Count; index++)
        {
            captions.ColumnDefinitions.Add(new ColumnDefinition());
            if (index % every != 0)
            {
                continue;
            }

            var label = new TextBlock
            {
                Text = labels[index],
                FontSize = LabelSize,
                Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
                TextWrapping = TextWrapping.NoWrap,
            };
            Grid.SetColumn(label, index);
            Grid.SetColumnSpan(label, Math.Min(every, labels.Count - index));
            captions.Children.Add(label);
        }

        return captions;
    }
}
