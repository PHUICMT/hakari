using Hakari.Core.Accounts;
using Hakari.Core.Indexing;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Hakari.Surfaces.Flyout;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// What the Plan value page shows: for each account, this month and last, the cost of its
/// usage at per-token prices next to what its plan costs. It does not depend on the period.
/// </summary>
internal sealed record PlanValueData(string Currency, IReadOnlyList<PlanRow> Rows)
{
    private const int MonthsShown = 2;

    public static PlanValueData Empty { get; } = new("USD", []);

    public static PlanValueData Load(DashboardFilter filter) =>
        DashboardData.Read((query, store) => Read(query, store, filter), Empty);

    private static PlanValueData Read(UsageQuery query, IndexStore store, DashboardFilter filter)
    {
        var now = DateTimeOffset.Now;
        var settings = SettingsStore.Default.Load();
        var converter = FlyoutDataLoader.StoredConverter(store, settings);
        var rows = new List<PlanRow>();
        foreach (var account in new AccountRepository(store).ListAccounts())
        {
            if (filter.AccountId is { } chosen && chosen != account.AccountId)
            {
                continue;
            }

            for (var back = 0; back < MonthsShown; back++)
            {
                var row = MonthOf(query, account, settings, converter, now, back);
                if (row is not null)
                {
                    rows.Add(row);
                }
            }
        }

        return new PlanValueData(query.Currency, rows);
    }

    private static PlanRow? MonthOf(
        UsageQuery query,
        AccountInfo account,
        HakariSettings settings,
        Core.Currency.CurrencyConverter? converter,
        DateTimeOffset now,
        int monthsBack)
    {
        var from = TimePeriods.StartOfMonth(now.AddMonths(-monthsBack));
        DateTimeOffset? to = monthsBack == 0
            ? null
            : TimePeriods.StartOfMonth(now.AddMonths(1 - monthsBack));
        var usage = new UsageFilter(From: from, To: to, AccountId: account.AccountId);
        var cost = query.Total(usage).Cost;
        if (cost <= 0)
        {
            return null;
        }

        var dollars = PlanPrices.MonthlyDollars(account.Plan);
        var price = dollars is { } list && converter is not null
            ? converter.Convert(list, DateOnly.FromDateTime(from.UtcDateTime))
            : dollars;
        var label = Texts.Format(
            "dashboard.plan.rowLabel",
            AccountLabels.Short(account, settings.NicknameOf(account.AccountId)),
            PlanNames.Short(account.Plan),
            from.ToString("MMM", Texts.Culture));
        return new PlanRow(label, cost, price);
    }
}
