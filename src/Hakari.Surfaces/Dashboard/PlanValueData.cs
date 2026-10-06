using System.Globalization;
using Hakari.Core.Accounts;
using Hakari.Core.Currency;
using Hakari.Core.Indexing;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Hakari.Surfaces.Flyout;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// What the Plan value page shows: for each account that has used anything lately, the cost of
/// its usage at per-token prices against its plan's price, month by month. It does not depend
/// on the period in the filter bar, only on the account and source.
/// </summary>
internal sealed record PlanValueData(
    string Currency,
    DateOnly Today,
    IReadOnlyList<PlanAccount> Accounts)
{
    private const int MonthsShown = 6;
    private const string DayFormat = "yyyy-MM-dd";

    public int DaysInMonth => DateTime.DaysInMonth(Today.Year, Today.Month);

    public static PlanValueData Empty { get; } =
        new("USD", DateOnly.FromDateTime(DateTime.Today), []);

    public static PlanValueData Load(DashboardFilter filter) =>
        DashboardData.Read((query, store) => Read(query, store, filter), Empty);

    private static PlanValueData Read(UsageQuery query, IndexStore store, DashboardFilter filter)
    {
        var now = DateTimeOffset.Now;
        var settings = SettingsStore.Default.Load();
        var converter = FlyoutDataLoader.StoredConverter(store, settings);
        var accounts = new List<PlanAccount>();
        foreach (var account in new AccountRepository(store).ListAccounts())
        {
            if (filter.AccountId is { } chosen && chosen != account.AccountId)
            {
                continue;
            }

            var months = Months(query, account, now);
            if (months.Any(month => month.Cost > 0))
            {
                accounts.Add(Build(query, account, settings, converter, months, now));
            }
        }

        return new PlanValueData(
            query.Currency,
            DateOnly.FromDateTime(now.DateTime),
            [.. accounts.OrderByDescending(account => account.ThisMonth.Cost)]);
    }

    private static List<PlanMonth> Months(UsageQuery query, AccountInfo account, DateTimeOffset now)
    {
        var months = new List<PlanMonth>();
        for (var back = 0; back < MonthsShown; back++)
        {
            var from = TimePeriods.StartOfMonth(now.AddMonths(-back));
            DateTimeOffset? to = back == 0
                ? null
                : TimePeriods.StartOfMonth(now.AddMonths(1 - back));
            var usage = new UsageFilter(From: from, To: to, AccountId: account.AccountId);
            months.Add(new PlanMonth(
                from.ToString("MMM", Texts.Culture),
                query.Total(usage).Cost));
        }

        return months;
    }

    private static PlanAccount Build(
        UsageQuery query,
        AccountInfo account,
        HakariSettings settings,
        CurrencyConverter? converter,
        List<PlanMonth> months,
        DateTimeOffset now)
    {
        var dollars = PlanPrices.MonthlyDollars(account.Plan);
        var price = dollars is { } list && converter is not null
            ? converter.Convert(list, DateOnly.FromDateTime(now.UtcDateTime))
            : dollars;
        return new PlanAccount(
            AccountLabels.Full(account, settings.NicknameOf(account.AccountId)),
            PlanNames.Short(account.Plan),
            price,
            months,
            price is { } monthly && months[0].Cost > 0
                ? BreakEven(query, account, monthly, months[0].Cost, now)
                : null);
    }

    /// <summary>The day the running total of this month passes the plan's price.</summary>
    private static PlanBreakEven BreakEven(
        UsageQuery query,
        AccountInfo account,
        decimal price,
        decimal monthCost,
        DateTimeOffset now)
    {
        var start = TimePeriods.StartOfMonth(now);
        var days = query.Summarize(
                new UsageFilter(From: start, AccountId: account.AccountId),
                GroupBy.Day)
            .ToDictionary(summary => summary.Key, summary => summary.Cost);
        var month = DateOnly.FromDateTime(start.DateTime);
        var total = 0m;
        for (var day = 0; day < now.Day; day++)
        {
            var date = month.AddDays(day);
            total += days.GetValueOrDefault(date.ToString(DayFormat, CultureInfo.InvariantCulture));
            if (total >= price)
            {
                return new PlanBreakEven(date, null);
            }
        }

        var perDay = monthCost / now.Day;
        var dayNeeded = (int)Math.Ceiling(price / perDay);
        return new PlanBreakEven(
            null,
            dayNeeded <= DateTime.DaysInMonth(now.Year, now.Month)
                ? month.AddDays(dayNeeded - 1)
                : null);
    }
}
