using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Querying;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// The costliest sessions: when they ran (or their title), the project, where they ran, the
/// model they leaned on, how long they lasted, responses and cost, with a "High" mark on the
/// unusual ones. A row opens its detail sheet.
/// </summary>
internal sealed partial class SessionsPage : LoadedPage<SessionRows>
{
    private const double ProjectColumn = 170;
    private const double SourceColumn = 86;
    private const double ModelColumn = 150;
    private const double DurationColumn = 90;
    private const double NumberColumn = 84;
    private const double CostColumn = 130;
    private const double PercentScale = 100;

    public SessionsPage()
        : base("dashboard.sessions")
    {
    }

    protected override SessionRows Read(DashboardFilter filter) => SessionRows.Load(filter);

    protected override UIElement Build(SessionRows data)
    {
        List<SimpleColumn> columns =
        [
            new(Texts.Get("dashboard.column.session"), new GridLength(1, GridUnitType.Star)),
            new(Texts.Get("dashboard.column.project"), new GridLength(ProjectColumn),
                ShownFrom: 760),
            new(Texts.Get("dashboard.column.source"), new GridLength(SourceColumn),
                ShownFrom: 900),
            new(Texts.Get("dashboard.column.modelMix"), new GridLength(ModelColumn),
                ShownFrom: 640),
            new(Texts.Get("dashboard.column.duration"), new GridLength(DurationColumn),
                IsNumber: true, ShownFrom: 560),
            new(Texts.Get("dashboard.column.responses"), new GridLength(NumberColumn),
                IsNumber: true, ShownFrom: 460),
            new(Texts.Get("dashboard.column.cost"), new GridLength(CostColumn), IsNumber: true),
        ];
        var table = SimpleTable.CreateSortable(
            columns,
            [.. data.Rows.Select(row => Cells(row, data))],
            [.. data.Rows.Select(Keys)]);
        TableCells.MakeRowsOpen(table, index => SessionSheet.Open(
            this, data.Rows[index], data.Currency));
        return DashboardCard.Create(
            "dashboard.sessions.title",
            Texts.Get("dashboard.sessions.caption"),
            table);
    }

    private static IReadOnlyList<object> Cells(SessionRow row, SessionRows data)
    {
        var (project, session) = GroupKeys.Split(row.Usage.Key);
        var when = When(row.Usage);
        var cost = MoneyText.Format(row.Usage.Cost, data.Currency);
        return
        [
            TableCells.TwoLines(row.Title ?? when, row.Title is null ? Short(session) : when),
            RowNames.Project(project).Title,
            row.Source is { } source ? TableCells.Badge(source) : string.Empty,
            row.TopModel is var (name, share)
                ? Model(name, PercentText.Format(share * PercentScale, 0))
                : string.Empty,
            Duration(row.Active),
            row.Usage.Messages.ToString("N0", CultureInfo.InvariantCulture),
            row.IsHigh
                ? TableCells.WithBadge(cost, TableCells.Badge(Texts.Get("dashboard.high"), true))
                : cost,
        ];
    }

    /// <summary>What each column sorts by: time, words, share, length, counts and money.</summary>
    private static IReadOnlyList<IComparable?> Keys(SessionRow row) =>
    [
        row.Usage.FirstSeen,
        RowNames.Project(GroupKeys.Split(row.Usage.Key).Project).Title,
        row.Source ?? string.Empty,
        row.TopModel?.Share ?? 0,
        row.Active,
        row.Usage.Messages,
        row.Usage.Cost,
    ];

    internal static string When(UsageSummary usage) =>
        usage.FirstSeen.ToLocalTime().ToString("MMM d · HH:mm", Texts.Culture);

    private static string Short(string session) => session.Length > 8 ? session[..8] : session;

    /// <summary>"6 h 40 m", "48 m", or "under a minute".</summary>
    internal static string Duration(TimeSpan span) => span.TotalHours >= 1
        ? Texts.Format("dashboard.duration.hours", (int)span.TotalHours, span.Minutes)
        : span.TotalMinutes >= 1
            ? Texts.Format("dashboard.duration.minutes", (int)span.TotalMinutes)
            : Texts.Get("dashboard.duration.short");

    private static StackPanel Model(string name, string share)
    {
        var cell = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        cell.Children.Add(new TextBlock
        {
            Text = name,
            FontFamily = (FontFamily)Application.Current.Resources["HakariMonoFont"],
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        cell.Children.Add(new TextBlock
        {
            Text = share,
            Foreground = DashboardCard.Brush("HakariInkMutedBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        return cell;
    }
}
