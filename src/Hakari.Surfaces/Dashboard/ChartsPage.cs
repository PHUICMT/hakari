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

    private static Border TrendCard(ChartsData data)
    {
        var body = new StackPanel { Spacing = TrendSpacing };
        var trends = data.Trends ?? [];
        foreach (var trend in trends)
        {
            // With several accounts each chart is named; one account is named in the caption.
            if (trends.Count > 1)
            {
                body.Children.Add(new TextBlock
                {
                    Text = trend.AccountName,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = DashboardCard.Brush("HakariInkMutedBrush"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
            }

            body.Children.Add(ChartOrTable.Create(
                new LimitTrendChart(trend), () => ChartOrTable.Readings(trend)));
        }

        if (trends.Count > 0)
        {
            body.Children.Add(DashboardLegend.Create(
                trends[0].Series.Select(series => (series.BrushKey, series.Name))));
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
