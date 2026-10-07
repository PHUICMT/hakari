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
/// reset, instead of sweeping down across the chart, and where nothing was recorded (Hakari
/// was not running), which is shaded and says so. Each line ends with where it stands now,
/// a few times along the bottom say when, and hovering reads off each line at that moment.
/// </summary>
internal sealed partial class LimitTrendChart : Grid
{
    private const double ChartHeight = 160;
    private const double AxisWidth = 36;
    private const double LabelSize = 10;
    private const double LineThickness = 1.6;
    private const double ResetDrop = 5;
    private const double ValueWidth = 40;
    private const double ValueGap = 6;
    private const double ValueLineHeight = LabelSize * 1.4;
    private const int TimeTicks = 4;
    private const double GapLabelRoom = 90;
    private static readonly TimeSpan RecordingGap = TimeSpan.FromMinutes(45);
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
    private readonly Canvas values = new()
    {
        Width = ValueWidth,
        Height = ChartHeight,
        VerticalAlignment = VerticalAlignment.Bottom,
    };
    private readonly Canvas times = new() { Height = LabelSize * 1.6 };

    public LimitTrendChart(LimitTrend trend)
    {
        this.trend = trend;
        ColumnSpacing = 8;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(AxisWidth) });
        ColumnDefinitions.Add(new ColumnDefinition());
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ValueWidth) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(ChartHeight + LabelSize) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Children.Add(Axis());
        SetColumn(plot, 1);
        plot.VerticalAlignment = VerticalAlignment.Bottom;
        cursor.Background = DashboardCard.Brush("HakariInkFaintBrush");
        Children.Add(plot);
        SetColumn(values, 2);
        Children.Add(values);
        times.Margin = new Thickness(0, 6, 0, 0);
        SetColumn(times, 1);
        SetRow(times, 1);
        Children.Add(times);

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

    /// <summary>
    /// A few evenly spaced times along the bottom; a short stretch names the hours, a long
    /// one only the days.
    /// </summary>
    private void DrawTimes()
    {
        times.Children.Clear();
        var span = trend.To - trend.From;
        var isShort = span < TimeSpan.FromDays(2);
        var format = isShort ? "HH:mm" : "MMM d";
        var endFormat = isShort ? "MMM d HH:mm" : "MMM d";
        for (var tick = 0; tick <= TimeTicks; tick++)
        {
            var at = trend.From + span * tick / TimeTicks;
            var isEnd = tick == 0 || tick == TimeTicks;
            var text = new TextBlock
            {
                Text = at.ToLocalTime().ToString(isEnd ? endFormat : format, Texts.Culture),
                FontSize = LabelSize,
                Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
            };
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var width = text.DesiredSize.Width;
            var x = plot.ActualWidth * tick / TimeTicks - width / 2;
            Canvas.SetLeft(text, Math.Clamp(x, 0, Math.Max(0, plot.ActualWidth - width)));
            times.Children.Add(text);
        }
    }

    /// <summary>Where each line stands at its end, in its color, kept from overlapping.</summary>
    private void DrawValues()
    {
        values.Children.Clear();
        var placed = new List<double>();
        var latest = trend.Series
            .Where(series => series.Readings.Count > 0)
            .Select(series => (Series: series, Reading: series.Readings[^1]))
            .OrderByDescending(entry => entry.Reading.Percent);
        foreach (var (series, reading) in latest)
        {
            var top = YOf(reading.Percent) - ValueLineHeight / 2;
            foreach (var other in placed)
            {
                if (Math.Abs(other - top) < ValueLineHeight)
                {
                    top = other + ValueLineHeight;
                }
            }

            placed.Add(top);
            var label = new TextBlock
            {
                Text = PercentText.Format(reading.Percent, 0),
                FontSize = LabelSize + 1,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                LineHeight = ValueLineHeight,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                Foreground = DashboardCard.Brush(series.BrushKey),
                Margin = new Thickness(ValueGap, 0, 0, 0),
            };
            Canvas.SetTop(label, top);
            values.Children.Add(label);
        }
    }

    /// <summary>
    /// Stretches with no reading at all, when Hakari was not running: shaded, and named when
    /// wide enough, so an empty stretch is not read as zero use.
    /// </summary>
    private void DrawGaps()
    {
        var moments = trend.Series.SelectMany(series => series.Readings)
            .Select(reading => reading.At)
            .Order()
            .ToList();
        for (var index = 1; index < moments.Count; index++)
        {
            if (moments[index] - moments[index - 1] < RecordingGap)
            {
                continue;
            }

            var left = XOf(moments[index - 1]);
            var width = XOf(moments[index]) - left;
            var band = new Border
            {
                Width = width,
                Height = ChartHeight,
                Background = DashboardCard.Brush("HakariHoverBrush"),
                IsHitTestVisible = false,
            };
            if (width >= GapLabelRoom)
            {
                band.Child = new TextBlock
                {
                    Text = Texts.Get("dashboard.trend.notRecorded"),
                    FontSize = LabelSize,
                    Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                };
            }

            Canvas.SetLeft(band, left);
            plot.Children.Add(band);
        }
    }

    private double drawnWidth = -1;

    private void Redraw()
    {
        if (plot.XamlRoot is null)
        {
            return;
        }

        plot.Children.Clear();
        DrawGaps();
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
        DrawTimes();
        DrawValues();
    }

    /// <summary>
    /// Readings split where the limit started over or where nothing was recorded for a
    /// while, so the line breaks there instead of guessing across.
    /// </summary>
    private static List<List<LimitReading>> Segments(IReadOnlyList<LimitReading> readings)
    {
        var segments = new List<List<LimitReading>>();
        foreach (var reading in readings)
        {
            if (segments.Count == 0
                || segments[^1][^1].Percent - reading.Percent >= ResetDrop
                || reading.At - segments[^1][^1].At >= RecordingGap)
            {
                segments.Add([]);
            }

            segments[^1].Add(reading);
        }

        return segments;
    }

    private const double DashLength = 2.5;

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
        if (series.IsDashed)
        {
            line.StrokeDashArray = new DoubleCollection { DashLength, DashLength };
        }

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
