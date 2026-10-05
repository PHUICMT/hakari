using Hakari.Core.Limits;
using Hakari.Core.Querying;

namespace Hakari.Core.Presentation.Widget;

/// <summary>
/// Reads the totals the widget can show. Shared by Hakari.exe and the settings preview so
/// both count the same way.
/// </summary>
public static class WidgetFactsBuilder
{
    /// <param name="accounts">Already arranged: shown accounts, in display order.</param>
    /// <param name="withAccountUsage">Also read each account's own totals, for blocks.</param>
    public static WidgetFacts Build(
        UsageQuery query,
        IReadOnlyList<WidgetAccount> accounts,
        bool withAccountUsage,
        IReadOnlyDictionary<string, string> nicknames,
        DateTimeOffset now,
        Func<string, UsageLimit, DateTimeOffset, DateTimeOffset?>? fullAt = null)
    {
        var total = UsageOf(query, accountId: null, now);
        var shown = withAccountUsage
            ? [.. accounts.Select(account => account with
            {
                Costs = UsageOf(query, account.AccountId, now),
            })]
            : accounts;
        return new WidgetFacts(
            CostToday: total.Today,
            CostThisMonth: total.ThisMonth,
            CostLastHour: total.LastHour,
            Currency: query.Currency,
            Accounts: shown,
            FullAt: fullAt,
            Nicknames: nicknames,
            TokensToday: total.TokensToday,
            TokensThisMonth: total.TokensThisMonth,
            RepliesToday: total.RepliesToday);
    }

    private static AccountCosts UsageOf(UsageQuery query, string? accountId, DateTimeOffset now)
    {
        UsageSummary Since(DateTimeOffset from) =>
            query.Total(new UsageFilter(From: from, AccountId: accountId));

        var today = Since(TimePeriods.StartOfToday(now));
        var month = Since(TimePeriods.StartOfMonth(now));
        var lastHour = Since(now.AddHours(-1));
        return new AccountCosts(
            today.Cost,
            month.Cost,
            lastHour.Cost,
            TokensToday: AllTokens(today),
            TokensThisMonth: AllTokens(month),
            RepliesToday: today.Messages);
    }

    private static long AllTokens(UsageSummary summary) =>
        summary.Tokens.TotalInput + summary.Tokens.Output;
}
