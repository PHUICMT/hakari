using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Whether plans pay for themselves. Across the accounts: what this month's usage would cost
/// per token, what the plans cost, what is saved, and where the month is heading. Then a card
/// per account with its months, a verdict, and when it passed the plan's price.
/// </summary>
internal sealed partial class PlanValuePage : LoadedPage<PlanValueData>
{
    private const double SectionSpacing = 16;
    private const double MinimumCardWidth = 420;
    private const int MostCardsAcross = 2;
    private const int PercentScale = 100;

    public PlanValuePage()
        : base("dashboard.planValue")
    {
    }

    protected override PlanValueData Read(DashboardFilter filter) => PlanValueData.Load(filter);

    protected override UIElement Build(PlanValueData data)
    {
        var page = new StackPanel { Spacing = SectionSpacing };
        if (data.Accounts.Count == 0 && data.Combined is null && data.Unassigned is null)
        {
            page.Children.Add(DashboardCard.Create(
                "dashboard.planValue.title",
                Texts.Get("dashboard.planValue.caption"),
                new TextBlock
                {
                    Text = Texts.Get("dashboard.empty"),
                    Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
                }));
            return page;
        }

        page.Children.Add(Tiles(data));
        if (data.Combined is { } combined)
        {
            page.Children.Add(PlanCards.Account(combined, data.Currency, data));
        }

        var cards = data.Accounts
            .Select(account => (FrameworkElement)PlanCards.Account(account, data.Currency, data))
            .ToList();
        if (cards.Count > 0)
        {
            page.Children.Add(CardGrid.Create(cards, MostCardsAcross, MinimumCardWidth));
        }

        if (data.Unassigned is { } earlier)
        {
            page.Children.Add(PlanCards.Account(earlier, data.Currency, data));
        }

        page.Children.Add(Note(data.Unassigned is not null));
        return page;
    }

    private static Grid Tiles(PlanValueData data)
    {
        var month = data.Today.ToString("MMMM", Texts.Culture);
        var priced = data.Accounts.Where(account => account.Price is not null).ToList();
        var cost = data.Combined?.ThisMonth.Cost
            ?? data.Accounts.Sum(account => account.ThisMonth.Cost);
        var before = data.Combined?.Months[1].Cost
            ?? data.Accounts.Sum(account => account.Months.ElementAtOrDefault(1)?.Cost ?? 0);
        var paid = priced.Sum(account => account.Price!.Value);
        var pricedCost = data.Combined is null
            ? priced.Sum(account => account.ThisMonth.Cost)
            : cost;
        var net = pricedCost - paid;
        var projected = cost / data.Today.Day * data.DaysInMonth;
        return DashboardTiles.Create(
        [
            DashboardTile.Create(
                Texts.Format("dashboard.planValue.tile.cost", month),
                MoneyText.Format(cost, data.Currency),
                Change(cost, before),
                before > 0 ? (cost > before ? "HakariCriticalBrush" : "HakariOkBrush") : null),
            DashboardTile.Create(
                Texts.Get("dashboard.planValue.tile.paid"),
                MoneyText.Format(paid, data.Currency),
                Texts.Format("dashboard.planValue.tile.plans", priced.Count)),
            DashboardTile.Create(
                Texts.Get("dashboard.planValue.tile.net"),
                Signed(net, data.Currency),
                paid > 0
                    ? Texts.Format(
                        "dashboard.planValue.tile.overall",
                        PlanCards.Multiple((double)(pricedCost / paid)))
                    : null,
                net >= 0 ? "HakariOkBrush" : "HakariCriticalBrush"),
            DashboardTile.Create(
                Texts.Get("dashboard.planValue.tile.projected"),
                MoneyText.Format(projected, data.Currency),
                Texts.Format(
                    "dashboard.planValue.tile.daysLeft",
                    data.DaysInMonth - data.Today.Day)),
        ]);
    }

    private static string? Change(decimal cost, decimal before)
    {
        if (before <= 0)
        {
            return null;
        }

        var change = (double)((cost - before) / before) * PercentScale;
        var arrow = change >= 0 ? "▲" : "▼";
        return Texts.Format(
            "dashboard.planValue.lastMonth",
            $"{arrow} {Math.Abs(change).ToString("F0", CultureInfo.InvariantCulture)}%");
    }

    private static string Signed(decimal amount, string currency) =>
        (amount < 0 ? "−" : "+") + MoneyText.Format(Math.Abs(amount), currency);

    private static TextBlock Note(bool hasEarlier) => new()
    {
        Text = hasEarlier
            ? $"{Texts.Get("dashboard.planValue.note")} "
                + Texts.Get("dashboard.planValue.earlierNote")
            : Texts.Get("dashboard.planValue.note"),
        FontSize = 12,
        Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
        TextWrapping = TextWrapping.Wrap,
    };
}
