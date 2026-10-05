using Hakari.Core.Localization;
using Hakari.Core.Querying;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// A page that splits the filtered usage by one thing (sessions, projects, branches) into a
/// table, the most expensive first.
/// </summary>
internal sealed partial class BreakdownPage : UserControl
{
    private const double PageTitleSize = 26;
    private const double SectionSpacing = 16;
    private const int MaximumRows = 50;
    private const int MaximumGroupedRows = 500;
    private static readonly Thickness PagePadding = new(24, 20, 24, 28);

    private readonly GroupBy groupBy;
    private readonly string titleKey;
    private readonly DashboardFilterBar filterBar = new();
    private readonly ContentControl body = new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        IsTabStop = false,
    };

    public BreakdownPage(GroupBy groupBy, string titleKey)
    {
        this.groupBy = groupBy;
        this.titleKey = titleKey;
        var content = new StackPanel { Spacing = SectionSpacing, Padding = PagePadding };
        var header = new Grid();
        header.Children.Add(new TextBlock
        {
            Text = Texts.Get(titleKey),
            FontSize = PageTitleSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardCard.Brush("HakariInkBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(filterBar);
        DashboardHeader.WrapWhenNarrow(header, filterBar);
        content.Children.Add(header);
        content.Children.Add(body);
        Content = new ScrollViewer { Content = content };
        filterBar.Changed += (_, _) => Refresh();
        Loaded += (_, _) => Refresh();
    }

    public void Refresh()
    {
        var (rows, total, currency) = DashboardData.Read(
            (query, _) =>
            {
                var filter = DashboardFilter.Current.ToUsageFilter(DateTimeOffset.Now);
                return (query.Summarize(filter, groupBy), query.Total(filter).Cost, query.Currency);
            },
            ((IReadOnlyList<UsageSummary>)[], 0m, "USD"));
        body.Content = DashboardCard.Create(
            Texts.Get(titleKey),
            Texts.Format("dashboard.rowCount", rows.Count),
            Table(rows, total, currency));
    }

    /// <summary>Branches and sessions sit under their project; other pages are flat.</summary>
    private UIElement Table(IReadOnlyList<UsageSummary> rows, decimal total, string currency)
    {
        var name = Texts.Get(titleKey);
        if (groupBy is GroupBy.ProjectBranch or GroupBy.ProjectSession)
        {
            return UsageTable.CreateGrouped(
                [.. rows.Take(MaximumGroupedRows)],
                total,
                currency,
                name,
                row => RowNames.InGroup(groupBy, row),
                row => GroupKeys.Split(row.Key).Project,
                RowNames.Project);
        }

        return UsageTable.Create(
            [.. rows.Take(MaximumRows)],
            total,
            currency,
            name,
            row => RowNames.Of(groupBy, row));
    }
}
