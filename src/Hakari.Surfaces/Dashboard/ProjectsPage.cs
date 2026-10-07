using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Microsoft.UI.Xaml;

namespace Hakari.Surfaces.Dashboard;

/// <summary>One project: its usage, how many sessions it had, and folders joined into it.</summary>
internal sealed record ProjectRow(UsageSummary Usage, int Sessions, int Joined);

internal sealed record ProjectRows(IReadOnlyList<ProjectRow> Rows, string Currency);

/// <summary>
/// Projects by working directory, across sources, the costliest first: sessions, responses,
/// cache hit rate and cost, as in the design.
/// </summary>
internal sealed partial class ProjectsPage : LoadedPage<ProjectRows>
{
    private const int MostRows = 50;
    private const double NumberColumn = 84;
    private const double CostColumn = 110;
    private const double PercentScale = 100;

    public ProjectsPage()
        : base("dashboard.projects")
    {
    }

    protected override ProjectRows Read(DashboardFilter filter) => DashboardData.Read(
        (query, _) =>
        {
            var usage = filter.ToUsageFilter(DateTimeOffset.Now);
            var merges = new ProjectMerges(SettingsStore.Default.Load().ProjectMerges);
            var sessions = query.Summarize(usage, GroupBy.ProjectSession)
                .GroupBy(session => GroupKeys.Split(session.Key).Project)
                .ToDictionary(group => group.Key, group => group.Count());
            return new ProjectRows(
                [
                    .. query.Summarize(usage, GroupBy.Project).Take(MostRows)
                        .Select(project => new ProjectRow(
                            project,
                            sessions.GetValueOrDefault(project.Key),
                            merges.JoinedInto(project.Key).Count)),
                ],
                query.Currency);
        },
        new ProjectRows([], "USD"));

    protected override UIElement Build(ProjectRows data)
    {
        List<SimpleColumn> columns =
        [
            new(Texts.Get("dashboard.projects"), new GridLength(1, GridUnitType.Star)),
            new(Texts.Get("dashboard.column.sessions"), new GridLength(NumberColumn),
                IsNumber: true, ShownFrom: 520),
            new(Texts.Get("dashboard.column.responses"), new GridLength(NumberColumn),
                IsNumber: true),
            new(Texts.Get("dashboard.column.hitRate"), new GridLength(NumberColumn),
                IsNumber: true, ShownFrom: 440),
            new(Texts.Get("dashboard.column.cost"), new GridLength(CostColumn), IsNumber: true),
        ];
        var rows = data.Rows.Select(row =>
        {
            var (title, detail) = RowNames.Project(row.Usage.Key);
            if (row.Joined > 0)
            {
                detail += " · " + Texts.Format("dashboard.merge.count", row.Joined);
            }

            return (IReadOnlyList<object>)
            [
                TableCells.TwoLines(title, detail),
                row.Sessions.ToString("N0", CultureInfo.InvariantCulture),
                row.Usage.Messages.ToString("N0", CultureInfo.InvariantCulture),
                PercentText.Format(row.Usage.Tokens.CacheHitRate * PercentScale, 0),
                MoneyText.Format(row.Usage.Cost, data.Currency),
            ];
        });
        var table = SimpleTable.CreateSortable(columns, [.. rows], [.. data.Rows.Select(row =>
            (IReadOnlyList<IComparable?>)
            [
                RowNames.Project(row.Usage.Key).Title,
                row.Sessions,
                row.Usage.Messages,
                row.Usage.Tokens.CacheHitRate,
                row.Usage.Cost,
            ])]);
        var keys = data.Rows.Select(row => row.Usage.Key).ToList();
        TableCells.MakeRowsOpen(table, (index, row) =>
            ProjectMergeMenu.Show(row, keys[index], keys, Reload));
        return DashboardCard.Create(
            Texts.Get("dashboard.projects"),
            Texts.Get("dashboard.projects.caption"),
            table);
    }
}
