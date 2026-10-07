using Hakari.Core.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>When in the week the money is spent, and how the models share it over time.</summary>
internal sealed partial class ChartsPage : LoadedPage<ChartsData>
{
    private const double SectionSpacing = 16;

    public ChartsPage()
        : base("dashboard.charts")
    {
    }

    protected override ChartsData Read(DashboardFilter filter) => ChartsData.Load(filter);

    protected override UIElement Build(ChartsData data)
    {
        var page = new StackPanel { Spacing = SectionSpacing };
        page.Children.Add(HeatCard(data));
        page.Children.Add(MixCard(data));
        page.Children.Add(TrendCard(data));
        return page;
    }

    private static Border HeatCard(ChartsData data)
    {
        var body = new StackPanel();
        body.Children.Add(ChartOrTable.Create(
            new HeatMap(data.Heat, data.Currency),
            () => ChartOrTable.Week(data.Heat, data.Currency)));
        body.Children.Add(HeatScale.Create());
        return DashboardCard.Create(
            Texts.Get("dashboard.heat.title"),
            Texts.Get("dashboard.heat.caption"),
            body);
    }

    private const double TrendSpacing = 12;
    private const string TrendFoldPrefix = "trend:";

    private static Grid AccountHeader(string accountName, FrameworkElement chart)
    {
        var header = new Grid
        {
            ColumnSpacing = 4,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Transparent),
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition());
        var name = new TextBlock
        {
            Text = accountName,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = DashboardCard.Brush("HakariInkMutedBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(name, 1);
        header.Children.Add(name);
        header.Children.Add(DashboardFold.Attach(header, chart, TrendFoldPrefix + accountName));
        return header;
    }

    private static Border TrendCard(ChartsData data)
    {
        var body = new StackPanel { Spacing = TrendSpacing };
        var trends = data.Trends ?? [];
        foreach (var trend in trends)
        {
            var chart = ChartOrTable.Create(
                new LimitTrendChart(trend), () => ChartOrTable.Readings(trend));

            // With several accounts each chart is named and folds on its own; one account is
            // named in the caption.
            if (trends.Count > 1)
            {
                body.Children.Add(AccountHeader(trend.AccountName, chart));
            }

            body.Children.Add(chart);
        }

        if (trends.Count > 0)
        {
            body.Children.Add(DashboardLegend.CreateLines(trends[0].Series
                .Select(series => (series.BrushKey, series.Name, series.IsDashed))));
        }
        else
        {
            body.Children.Add(new TextBlock
            {
                Text = Texts.Get("dashboard.trend.empty"),
                Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        return DashboardCard.Create(
            Texts.Get("dashboard.trend.title"),
            trends.Count == 1
                ? Texts.Format("dashboard.trend.caption", trends[0].AccountName)
                : trends.Count > 1 ? Texts.Get("dashboard.trend.captionAll") : null,
            body);
    }

    private static Border MixCard(ChartsData data)
    {
        var body = new StackPanel();
        body.Children.Add(ChartOrTable.Create(
            ShareChart.Create(data.Labels, data.Mix, data.Currency),
            () => ChartOrTable.Shares(data.Labels, data.Mix, data.Currency)));
        body.Children.Add(DashboardLegend.Create(
            data.Mix.Select(series => (
                series.BrushKey,
                series.Name.Length == 0 ? Texts.Get("dashboard.mix.other") : series.Name))));
        return DashboardCard.Create(
            Texts.Get("dashboard.mix.title"),
            Texts.Get("dashboard.mix.caption"),
            body);
    }
}
