using Hakari.Core.Limits;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// How full each limit was over time, as lines from 0 to 100%. A line breaks where a limit
/// reset, instead of sweeping down across the chart. Hovering reads off each line at that
/// moment.
/// </summary>
internal sealed partial class LimitTrendChart : Grid
{
    private const double ChartHeight = 160;
    private const double AxisWidth = 36;
    private const double LabelSize = 10;
    private const double LineThickness = 1.6;
    private const double ResetDrop = 5;
    private static readonly int[] AxisSteps = [0, 50, 100];

    private readonly LimitTrend trend;
    private readonly Canvas plot = new()
    {
        Height = ChartHeight,
        Background = new SolidColorBrush(Colors.Transparent),
    };
    private readonly Border cursor = new()
    {
        Width = 1,
        Height = ChartHeight,
        Visibility = Visibility.Collapsed,
        IsHitTestVisible = false,
    };

    private readonly ChartReadout readout = new();

    public LimitTrendChart(LimitTrend trend)
    {
        this.trend = trend;
        ColumnSpacing = 8;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(AxisWidth) });
        ColumnDefinitions.Add(new ColumnDefinition());
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(ChartHeight + LabelSize) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Children.Add(Axis());
        SetColumn(plot, 1);
        plot.VerticalAlignment = VerticalAlignment.Bottom;
        cursor.Background = DashboardCard.Brush("HakariInkFaintBrush");
        Children.Add(plot);
        var ends = Ends();
        SetColumn(ends, 1);
        SetRow(ends, 1);
        Children.Add(ends);

        // Redrawn after layout, not inside it, and only when the width really changed: a
        // redraw from within the size pass can start a layout cycle that Windows ends hard.
        plot.SizeChanged += (_, args) =>
        {
            if (Math.Abs(args.NewSize.Width - drawnWidth) < 1)
            {
                return;
            }

            drawnWidth = args.NewSize.Width;
            DispatcherQueue.TryEnqueue(Redraw);
        };
        plot.PointerMoved += (_, args) => Hover(args.GetCurrentPoint(plot).Position);
        plot.PointerExited += (_, _) =>
        {
            cursor.Visibility = Visibility.Collapsed;
            readout.Hide();
        };
    }

    private Canvas Axis() => ChartAxis.Create(
        AxisSteps.Select(step => ($"{step}%", step / 100.0)),
        ChartHeight,
        AxisWidth,
        LabelSize);

    private Grid Ends()
    {
        var ends = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        ends.Children.Add(Caption(trend.From, HorizontalAlignment.Left));
        ends.Children.Add(Caption(trend.To, HorizontalAlignment.Right));
        return ends;
    }

    private static TextBlock Caption(DateTimeOffset at, HorizontalAlignment alignment) => new()
    {
        Text = at.ToLocalTime().ToString("MMM d", Texts.Culture),
        FontSize = LabelSize,
        Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
        HorizontalAlignment = alignment,
    };

    private double drawnWidth = -1;

    private void Redraw()
    {
        if (plot.XamlRoot is null)
        {
            return;
        }

        plot.Children.Clear();
        foreach (var step in AxisSteps)
        {
            var y = ChartHeight - ChartHeight * step / 100;
            var line = new Border
            {
                Width = plot.ActualWidth,
                Height = 1,
                Background = DashboardCard.Brush("HakariLineBrush"),
            };
            Canvas.SetTop(line, Math.Min(y, ChartHeight - 1));
            plot.Children.Add(line);
        }

        foreach (var series in trend.Series)
        {
            foreach (var segment in Segments(series.Readings))
            {
                plot.Children.Add(Line(series, segment));
            }
        }

        Canvas.SetTop(cursor, 0);
        plot.Children.Add(cursor);
    }

    /// <summary>Readings split where the limit started over, so the line breaks there.</summary>
    private static List<List<LimitReading>> Segments(IReadOnlyList<LimitReading> readings)
    {
        var segments = new List<List<LimitReading>>();
        foreach (var reading in readings)
        {
            if (segments.Count == 0 || segments[^1][^1].Percent - reading.Percent >= ResetDrop)
            {
                segments.Add([]);
            }

            segments[^1].Add(reading);
        }

        return segments;
    }

    private Polyline Line(TrendSeries series, List<LimitReading> segment)
    {
        var line = new Polyline
        {
            Stroke = DashboardCard.Brush(series.BrushKey),
            StrokeThickness = LineThickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        foreach (var reading in segment)
        {
            line.Points.Add(new Point(XOf(reading.At), YOf(reading.Percent)));
        }

        return line;
    }

    private double XOf(DateTimeOffset at)
    {
        var span = (trend.To - trend.From).TotalSeconds;
        return span <= 0 ? 0 : (at - trend.From).TotalSeconds / span * plot.ActualWidth;
    }

    private static double YOf(double percent) =>
        ChartHeight - Math.Clamp(percent, 0, 100) / 100 * ChartHeight;

    private void Hover(Point at)
    {
        var span = (trend.To - trend.From).TotalSeconds;
        if (plot.ActualWidth <= 0 || span <= 0)
        {
            return;
        }

        var moment = trend.From.AddSeconds(at.X / plot.ActualWidth * span);
        cursor.Visibility = Visibility.Visible;
        Canvas.SetLeft(cursor, Math.Clamp(at.X, 0, plot.ActualWidth));
        var rows = trend.Series
            .Select(series => (series, reading: LatestAt(series.Readings, moment)))
            .Where(entry => entry.reading is not null)
            .Select(entry => new ReadoutRow(
                entry.series.BrushKey,
                entry.series.Name,
                PercentText.Format(entry.reading!.Percent, 0)))
            .ToList();
        if (rows.Count > 0)
        {
            readout.Show(
                plot,
                at,
                moment.ToLocalTime().ToString("MMM d HH:mm", Texts.Culture),
                rows);
        }
    }

    /// <summary>The last reading at or before a moment, within a day of it.</summary>
    private static LimitReading? LatestAt(
        IReadOnlyList<LimitReading> readings,
        DateTimeOffset moment)
    {
        var found = readings.LastOrDefault(reading => reading.At <= moment);
        return found is not null && moment - found.At < TimeSpan.FromDays(1) ? found : null;
    }
}
