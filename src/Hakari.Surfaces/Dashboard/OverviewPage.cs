using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Pricing;
using Hakari.Core.Presentation;
using Hakari.Core.Querying;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// The dashboard's first page: cost for the period against the one before, responses, cache
/// and output tokens, where the money goes by token type, cost per day, and cost by model.
/// </summary>
internal sealed partial class OverviewPage : UserControl
{
    private const double PageTitleSize = 26;
    private const double SectionSpacing = 16;
    private const double TileSpacing = 12;
    private const double ShareBarHeight = 10;
    private const double ShareGap = 2;
    private const int PercentScale = 100;
    private static readonly Thickness PagePadding = new(24, 20, 24, 28);

    private static readonly (TokenKind Kind, string TextKey, string BrushKey)[] Kinds =
    [
        (TokenKind.CacheRead, "dashboard.kind.cacheRead", "HakariChart1Brush"),
        (TokenKind.CacheWriteOneHour, "dashboard.kind.cacheWriteHour", "HakariChart2Brush"),
        (TokenKind.Output, "dashboard.kind.output", "HakariChart3Brush"),
        (TokenKind.CacheWriteFiveMinutes, "dashboard.kind.cacheWriteFive", "HakariChart4Brush"),
        (TokenKind.Input, "dashboard.kind.input", "HakariChart5Brush"),
    ];

    private readonly StackPanel content = new() { Spacing = SectionSpacing, Padding = PagePadding };
    private readonly DashboardFilterBar filterBar = new();
    private readonly PageLoader<OverviewData> loader;
    private readonly ContentControl body = new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        IsTabStop = false,
    };

    public OverviewPage()
    {
        content.Children.Add(Header());
        content.Children.Add(body);
        Content = new ScrollViewer { Content = content };
        loader = new PageLoader<OverviewData>(body, OverviewData.Load, Build);
        filterBar.Changed += (_, _) => loader.Load(force: true);
        loader.Load();
    }

    /// <summary>Shows the current filter's numbers, reading again only when stale.</summary>
    public void Refresh() => loader.Load();

    private Grid Header()
    {
        var header = new Grid();
        header.Children.Add(new TextBlock
        {
            Text = Texts.Get("dashboard.overview"),
            FontSize = PageTitleSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardCard.Brush("HakariInkBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(filterBar);
        DashboardHeader.WrapWhenNarrow(header, filterBar);
        return header;
    }

    private static StackPanel Build(OverviewData data)
    {
        var page = new StackPanel { Spacing = SectionSpacing };
        page.Children.Add(Tiles(data));
        page.Children.Add(MoneyCard(data));
        var lower = new Grid { ColumnSpacing = SectionSpacing };
        lower.ColumnDefinitions.Add(StarColumn(ChartShare));
        lower.ColumnDefinitions.Add(StarColumn(TableShare));
        var chart = DashboardCard.Create(
            Texts.Get("dashboard.dailyCost"),
            PeriodCaption(),
            CostChart.Create(data.Timeline, data.Currency));
        var models = DashboardCard.Create(
            Texts.Get("dashboard.byModel"),
            Texts.Get("dashboard.costShare"),
            ModelTable(data));
        lower.Children.Add(chart);
        lower.Children.Add(models);
        StackWhenNarrow(lower, chart, models);
        page.Children.Add(lower);
        return page;
    }

    private const double ChartShare = 3;
    private const double TableShare = 2;

    private static ColumnDefinition StarColumn(double share) =>
        new() { Width = new GridLength(share, GridUnitType.Star) };

    /// <summary>Side by side when there is room; the table goes under the chart when not.</summary>
    private static void StackWhenNarrow(Grid lower, FrameworkElement chart, FrameworkElement models)
    {
        lower.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        lower.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        WidthSteps.Watch(lower, [SideBySideWidth], level =>
        {
            var sideBySide = level >= 1;
            lower.ColumnDefinitions[1].Width = sideBySide
                ? new GridLength(TableShare, GridUnitType.Star)
                : new GridLength(0);
            lower.ColumnSpacing = sideBySide ? SectionSpacing : 0;
            lower.RowSpacing = sideBySide ? 0 : SectionSpacing;
            Grid.SetColumn(models, sideBySide ? 1 : 0);
            Grid.SetRow(models, sideBySide ? 0 : 1);
        });
    }

    private const double SideBySideWidth = 980;
    private const double FourTilesWidth = 760;

    private static Grid Tiles(OverviewData data)
    {
        var total = data.Total;
        var tokens = total.Tokens;
        var tiles = new Grid { ColumnSpacing = TileSpacing };
        var values = new[]
        {
            DashboardTile.Create(
                Texts.Format("dashboard.tile.cost", PeriodCaption()),
                MoneyText.Format(total.Cost, data.Currency),
                ChangeText(total.Cost, data.PreviousCost),
                ChangeTone(total.Cost, data.PreviousCost)),
            DashboardTile.Create(
                Texts.Get("dashboard.tile.responses"),
                total.Messages.ToString("N0", CultureInfo.InvariantCulture),
                Texts.Format(
                    "dashboard.tile.perDay",
                    TokenText.Format(total.Messages / data.DayCount))),
            DashboardTile.Create(
                Texts.Get("dashboard.tile.cacheHit"),
                PercentText.Format(tokens.CacheHitRate * PercentScale, 0),
                Texts.Format(
                    "dashboard.tile.cacheDetail",
                    TokenText.Format(tokens.CacheRead),
                    TokenText.Format(tokens.CacheWrite))),
            DashboardTile.Create(
                Texts.Get("dashboard.tile.output"),
                TokenText.Format(tokens.Output),
                Texts.Format(
                    "dashboard.tile.perResponse",
                    TokenText.Format(total.Messages == 0 ? 0 : tokens.Output / total.Messages))),
        };
        tiles.RowSpacing = TileSpacing;
        foreach (var tile in values)
        {
            tiles.ColumnDefinitions.Add(new ColumnDefinition());
            tiles.Children.Add(tile);
        }

        tiles.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        tiles.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Four across when there is room, two by two when not.
        WidthSteps.Watch(tiles, [FourTilesWidth], level =>
        {
            var across = level >= 1 ? values.Length : values.Length / 2;
            for (var index = 0; index < values.Length; index++)
            {
                Grid.SetColumn(values[index], index % across);
                Grid.SetRow(values[index], index / across);
                tiles.ColumnDefinitions[index].Width = index < across
                    ? new GridLength(1, GridUnitType.Star)
                    : new GridLength(0);
            }
        });
        return tiles;
    }

    private static string? ChangeText(decimal cost, decimal? previous)
    {
        if (previous is not { } before || before <= 0)
        {
            return null;
        }

        var change = (double)((cost - before) / before) * PercentScale;
        var arrow = change >= 0 ? "▲" : "▼";
        return Texts.Format(
            "dashboard.tile.vsPrevious",
            $"{arrow} {Math.Abs(change).ToString("F0", CultureInfo.InvariantCulture)}%",
            PeriodCaption());
    }

    /// <summary>Spending more reads as a warning, less as good news.</summary>
    private static string? ChangeTone(decimal cost, decimal? previous) =>
        previous is { } before && before > 0
            ? cost > before ? "HakariCriticalBrush" : "HakariOkBrush"
            : null;

    private static Border MoneyCard(OverviewData data)
    {
        var bar = new Grid { Height = ShareBarHeight, ColumnSpacing = ShareGap };
        var legend = new VariableSizedWrapGrid
        {
            Orientation = Orientation.Horizontal,
            ItemWidth = LegendItemWidth,
            Margin = new Thickness(0, 12, 0, 0),
        };
        var column = 0;
        foreach (var share in data.Shares.Where(share => share.Share > 0))
        {
            var (_, textKey, brushKey) = Kinds.First(kind => kind.Kind == share.Kind);
            bar.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(share.Share, GridUnitType.Star),
            });
            var piece = new Border
            {
                Background = DashboardCard.Brush(brushKey),
                CornerRadius = new CornerRadius(ShareBarHeight / 2),
            };
            Grid.SetColumn(piece, column++);
            bar.Children.Add(piece);
            legend.Children.Add(LegendItem(
                brushKey,
                Texts.Get(textKey),
                MoneyText.Format(data.Total.Cost * (decimal)share.Share, data.Currency),
                PercentText.Format(share.Share * PercentScale, 0)));
        }

        var stack = new StackPanel();
        stack.Children.Add(bar);
        stack.Children.Add(legend);
        return DashboardCard.Create(
            Texts.Get("dashboard.whereMoneyGoes"),
            PeriodCaption(),
            stack);
    }

    private const double LegendItemWidth = 220;
    private const double LegendSwatch = 10;

    private static StackPanel LegendItem(string brushKey, string name, string money, string share)
    {
        var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        item.Children.Add(new Border
        {
            Width = LegendSwatch,
            Height = LegendSwatch,
            CornerRadius = new CornerRadius(2),
            Background = DashboardCard.Brush(brushKey),
            VerticalAlignment = VerticalAlignment.Center,
        });
        item.Children.Add(new TextBlock
        {
            Text = $"{name} {money} · {share}",
            FontSize = 12,
            Foreground = DashboardCard.Brush("HakariInkMutedBrush"),
        });
        return item;
    }

    private static UIElement ModelTable(OverviewData data) =>
        UsageTable.Create(
            data.ByModel,
            data.Total.Cost,
            data.Currency,
            Texts.Get("dashboard.column.model"),
            row => (row.Key, null));

    private static string PeriodCaption() => Texts.Get(DashboardFilter.Current.Period switch
    {
        DashboardPeriod.Today => "dashboard.period.today",
        DashboardPeriod.SevenDays => "dashboard.period.week",
        DashboardPeriod.ThirtyDays => "dashboard.period.month",
        _ => "dashboard.period.all",
    });
}
