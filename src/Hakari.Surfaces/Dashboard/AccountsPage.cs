using Hakari.Core.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// The totals across accounts, every account Hakari has seen with its limits and what it spent,
/// the usage from before Hakari ran, and the places usage is read from.
/// </summary>
internal sealed partial class AccountsPage : LoadedPage<AccountsData>
{
    private const double SectionSpacing = 16;
    private const double MinimumCardWidth = 300;
    private const int MostCardsAcross = 3;
    private const double NameColumn = 160;
    private const double StatusColumn = 130;
    private const double FilesColumn = 70;
    private const double SizeColumn = 84;
    private const double CostColumn = 110;
    private const double AccountShownFrom = 560;
    private const double FootprintShownFrom = 700;

    public AccountsPage()
        : base("dashboard.accounts")
    {
    }

    protected override AccountsData Read(DashboardFilter filter) => AccountsData.Load(filter);

    protected override UIElement Build(AccountsData data)
    {
        var period = PeriodText.Caption();
        var page = new StackPanel { Spacing = SectionSpacing };
        page.Children.Add(AccountCards.Summary(data, period, data.Sources.Count));
        var cards = data.Accounts
            .Select(account => (FrameworkElement)AccountCards.Account(account, period))
            .ToList();
        page.Children.Add(CardGrid.Create(cards, MostCardsAcross, MinimumCardWidth));
        if (data.Earlier is { } earlier)
        {
            page.Children.Add(AccountCards.Earlier(earlier, period));
        }

        page.Children.Add(Sources(data));
        return page;
    }

    private static Border Sources(AccountsData data)
    {
        UIElement table = data.Sources.Count == 0
            ? new TextBlock
            {
                Text = Texts.Get("dashboard.empty"),
                Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
            }
            : SimpleTable.Create(Columns(), [.. data.Sources.Select(Row)]);
        return DashboardCard.Create(
            "dashboard.sources.title",
            Texts.Get("dashboard.sources.caption"),
            table);
    }

    private static List<SimpleColumn> Columns() =>
    [
        new(Texts.Get("dashboard.sources.source"), new GridLength(1, GridUnitType.Star)),
        new(Texts.Get("dashboard.sources.account"), new GridLength(NameColumn),
            ShownFrom: AccountShownFrom),
        new(Texts.Get("dashboard.sources.status"), new GridLength(StatusColumn)),
        new(Texts.Get("dashboard.sources.files"), new GridLength(FilesColumn), IsNumber: true,
            ShownFrom: FootprintShownFrom),
        new(Texts.Get("dashboard.sources.size"), new GridLength(SizeColumn), IsNumber: true,
            ShownFrom: FootprintShownFrom),
        new(Texts.Get("dashboard.sources.allTime"), new GridLength(CostColumn), IsNumber: true),
    ];

    private static IReadOnlyList<object> Row(SourceLine line) =>
    [
        line.Name,
        line.Account,
        DashboardBadge.Create(line.Status, line.IsLive ? BadgeTone.Ok : BadgeTone.Neutral),
        line.Files.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
        Core.Presentation.ByteText.Format(line.Bytes),
        line.AllTime,
    ];
}
