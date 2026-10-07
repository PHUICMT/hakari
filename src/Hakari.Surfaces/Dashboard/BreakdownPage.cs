using Hakari.Core.Localization;
using Hakari.Core.Querying;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// A page that splits the filtered usage by one thing (sessions, projects, branches) into a
/// table, the most expensive first.
/// </summary>
internal sealed partial class BreakdownPage : LoadedPage<BreakdownRows>
{
    private const int MaximumRows = 50;
    private const int MaximumGroupedRows = 500;

    private readonly GroupBy groupBy;
    private readonly string titleKey;

    public BreakdownPage(GroupBy groupBy, string titleKey)
        : base(titleKey)
    {
        this.groupBy = groupBy;
        this.titleKey = titleKey;
    }

    /// <summary>Runs off the UI thread.</summary>
    protected override BreakdownRows Read(DashboardFilter filter) => DashboardData.Read(
        (query, store) =>
        {
            var usage = filter.ToUsageFilter(DateTimeOffset.Now);
            return new BreakdownRows(
                query.Summarize(usage, groupBy),
                query.Total(usage).Cost,
                query.Currency,
                groupBy == GroupBy.ProjectSession ? SessionTitles.Load(store) : null);
        },
        new BreakdownRows([], 0m, "USD"));

    protected override UIElement Build(BreakdownRows data) => DashboardCard.Create(
        titleKey,
        Texts.Format("dashboard.rowCount", data.Rows.Count),
        Table(data));

    /// <summary>Branches and sessions sit under their project; other pages are flat.</summary>
    private UIElement Table(BreakdownRows data)
    {
        var (rows, total, currency) = (data.Rows, data.Total, data.Currency);
        var name = Texts.Get(titleKey);
        if (groupBy is GroupBy.ProjectBranch or GroupBy.ProjectSession)
        {
            return UsageTable.CreateGrouped(
                [.. rows.Take(MaximumGroupedRows)],
                total,
                currency,
                name,
                row => RowNames.InGroup(groupBy, row, data.Titles),
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
