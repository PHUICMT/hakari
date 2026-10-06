using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Cost by weekday and hour, one square per hour. The darker the square, the more was spent
/// then. Squares stay square whatever the width, and appear a column at a time.
/// </summary>
internal sealed partial class HeatMap : Grid
{
    private const double LabelWidth = 34;
    private const double Gap = 3;
    private const double LabelSize = 10;
    private const double CellRadius = 2;
    private const double OutlineWidth = 1.5;
    private const int LabelEveryHours = 6;
    private const double StaggerMilliseconds = 14;
    private static readonly double[] LevelFloors = [0.15, 0.4, 0.7];

    private readonly List<RowDefinition> cellRows = [];
    private readonly ChartReadout readout = new();

    public HeatMap(IReadOnlyList<decimal> costs, string currency)
    {
        ColumnSpacing = Gap;
        RowSpacing = Gap;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelWidth) });
        for (var hour = 0; hour < ChartsData.Hours; hour++)
        {
            ColumnDefinitions.Add(new ColumnDefinition());
        }

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddHourLabels();
        var highest = costs.Count == 0 ? 0m : costs.Max();
        var storyboard = new Storyboard();
        var animate = SurfaceMotion.Current() == AnimationSetting.Full;
        for (var day = 0; day < ChartsData.Weekdays; day++)
        {
            var row = new RowDefinition { Height = GridLength.Auto };
            cellRows.Add(row);
            RowDefinitions.Add(row);
            AddDay(day, costs, highest, currency, storyboard, animate);
        }

        SizeChanged += (_, args) => FitSquares(args.NewSize.Width);
        Loaded += (_, _) => storyboard.Begin();
    }

    /// <summary>How many shades there are: the lightest for nothing, then four darker.</summary>
    public static int LevelCount => LevelFloors.Length + 2;

    public static string LevelBrush(int level) => $"HakariHeat{level}Brush";

    private void AddHourLabels()
    {
        for (var hour = 0; hour < ChartsData.Hours; hour += LabelEveryHours)
        {
            var label = new TextBlock
            {
                Text = hour.ToString(),
                FontSize = LabelSize,
                Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            SetColumn(label, hour + 1);
            SetColumnSpan(label, LabelEveryHours);
            Children.Add(label);
        }
    }

    private void AddDay(
        int day,
        IReadOnlyList<decimal> costs,
        decimal highest,
        string currency,
        Storyboard storyboard,
        bool animate)
    {
        var dayName = DayName(day);
        var name = new TextBlock
        {
            Text = dayName,
            FontSize = LabelSize,
            Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        SetRow(name, day + 1);
        Children.Add(name);
        for (var hour = 0; hour < ChartsData.Hours; hour++)
        {
            var index = day * ChartsData.Hours + hour;
            var cost = costs.Count > index ? costs[index] : 0m;
            var cell = new Border
            {
                CornerRadius = new CornerRadius(CellRadius),
                Background = DashboardCard.Brush(LevelBrush(LevelOf(cost, highest))),
            };
            Hover(cell, $"{dayName} {hour:00}:00", MoneyText.Format(cost, currency));
            SetColumn(cell, hour + 1);
            SetRow(cell, day + 1);
            Children.Add(cell);
            if (animate)
            {
                cell.Opacity = 0;
                var delay = TimeSpan.FromMilliseconds(StaggerMilliseconds * hour);
                storyboard.Children.Add(SurfaceMotion.Animate(
                    cell, "Opacity", 0, 1, SurfaceMotion.Normal, delay));
            }
        }
    }

    /// <summary>The square under the pointer gets an outline and a readout of its cost.</summary>
    private void Hover(Border cell, string when, string money)
    {
        cell.PointerEntered += (_, args) =>
        {
            cell.BorderBrush = DashboardCard.Brush("HakariInkBrush");
            cell.BorderThickness = new Thickness(OutlineWidth);
            readout.Show(
                cell,
                args.GetCurrentPoint(cell).Position,
                when,
                [new ReadoutRow(null, Texts.Get("dashboard.column.cost"), money)]);
        };
        cell.PointerExited += (_, _) =>
        {
            cell.BorderThickness = new Thickness(0);
            readout.Hide();
        };
    }

    /// <summary>Nothing spent is the lightest; the rest spread over the four darker.</summary>
    private static int LevelOf(decimal cost, decimal highest)
    {
        if (cost <= 0 || highest <= 0)
        {
            return 0;
        }

        var share = (double)(cost / highest);
        return 1 + LevelFloors.Count(floor => share >= floor);
    }

    /// <summary>Monday first, in the language of the app.</summary>
    private static string DayName(int day) =>
        Texts.Culture.DateTimeFormat.AbbreviatedDayNames[(day + 1) % ChartsData.Weekdays];

    private void FitSquares(double width)
    {
        var side = (width - LabelWidth - Gap * ChartsData.Hours) / ChartsData.Hours;
        if (side <= 0)
        {
            return;
        }

        foreach (var row in cellRows)
        {
            row.Height = new GridLength(side);
        }
    }
}
