using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// How much of the cost is the main conversation and how much is subagents, the helpers Claude
/// Code starts for a task. Their responses are logged apart, as sidechains.
/// </summary>
internal sealed partial class WorkflowsPage : LoadedPage<WorkflowsData>
{
    private const int PercentScale = 100;
    private const string DayFormat = "MMM d";

    public WorkflowsPage()
        : base("dashboard.workflows")
    {
    }

    protected override WorkflowsData Read(DashboardFilter filter) => WorkflowsData.Load(filter);

    protected override UIElement Build(WorkflowsData data)
    {
        var main = data.MainThread;
        var subagents = data.Subagents;
        var total = main.Cost + subagents.Cost;
        var perResponse = subagents.Messages == 0 ? 0 : subagents.Cost / subagents.Messages;
        var tiles = DashboardTiles.Create(
        [
            DashboardTile.Create(
                Texts.Get("dashboard.workflows.main"),
                MoneyText.Format(main.Cost, data.Currency),
                Texts.Format("dashboard.workflows.share", Share(main.Cost, total))),
            DashboardTile.Create(
                Texts.Get("dashboard.workflows.subagents"),
                MoneyText.Format(subagents.Cost, data.Currency),
                Texts.Format("dashboard.workflows.share", Share(subagents.Cost, total))),
            DashboardTile.Create(
                Texts.Get("dashboard.workflows.responses"),
                subagents.Messages.ToString("N0", CultureInfo.InvariantCulture),
                Texts.Format(
                    "dashboard.workflows.each",
                    MoneyText.Format(perResponse, data.Currency))),
            LargestTile(data),
        ]);
        var page = new StackPanel { Spacing = SectionSpacing };
        page.Children.Add(DashboardCard.Create(
            Texts.Get("dashboard.workflows.title"),
            Texts.Get("dashboard.workflows.caption"),
            tiles));
        if (subagents.Cost <= 0)
        {
            page.Children.Add(DashboardCard.Create(
                Texts.Get("dashboard.workflows.sessions.title"),
                null,
                Faint(Texts.Get("dashboard.workflows.none"))));
            return page;
        }

        page.Children.Add(DailyCard(data));
        page.Children.Add(CardGrid.Create(
            [SessionsCard(data), ModelsCard(data)],
            mostAcross: 2,
            minWidth: HalfCardMinimum));
        return page;
    }

    private const double SectionSpacing = 16;
    private const double HalfCardMinimum = 420;
    private const double ShareColumn = 64;
    private const double MoneyColumn = 96;
    private const double CountColumn = 72;
    private const double ShareBarWidth = 64;
    private const double ShareBarHeight = 4;

    /// <summary>Each day's cost as a bar, the main thread below and subagents above.</summary>
    private static Microsoft.UI.Xaml.Controls.Border DailyCard(WorkflowsData data)
    {
        var body = new StackPanel();
        body.Children.Add(ShareChart.Create(data.Labels, data.Daily, data.Currency));
        body.Children.Add(DashboardLegend.Create(
            data.Daily.Select(series => (series.BrushKey, series.Name))));
        return DashboardCard.Create(
            Texts.Get("dashboard.workflows.daily.title"),
            Texts.Get("dashboard.workflows.daily.caption"),
            body);
    }

    /// <summary>
    /// The sessions that leaned on subagents most: what the subagents cost and how much of
    /// the whole session that was.
    /// </summary>
    private static Microsoft.UI.Xaml.Controls.Border SessionsCard(WorkflowsData data)
    {
        List<SimpleColumn> columns =
        [
            new(Texts.Get("dashboard.workflows.column.session"),
                new GridLength(1, GridUnitType.Star)),
            new(Texts.Get("dashboard.workflows.subagents"), new GridLength(MoneyColumn),
                IsNumber: true),
            new(Texts.Get("dashboard.workflows.column.share"), new GridLength(ShareColumn),
                IsNumber: true),
            new(Texts.Get("dashboard.column.responses"), new GridLength(CountColumn),
                IsNumber: true, ShownFrom: 380),
        ];
        var rows = data.Sessions.Select(session =>
        {
            var (title, detail) = RowNames.InGroup(
                Hakari.Core.Querying.GroupBy.ProjectSession, session.Subagents, data.Titles);
            var project = Hakari.Core.Querying.GroupKeys.Split(session.Subagents.Key).Project;
            var where = RowNames.Project(project).Title;
            return (IReadOnlyList<object>)
            [
                TwoLines(title, detail is null ? where : $"{where} · {detail}"),
                MoneyText.Format(session.Subagents.Cost, data.Currency),
                Share(session.Subagents.Cost, session.SessionCost),
                session.Subagents.Messages.ToString("N0", CultureInfo.InvariantCulture),
            ];
        });
        return DashboardCard.Create(
            Texts.Get("dashboard.workflows.sessions.title"),
            Texts.Get("dashboard.workflows.sessions.caption"),
            SimpleTable.Create(columns, [.. rows]));
    }

    /// <summary>What the subagents ran on, each with a bar for its share of their cost.</summary>
    private static Microsoft.UI.Xaml.Controls.Border ModelsCard(WorkflowsData data)
    {
        var total = data.Models.Sum(model => model.Cost);
        List<SimpleColumn> columns =
        [
            new(Texts.Get("dashboard.column.model"), new GridLength(1, GridUnitType.Star)),
            new(Texts.Get("dashboard.workflows.column.share"),
                new GridLength(ShareBarWidth + ShareColumn)),
            new(Texts.Get("dashboard.column.cost"), new GridLength(MoneyColumn), IsNumber: true),
        ];
        var rows = data.Models.Select(model => (IReadOnlyList<object>)
        [
            model.Key.Length == 0 ? Texts.Get("dashboard.unknown") : model.Key,
            ShareBar(total <= 0 ? 0 : (double)(model.Cost / total)),
            MoneyText.Format(model.Cost, data.Currency),
        ]);
        return DashboardCard.Create(
            Texts.Get("dashboard.workflows.models.title"),
            Texts.Get("dashboard.workflows.models.caption"),
            SimpleTable.Create(columns, [.. rows]));
    }

    private static StackPanel ShareBar(double fraction)
    {
        var track = new Microsoft.UI.Xaml.Controls.Grid
        {
            Width = ShareBarWidth,
            Height = ShareBarHeight,
            VerticalAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(ShareBarHeight / 2),
            Background = DashboardCard.Brush("HakariLineStrongBrush"),
        };
        track.Children.Add(new Microsoft.UI.Xaml.Controls.Border
        {
            Width = Math.Max(ShareBarHeight, ShareBarWidth * Math.Clamp(fraction, 0, 1)),
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(ShareBarHeight / 2),
            Background = DashboardCard.Brush("HakariChart2Brush"),
        });
        var cell = new StackPanel
        {
            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal,
            Spacing = 8,
        };
        cell.Children.Add(track);
        cell.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
        {
            Text = PercentText.Format(fraction * PercentScale, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        return cell;
    }

    private static StackPanel TwoLines(string title, string detail)
    {
        var cell = new StackPanel();
        cell.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
        {
            Text = title,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        cell.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
        {
            Text = detail,
            FontSize = 11,
            Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        ToolTipService.SetToolTip(cell, detail);
        return cell;
    }

    private static Microsoft.UI.Xaml.Controls.TextBlock Faint(string text) => new()
    {
        Text = text,
        Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
        TextWrapping = TextWrapping.Wrap,
    };

    private static Microsoft.UI.Xaml.Controls.Border LargestTile(WorkflowsData data) =>
        DashboardTile.Create(
            Texts.Get("dashboard.workflows.largest"),
            data.Largest is { } largest
                ? MoneyText.Format(largest.Cost, data.Currency)
                : Texts.Get("dashboard.none"),
            data.Largest?.LastSeen.ToLocalTime().ToString(DayFormat, Texts.Culture));

    private static string Share(decimal part, decimal total) =>
        PercentText.Format(total <= 0 ? 0 : (double)(part / total) * PercentScale, 0);
}
