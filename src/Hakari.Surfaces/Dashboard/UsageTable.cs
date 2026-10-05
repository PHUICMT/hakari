using System.Collections.ObjectModel;
using Hakari.Core.Localization;
using Hakari.Core.Querying;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Usage tables: name, responses, tokens and cost with a share bar, flat or grouped under
/// projects. Rows are virtualized, so only those on screen exist; a long list scrolls and
/// folds as lightly as a short one.
/// </summary>
internal static class UsageTable
{
    public static UIElement Create(
        IReadOnlyList<UsageSummary> rows,
        decimal totalCost,
        string currency,
        string nameHeader,
        Func<UsageSummary, (string Title, string? Detail)> nameOf)
    {
        if (rows.Count == 0)
        {
            return EmptyText();
        }

        var items = new ObservableCollection<TableItem>(
            rows.Select(row => new TableItem(row, nameOf(row))));
        return new TableView(items, totalCost, currency, nameHeader);
    }

    /// <summary>
    /// Rows gathered under their project, each project a header with its own totals that
    /// folds its rows away. The most expensive project comes first; all start folded.
    /// </summary>
    public static UIElement CreateGrouped(
        IReadOnlyList<UsageSummary> rows,
        decimal totalCost,
        string currency,
        string nameHeader,
        Func<UsageSummary, (string Title, string? Detail)> nameOf,
        Func<UsageSummary, string> groupOf,
        Func<string, (string Title, string? Detail)> groupNameOf)
    {
        if (rows.Count == 0)
        {
            return EmptyText();
        }

        var groups = rows.GroupBy(groupOf)
            .Select(group => Group(group.Key, [.. group], nameOf, groupNameOf))
            .OrderByDescending(group => group.Summary.Cost);
        return new TableView(
            new ObservableCollection<TableItem>(groups),
            totalCost,
            currency,
            nameHeader);
    }

    private static TableItem Group(
        string key,
        List<UsageSummary> rows,
        Func<UsageSummary, (string Title, string? Detail)> nameOf,
        Func<string, (string Title, string? Detail)> groupNameOf)
    {
        var total = rows.Aggregate(
            UsageSummary.Empty with { Key = key },
            (sum, row) => sum.Merge(row));
        var (title, detail) = groupNameOf(key);
        var children = rows
            .Select(row => new TableItem(row, nameOf(row), IsIndented: true))
            .ToList();
        var groupDetail = Texts.Format("dashboard.groupDetail", rows.Count, detail ?? string.Empty);
        return new TableItem(total, (title, groupDetail), children);
    }

    private static TextBlock EmptyText() => new()
    {
        Text = Texts.Get("dashboard.empty"),
        Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
    };
}
