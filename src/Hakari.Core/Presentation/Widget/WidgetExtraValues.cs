using Hakari.Core.Querying;

namespace Hakari.Core.Presentation.Widget;

/// <summary>
/// The format values that cost a read of their own: the week and all time, the current
/// session, the month's projection, today's budget, burn in tokens, token kinds, the cache,
/// active sessions, the top model and the plan's value. Only the names a format really uses
/// are read, so a widget without a custom format costs nothing extra.
/// </summary>
public static class WidgetExtraValues
{
    public const string CostWeek = "cost.week";
    public const string CostAll = "cost.all";
    public const string CostSession = "cost.session";
    public const string CostProjection = "cost.projection.month";
    public const string BudgetToday = "budget.remaining.day";
    public const string BurnTokens = "burn.tokens";
    public const string TokensInput = "tokens.input";
    public const string TokensOutput = "tokens.output";
    public const string TokensCacheRead = "tokens.cacheRead";
    public const string TokensCacheWrite = "tokens.cacheWrite";
    public const string CacheHitRate = "cache.hitRate";
    public const string SessionsActive = "sessions.active";
    public const string ModelTop = "model.top";
    /// <summary>Written apart so the locale test does not take it for a text key.</summary>
    public const string PlanValue = "plan" + ".value";

    private const double PercentScale = 100;
    private const string Approximately = "≈ ";
    private const string PerMinute = "/min";
    private const string ModelPrefix = "claude-";
    private static readonly TimeSpan BurnWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(5);

    public static IReadOnlyList<string> Names { get; } =
    [
        CostWeek, CostAll, CostSession, CostProjection, BudgetToday, BurnTokens, TokensInput,
        TokensOutput, TokensCacheRead, TokensCacheWrite, CacheHitRate, SessionsActive,
        ModelTop, PlanValue,
    ];

    /// <param name="wanted">Names the formats use; anything else is not read.</param>
    /// <param name="dailyBudget">The day's budget in the shown currency, or null for none.</param>
    /// <param name="planPrice">The first account's plan price a month, in that currency.</param>
    public static IReadOnlyDictionary<string, FormatValue> Read(
        UsageQuery query,
        IReadOnlyCollection<string> wanted,
        DateTimeOffset now,
        decimal? dailyBudget = null,
        decimal? planPrice = null,
        string? accountId = null)
    {
        var values = new Dictionary<string, FormatValue>(StringComparer.Ordinal);
        var currency = query.Currency;
        var today = new Lazy<UsageSummary>(() =>
            query.Total(new UsageFilter(From: TimePeriods.StartOfToday(now))));
        var month = new Lazy<UsageSummary>(() =>
            query.Total(new UsageFilter(From: TimePeriods.StartOfMonth(now))));
        foreach (var name in wanted.Distinct())
        {
            if (Value(name) is { } value)
            {
                values[name] = value;
            }
        }

        return values;

        FormatValue Money(decimal amount) => FormatValue.Money(amount, currency);

        FormatValue? Value(string name) => name switch
        {
            CostWeek => Money(query.Total(
                new UsageFilter(From: TimePeriods.StartOfWeek(now))).Cost),
            CostAll => Money(query.Total(UsageFilter.Everything).Cost),
            CostSession => LatestSession(query, now) is { } session ? Money(session.Cost) : null,
            CostProjection => MonthProjection.Of(month.Value.Cost, now) is { } projected
                ? FormatValue.Words(
                    Approximately + MoneyText.Format(decimal.Round(projected), currency))
                : null,
            BudgetToday => dailyBudget is { } budget
                ? Money(Math.Max(0, budget - today.Value.Cost))
                : null,
            BurnTokens => FormatValue.Words(
                TokenText.Format(TokensPerMinute(query, now)) + PerMinute),
            TokensInput => FormatValue.Tokens(today.Value.Tokens.Input),
            TokensOutput => FormatValue.Tokens(today.Value.Tokens.Output),
            TokensCacheRead => FormatValue.Tokens(today.Value.Tokens.CacheRead),
            TokensCacheWrite => FormatValue.Tokens(today.Value.Tokens.CacheWrite),
            CacheHitRate => FormatValue.Percent(today.Value.Tokens.CacheHitRate * PercentScale),
            SessionsActive => FormatValue.Count(query.Summarize(
                new UsageFilter(From: now - ActiveWindow), GroupBy.Session).Count),
            ModelTop => TopModel(query, now, today.Value.Cost),
            PlanValue => planPrice is > 0 && accountId is not null
                ? FormatValue.Words(PlanMultiple(query, now, accountId, planPrice.Value))
                : null,
            _ => null,
        };
    }
    private static UsageSummary? LatestSession(UsageQuery query, DateTimeOffset now) =>
        query.Summarize(new UsageFilter(From: TimePeriods.StartOfToday(now)), GroupBy.Session)
            .MaxBy(session => session.LastSeen);

    private static long TokensPerMinute(UsageQuery query, DateTimeOffset now)
    {
        var recent = query.Total(new UsageFilter(From: now - BurnWindow)).Tokens;
        return (recent.TotalInput + recent.Output) / (long)BurnWindow.TotalMinutes;
    }

    /// <summary>"opus-5-5 · 92%": today's costliest model and its share of the day.</summary>
    private static FormatValue? TopModel(UsageQuery query, DateTimeOffset now, decimal total)
    {
        var top = query.Summarize(
            new UsageFilter(From: TimePeriods.StartOfToday(now)), GroupBy.Model).FirstOrDefault();
        if (top is null || total <= 0)
        {
            return null;
        }

        var share = PercentText.Format((double)(top.Cost / total) * PercentScale, 0);
        var name = top.Key.StartsWith(ModelPrefix, StringComparison.Ordinal)
            ? top.Key[ModelPrefix.Length..]
            : top.Key;
        return FormatValue.Words($"{name} · {share}");
    }

    /// <summary>"53×": what the month's usage would cost at list prices, over the plan.</summary>
    private static string PlanMultiple(
        UsageQuery query,
        DateTimeOffset now,
        string accountId,
        decimal planPrice)
    {
        var spent = query.Total(new UsageFilter(
            From: TimePeriods.StartOfMonth(now),
            AccountId: accountId)).Cost;
        return $"{decimal.Round(spent / planPrice, spent / planPrice < 10 ? 1 : 0)}×";
    }
}
