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
/// What the Plan value page shows. Each account's usage at per-token prices against its plan's
/// price, month by month; usage from before Hakari was installed on its own, since it belongs
/// to no account; and everything together against all the plans, which stays true however the
/// earlier usage would have been split. It does not depend on the period in the filter bar.
/// </summary>
/// <param name="Combined">Null when one account or the earlier usage is chosen.</param>
/// <param name="Unassigned">Null when there is none, or one account is chosen.</param>
internal sealed record PlanValueData(
    string Currency,
    DateOnly Today,
    PlanAccount? Combined,
    IReadOnlyList<PlanAccount> Accounts,
    PlanAccount? Unassigned)
{
    private const int MonthsShown = 6;
    private const string DayFormat = "yyyy-MM-dd";

    public int DaysInMonth => DateTime.DaysInMonth(Today.Year, Today.Month);

    public static PlanValueData Empty { get; } =
        new("USD", DateOnly.FromDateTime(DateTime.Today), null, [], null);

    public static PlanValueData Load(DashboardFilter filter) =>
        DashboardData.Read((query, store) => Read(query, store, filter), Empty);

    private static PlanValueData Read(UsageQuery query, IndexStore store, DashboardFilter filter)
    {
        var now = DateTimeOffset.Now;
        var settings = SettingsStore.Default.Load();
        var converter = FlyoutDataLoader.StoredConverter(store, settings);
        var everything = filter.AccountId is null;
        var accounts = new List<PlanAccount>();
        foreach (var account in new AccountRepository(store).ListAccounts())
        {
            if (filter.AccountId is { } chosen && chosen != account.AccountId)
            {
                continue;
            }

            var months = Months(query, account.AccountId, now);
            if (months.Any(month => month.Cost > 0))
            {
                accounts.Add(Build(query, account, settings, converter, months, now));
            }
        }

        accounts.Sort((first, second) => second.ThisMonth.Cost.CompareTo(first.ThisMonth.Cost));
        return new PlanValueData(
            query.Currency,
            DateOnly.FromDateTime(now.DateTime),
            everything ? CombinedCard(query, accounts, now) : null,
            accounts,
            everything || filter.AccountId == string.Empty
                ? EarlierCard(query, now)
                : null);
    }

    /// <summary>All usage against the plans of the accounts that have used lately.</summary>
    private static PlanAccount? CombinedCard(
        UsageQuery query,
        List<PlanAccount> accounts,
        DateTimeOffset now)
    {
        var months = Months(query, null, now);
        if (accounts.Count == 0 || months.All(month => month.Cost <= 0))
        {
            return null;
        }

        var priced = accounts.Where(account => account.Price is not null).ToList();
        var price = priced.Sum(account => account.Price!.Value);
        return new PlanAccount(
            PlanCardKind.Combined,
            Texts.Get("dashboard.planValue.combined"),
            Texts.Format("dashboard.planValue.plans", priced.Count),
            priced.Count == 0 ? null : price,
            months,
            priced.Count > 0 && months[0].Cost > 0
                ? BreakEven(query, null, price, months[0].Cost, now)
                : null);
    }

    private static PlanAccount? EarlierCard(UsageQuery query, DateTimeOffset now)
    {
        var months = Months(query, string.Empty, now);
        return months.All(month => month.Cost <= 0)
            ? null
            : new PlanAccount(
                PlanCardKind.Unassigned,
                Texts.Get("dashboard.accounts.earlier"),
                string.Empty,
                null,
                months,
                null);
    }

    private static List<PlanMonth> Months(UsageQuery query, string? accountId, DateTimeOffset now)
    {
        var months = new List<PlanMonth>();
        for (var back = 0; back < MonthsShown; back++)
        {
            var from = TimePeriods.StartOfMonth(now.AddMonths(-back));
            DateTimeOffset? to = back == 0
                ? null
                : TimePeriods.StartOfMonth(now.AddMonths(1 - back));
            var usage = new UsageFilter(From: from, To: to, AccountId: accountId);
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
        var dollars = settings.PlanPriceOf(account.AccountId, account.Plan);
        var price = dollars is { } list && converter is not null
            ? converter.Convert(list, DateOnly.FromDateTime(now.UtcDateTime))
            : dollars;
        return new PlanAccount(
            PlanCardKind.Account,
            AccountLabels.Full(account, settings.NicknameOf(account.AccountId)),
            PlanNames.Short(account.Plan),
            price,
            months,
            price is { } monthly && months[0].Cost > 0
                ? BreakEven(query, account.AccountId, monthly, months[0].Cost, now)
                : null);
    }

    /// <summary>The day the running total of this month passes the plan's price.</summary>
    private static PlanBreakEven BreakEven(
        UsageQuery query,
        string? accountId,
        decimal price,
        decimal monthCost,
        DateTimeOffset now)
    {
        var start = TimePeriods.StartOfMonth(now);
        var days = query.Summarize(
                new UsageFilter(From: start, AccountId: accountId),
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
