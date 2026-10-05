using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Placeholders shaped like the page that is loading: cards holding grey "bones" where text,
/// numbers, bars and rows will be. The bones brighten in a wave that runs down the page, so
/// it reads as working rather than stuck. Off draws them still.
/// </summary>
internal static class LoadingSkeleton
{
    private const double Spacing = 16;
    private const double BoneHeight = 10;
    private const double TitleBoneWidth = 120;
    private const double ValueBoneHeight = 22;
    private const double BarHeight = 10;
    private const double ChartHeight = 200;
    private const int TileCount = 4;
    private const int TableRows = 8;
    private const int ChartBars = 24;
    private const double PulseLow = 0.35;
    private const double PulseHigh = 0.85;
    private static readonly TimeSpan PulseHalf = TimeSpan.FromMilliseconds(650);
    private static readonly TimeSpan WaveStep = TimeSpan.FromMilliseconds(70);
    private static readonly Thickness CardPadding = new(16, 14, 16, 16);

    public static StackPanel Overview()
    {
        var bones = new List<FrameworkElement>();
        var page = new StackPanel { Spacing = Spacing };
        page.Children.Add(Tiles(bones));
        page.Children.Add(Card(bones, MoneyBody(bones)));
        var lower = new Grid { ColumnSpacing = Spacing };
        lower.ColumnDefinitions.Add(Star(ChartShare));
        lower.ColumnDefinitions.Add(Star(TableShare));
        lower.Children.Add(Card(bones, ChartBody(bones)));
        var table = Card(bones, RowsBody(bones, TableRows / 2));
        Grid.SetColumn(table, 1);
        lower.Children.Add(table);
        page.Children.Add(lower);
        Wave(page, bones);
        return page;
    }

    public static StackPanel Table()
    {
        var bones = new List<FrameworkElement>();
        var page = new StackPanel { Spacing = Spacing };
        page.Children.Add(Card(bones, RowsBody(bones, TableRows)));
        Wave(page, bones);
        return page;
    }

    private const double ChartShare = 3;
    private const double TableShare = 2;

    private static ColumnDefinition Star(double share) =>
        new() { Width = new GridLength(share, GridUnitType.Star) };

    private static Grid Tiles(List<FrameworkElement> bones)
    {
        var tiles = new Grid { ColumnSpacing = 12 };
        for (var index = 0; index < TileCount; index++)
        {
            tiles.ColumnDefinitions.Add(new ColumnDefinition());
            var body = new StackPanel { Spacing = 10 };
            body.Children.Add(Bone(bones, 80));
            body.Children.Add(Bone(bones, 110, ValueBoneHeight));
            body.Children.Add(Bone(bones, 140));
            var tile = Frame(body);
            Grid.SetColumn(tile, index);
            tiles.Children.Add(tile);
        }

        return tiles;
    }

    private static StackPanel MoneyBody(List<FrameworkElement> bones)
    {
        var body = new StackPanel { Spacing = 12 };
        var bar = Bone(bones, double.NaN, BarHeight);
        bar.HorizontalAlignment = HorizontalAlignment.Stretch;
        body.Children.Add(bar);
        var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        for (var item = 0; item < 3; item++)
        {
            legend.Children.Add(Bone(bones, 150));
        }

        body.Children.Add(legend);
        return body;
    }

    /// <summary>Bars of varying height, like a chart about to appear.</summary>
    private static Grid ChartBody(List<FrameworkElement> bones)
    {
        var chart = new Grid { Height = ChartHeight, ColumnSpacing = 4 };
        for (var index = 0; index < ChartBars; index++)
        {
            chart.ColumnDefinitions.Add(new ColumnDefinition());
            var share = 0.25 + 0.6 * Math.Abs(Math.Sin(index * 0.7));
            var bar = Bone(bones, double.NaN, ChartHeight * share);
            bar.HorizontalAlignment = HorizontalAlignment.Stretch;
            bar.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetColumn(bar, index);
            chart.Children.Add(bar);
        }

        return chart;
    }

    /// <summary>Table rows: a name with a second line, then a number and a cost.</summary>
    private static StackPanel RowsBody(List<FrameworkElement> bones, int rows)
    {
        var body = new StackPanel { Spacing = 18 };
        for (var row = 0; row < rows; row++)
        {
            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition());
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            var name = new StackPanel { Spacing = 6 };
            name.Children.Add(Bone(bones, 140 + row % 3 * 40));
            name.Children.Add(Bone(bones, 220, BoneHeight * 0.8));
            var number = Bone(bones, 50);
            number.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(number, 1);
            var cost = Bone(bones, 90);
            cost.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(cost, 2);
            line.Children.Add(name);
            line.Children.Add(number);
            line.Children.Add(cost);
            body.Children.Add(line);
        }

        return body;
    }

    private static Border Card(List<FrameworkElement> bones, UIElement body)
    {
        var stack = new StackPanel { Spacing = 16 };
        stack.Children.Add(Bone(bones, TitleBoneWidth));
        stack.Children.Add(body);
        return Frame(stack);
    }

    private static Border Frame(UIElement child) => new()
    {
        Style = (Style)Application.Current.Resources["HakariCard"],
        Padding = CardPadding,
        Child = child,
    };

    private static Border Bone(
        List<FrameworkElement> bones,
        double width,
        double height = BoneHeight)
    {
        var bone = new Border
        {
            Width = width,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(Math.Min(height / 2, 4)),
            Background = DashboardCard.Brush("HakariLineStrongBrush"),
            Opacity = PulseLow,
        };
        bones.Add(bone);
        return bone;
    }

    /// <summary>Each bone pulses a little after the one before it, so a wave runs down.</summary>
    private static void Wave(FrameworkElement page, List<FrameworkElement> bones)
    {
        if (SurfaceMotion.Current() == AnimationSetting.Off)
        {
            return;
        }

        var storyboard = new Storyboard();
        for (var index = 0; index < bones.Count; index++)
        {
            var pulse = new DoubleAnimation
            {
                From = PulseLow,
                To = PulseHigh,
                Duration = PulseHalf,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = WaveStep * (index % 12),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            Storyboard.SetTarget(pulse, bones[index]);
            Storyboard.SetTargetProperty(pulse, "Opacity");
            storyboard.Children.Add(pulse);
        }

        page.Loaded += (_, _) => storyboard.Begin();
        page.Unloaded += (_, _) => storyboard.Stop();
    }
}
