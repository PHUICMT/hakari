using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Cost per bar with a few labelled gridlines. Bars grow up from the base, one after
/// another, when the chart appears; the last (current) bar is drawn lighter.
/// </summary>
internal static class CostChart
{
    private const double ChartHeight = 200;
    private const double AxisWidth = 52;
    private const double BarGap = 3;
    private const double BarRadius = 2;
    private const double LabelSize = 10;
    private const int Gridlines = 4;
    private const int LabelEvery = 7;
    private const double GrowStaggerMilliseconds = 12;
    private const string ScalePath = "ScaleY";

    public static Grid Create(IReadOnlyList<(string Label, decimal Cost)> bars, string currency)
    {
        var highest = bars.Count == 0 ? 0 : (double)bars.Max(bar => bar.Cost);
        var top = NiceTop(highest);
        // Headroom above the top gridline, so its label is never clipped.
        var chart = new Grid { ColumnSpacing = 8, Padding = new Thickness(0, LabelSize, 0, 0) };
        chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(AxisWidth) });
        chart.ColumnDefinitions.Add(new ColumnDefinition());
        chart.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ChartHeight) });
        chart.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        chart.Children.Add(Axis(top, currency));
        var plot = Plot(bars, top, currency);
        Grid.SetColumn(plot, 1);
        chart.Children.Add(plot);
        var labels = Labels(bars);
        Grid.SetColumn(labels, 1);
        Grid.SetRow(labels, 1);
        chart.Children.Add(labels);
        return chart;
    }

    /// <summary>A round number above the highest bar, so the gridlines read cleanly.</summary>
    private static double NiceTop(double highest)
    {
        if (highest <= 0)
        {
            return 1;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(highest)));
        var steps = new[] { 1, 2, 2.5, 5, 10 };
        return steps.Select(step => step * magnitude).First(value => value >= highest);
    }

    private static Canvas Axis(double top, string currency) => ChartAxis.Create(
        Enumerable.Range(0, Gridlines + 1).Select(line => (
            MoneyText.Format((decimal)(top * line / Gridlines), currency),
            (double)line / Gridlines)),
        ChartHeight,
        AxisWidth,
        LabelSize);

    private static Grid Plot(
        IReadOnlyList<(string Label, decimal Cost)> bars,
        double top,
        string currency)
    {
        var plot = new Grid { ColumnSpacing = BarGap };
        for (var line = 0; line <= Gridlines; line++)
        {
            plot.Children.Add(new Border
            {
                Height = 1,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, ChartHeight * line / Gridlines),
                Background = DashboardCard.Brush("HakariLineBrush"),
            });
        }

        var storyboard = new Storyboard();
        var animate = SurfaceMotion.Current() == AnimationSetting.Full && ChartEntrance.ShouldPlay;
        for (var index = 0; index < bars.Count; index++)
        {
            plot.ColumnDefinitions.Add(new ColumnDefinition());
            var bar = Bar(bars[index], top, isCurrent: index == bars.Count - 1);
            Grid.SetColumn(bar, index);
            plot.Children.Add(bar);
            if (animate && bar.RenderTransform is ScaleTransform grow)
            {
                grow.ScaleY = 0;
                var delay = TimeSpan.FromMilliseconds(GrowStaggerMilliseconds * index);
                storyboard.Children.Add(
                    SurfaceMotion.Animate(grow, ScalePath, 0, 1, SurfaceMotion.Entrance, delay));
            }
        }

        foreach (var line in plot.Children.OfType<Border>().Take(Gridlines + 1))
        {
            Grid.SetColumnSpan(line, Math.Max(1, bars.Count));
        }

        ChartEntrance.PlayOnce(plot, storyboard);
        ChartHover.Attach(plot, bars.Count, index => (
            bars[index].Label,
            [new ReadoutRow(
                null,
                Texts.Get("dashboard.column.cost"),
                MoneyText.Format(bars[index].Cost, currency))]));
        return plot;
    }

    private static Border Bar((string Label, decimal Cost) bar, double top, bool isCurrent)
    {
        var height = top <= 0 ? 0 : ChartHeight * (double)bar.Cost / top;
        var view = new Border
        {
            Height = Math.Max(0, height),
            VerticalAlignment = VerticalAlignment.Bottom,
            CornerRadius = new CornerRadius(BarRadius, BarRadius, 0, 0),
            Background = DashboardCard.Brush(isCurrent ? "HakariChart3Brush" : "HakariChart2Brush"),
            RenderTransformOrigin = new Windows.Foundation.Point(0.5, 1),
            RenderTransform = new ScaleTransform(),
        };
        return view;
    }

    private static Grid Labels(IReadOnlyList<(string Label, decimal Cost)> bars)
    {
        var labels = new Grid { ColumnSpacing = BarGap, Margin = new Thickness(0, 6, 0, 0) };
        var every = Math.Max(1, bars.Count > LabelEvery * 2 ? LabelEvery : bars.Count / Gridlines);
        for (var index = 0; index < bars.Count; index++)
        {
            labels.ColumnDefinitions.Add(new ColumnDefinition());
            if (index % every != 0)
            {
                continue;
            }

            var label = new TextBlock
            {
                Text = bars[index].Label,
                FontSize = LabelSize,
                Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
                TextWrapping = TextWrapping.NoWrap,
            };
            Grid.SetColumn(label, index);
            Grid.SetColumnSpan(label, Math.Min(every, bars.Count - index));
            labels.Children.Add(label);
        }

        return labels;
    }
}
