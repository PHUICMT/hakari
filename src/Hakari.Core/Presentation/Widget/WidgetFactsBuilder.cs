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
        Func<string, UsageLimit, DateTimeOffset, DateTimeOffset?>? fullAt = null,
        int percentDecimals = 0)
    {
        var total = UsageOf(query, accountId: null, now);
        var shown = accounts.Select(account => account with
        {
            Costs = withAccountUsage ? UsageOf(query, account.AccountId, now) : account.Costs,
            EstimatedPercents = percentDecimals > 0
                ? EstimatesOf(query, account, now)
                : null,
        }).ToList();
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
            RepliesToday: total.RepliesToday,
            PercentDecimals: percentDecimals);
    }

    /// <summary>From any reading, live or last known: it builds on when it was taken.</summary>
    private static Dictionary<string, double>? EstimatesOf(
        UsageQuery query,
        WidgetAccount account,
        DateTimeOffset now)
    {
        var snapshot = account.Snapshot.ProjectedTo(now);

        decimal CostBetween(DateTimeOffset from, DateTimeOffset to) => query.Total(
            new UsageFilter(From: from, To: to, AccountId: account.AccountId)).Cost;

        return snapshot.Limits
            .Where(limit => PercentEstimator.WindowOf(limit) is not null)
            .ToDictionary(
                limit => limit.Kind,
                limit => PercentEstimator.Estimate(limit, snapshot.FetchedAt, now, CostBetween));
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
