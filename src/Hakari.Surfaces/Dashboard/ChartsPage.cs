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
        return page;
    }

    private static Border HeatCard(ChartsData data)
    {
        var body = new StackPanel();
        body.Children.Add(new HeatMap(data.Heat, data.Currency));
        body.Children.Add(HeatScale.Create());
        return DashboardCard.Create(
            Texts.Get("dashboard.heat.title"),
            Texts.Get("dashboard.heat.caption"),
            body);
    }

    private static Border MixCard(ChartsData data)
    {
        var body = new StackPanel();
        body.Children.Add(ShareChart.Create(data.Labels, data.Mix));
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
