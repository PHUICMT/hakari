using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Querying;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Dashboard;

internal sealed record BranchRows(IReadOnlyList<UsageSummary> Rows, string Currency);

/// <summary>
/// The git branch each response was made on, with its project beside it, one flat list, the
/// costliest first. Every project has its own "main", so the project tells them apart.
/// </summary>
internal sealed partial class BranchesPage : LoadedPage<BranchRows>
{
    private const int MostRows = 100;
    private const double ProjectColumn = 220;
    private const double NumberColumn = 90;
    private const double CostColumn = 110;

    public BranchesPage()
        : base("dashboard.branches")
    {
    }

    protected override BranchRows Read(DashboardFilter filter) => DashboardData.Read(
        (query, _) => new BranchRows(
            [
                .. query.Summarize(filter.ToUsageFilter(DateTimeOffset.Now), GroupBy.ProjectBranch)
                    .Take(MostRows),
            ],
            query.Currency),
        new BranchRows([], "USD"));

    protected override UIElement Build(BranchRows data)
    {
        List<SimpleColumn> columns =
        [
            new(Texts.Get("dashboard.column.branch"), new GridLength(1, GridUnitType.Star)),
            new(Texts.Get("dashboard.column.project"), new GridLength(ProjectColumn),
                ShownFrom: 480),
            new(Texts.Get("dashboard.column.responses"), new GridLength(NumberColumn),
                IsNumber: true),
            new(Texts.Get("dashboard.column.cost"), new GridLength(CostColumn), IsNumber: true),
        ];
        var rows = data.Rows.Select(row =>
        {
            var (project, branch) = GroupKeys.Split(row.Key);
            return (IReadOnlyList<object>)
            [
                Mono(branch.Length == 0 ? Texts.Get("dashboard.noBranch") : branch),
                RowNames.Project(project).Title,
                row.Messages.ToString("N0", CultureInfo.InvariantCulture),
                MoneyText.Format(row.Cost, data.Currency),
            ];
        });
        return DashboardCard.Create(
            Texts.Get("dashboard.branches"),
            Texts.Get("dashboard.branches.caption"),
            SimpleTable.CreateSortable(columns, [.. rows], [.. data.Rows.Select(row =>
            {
                var (project, branch) = GroupKeys.Split(row.Key);
                return (IReadOnlyList<IComparable?>)
                [
                    branch,
                    RowNames.Project(project).Title,
                    row.Messages,
                    row.Cost,
                ];
            })]));
    }

    private static TextBlock Mono(string text) => new()
    {
        Text = text,
        FontFamily = (FontFamily)Application.Current.Resources["HakariMonoFont"],
        TextTrimming = TextTrimming.CharacterEllipsis,
    };
}
