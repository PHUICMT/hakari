using System.Globalization;
using Hakari.Core.Limits;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Surfaces.Controls;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Every chart can be read as a table instead, for a screen reader or for exact figures: a
/// small button under the chart swaps one for the other, cross-fading. The table is built
/// only when first asked for.
/// </summary>
internal static class ChartOrTable
{
    private const double PercentScale = 100;
    private const double LabelColumn = 140;
    private const double FigureColumn = 110;
    private const string TimeFormat = "MMM d HH:mm";

    public static StackPanel Create(FrameworkElement chart, Func<FrameworkElement> table)
    {
        var host = new ContentControl
        {
            Content = chart,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsTabStop = false,
        };
        FrameworkElement? built = null;
        var showingTable = false;
        var button = new Button
        {
            Style = (Style)Application.Current.Resources["HakariSubtleButton"],
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0),
            Content = Texts.Get("dashboard.asTable"),
        };
        button.Click += (_, _) =>
        {
            showingTable = !showingTable;
            built ??= Scrolling(table());
            host.Content = showingTable ? built : chart;
            button.Content = Texts.Get(showingTable ? "dashboard.asChart" : "dashboard.asTable");
            if (SurfaceMotion.Current() != Core.Settings.AnimationSetting.Off)
            {
                host.Opacity = 0;
                SurfaceMotion.Settle(host, "Opacity", 1);
            }
        };
        var panel = new StackPanel();
        panel.Children.Add(host);
        panel.Children.Add(button);
        return panel;
    }

    private const double TableMostHeight = 320;

    /// <summary>A long table scrolls within its own box instead of stretching the page.</summary>
    private static ScrollViewer Scrolling(FrameworkElement table) => new()
    {
        Content = table,
        MaxHeight = TableMostHeight,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };

    /// <summary>One row per bar: its label and its cost.</summary>
    public static FrameworkElement Costs(
        IReadOnlyList<(string Label, decimal Cost)> bars,
        string currency) =>
        SimpleTable.CreateSortable(
            [
                new(Texts.Get("dashboard.column.when"), new GridLength(1, GridUnitType.Star)),
                new(Texts.Get("dashboard.column.cost"), new GridLength(FigureColumn),
                    IsNumber: true),
            ],
            [.. bars.Select(bar => (IReadOnlyList<object>)
                [bar.Label, MoneyText.Format(bar.Cost, currency)])],
            [.. bars.Select((bar, index) => (IReadOnlyList<IComparable?>) [index, bar.Cost])]);

    /// <summary>One row per bar, a column per series, and the bar's total.</summary>
    public static FrameworkElement Shares(
        IReadOnlyList<string> labels,
        IReadOnlyList<MixSeries> series,
        string currency)
    {
        List<SimpleColumn> columns =
        [
            new(Texts.Get("dashboard.column.when"), new GridLength(LabelColumn)),
            .. series.Select(part => new SimpleColumn(
                part.Name.Length == 0 ? Texts.Get("dashboard.mix.other") : part.Name,
                new GridLength(1, GridUnitType.Star),
                IsNumber: true)),
            new(Texts.Get("dashboard.column.total"), new GridLength(FigureColumn), IsNumber: true),
        ];
        var rows = labels.Select((label, index) =>
        {
            var total = series.Sum(part => part.Costs[index]);
            return (IReadOnlyList<object>)
            [
                label,
                .. series.Select(part => (object)MoneyText.Format(part.Costs[index], currency)),
                MoneyText.Format(total, currency),
            ];
        });
        var keys = labels.Select((_, index) => (IReadOnlyList<IComparable?>)
        [
            index,
            .. series.Select(part => (IComparable?)part.Costs[index]),
            series.Sum(part => part.Costs[index]),
        ]);
        return SimpleTable.CreateSortable(columns, [.. rows], [.. keys]);
    }

    /// <summary>Each weekday: what it cost, its busiest hour, and what that hour cost.</summary>
    public static FrameworkElement Week(IReadOnlyList<decimal> heat, string currency)
    {
        var days = Enumerable.Range(0, ChartsData.Weekdays).Select(day =>
        {
            var hours = heat.Skip(day * ChartsData.Hours).Take(ChartsData.Hours).ToList();
            var busiest = hours.IndexOf(hours.Max());
            return (Day: HeatMap.DayName(day), Total: hours.Sum(), Hour: busiest,
                Peak: hours[busiest]);
        }).ToList();
        return SimpleTable.CreateSortable(
            [
                new(Texts.Get("dashboard.column.day"), new GridLength(1, GridUnitType.Star)),
                new(Texts.Get("dashboard.column.cost"), new GridLength(FigureColumn),
                    IsNumber: true),
                new(Texts.Get("dashboard.column.busiestHour"), new GridLength(FigureColumn),
                    IsNumber: true),
                new(Texts.Get("dashboard.column.hourCost"), new GridLength(FigureColumn),
                    IsNumber: true),
            ],
            [.. days.Select(day => (IReadOnlyList<object>)
            [
                day.Day,
                MoneyText.Format(day.Total, currency),
                $"{day.Hour:00}:00",
                MoneyText.Format(day.Peak, currency),
            ])],
            [.. days.Select((day, index) => (IReadOnlyList<IComparable?>)
                [index, day.Total, day.Hour, day.Peak])]);
    }

    /// <summary>Every reading of every limit: when, which limit, how full.</summary>
    public static FrameworkElement Readings(LimitTrend trend)
    {
        var readings = trend.Series
            .SelectMany(series => series.Readings.Select(reading => (series.Name, reading)))
            .OrderByDescending(entry => entry.reading.At)
            .ToList();
        return SimpleTable.CreateSortable(
            [
                new(Texts.Get("dashboard.column.when"), new GridLength(1, GridUnitType.Star)),
                new(Texts.Get("dashboard.column.limit"), new GridLength(LabelColumn)),
                new(Texts.Get("dashboard.column.used"), new GridLength(FigureColumn),
                    IsNumber: true),
            ],
            [.. readings.Select(entry => (IReadOnlyList<object>)
            [
                entry.reading.At.ToLocalTime().ToString(TimeFormat, Texts.Culture),
                entry.Name,
                PercentText.Format(entry.reading.Percent, 0),
            ])],
            [.. readings.Select(entry => (IReadOnlyList<IComparable?>)
                [entry.reading.At, entry.Name, entry.reading.Percent])]);
    }
}
