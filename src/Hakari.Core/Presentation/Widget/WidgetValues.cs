using Hakari.Core.Limits;

namespace Hakari.Core.Presentation.Widget;

/// <summary>
/// The names a custom format string can use, and what each one is right now: money and token
/// totals, the limits of the first shown account with their percent and the time until they
/// reset, and that account's name.
/// </summary>
public static class WidgetValues
{
    private const string Reset = ".reset";
    private const string FiveHours = LimitPrefix + "5h";
    private const string Week = LimitPrefix + "week";
    private const string Pressing = LimitPrefix + "pressing";

    /// <summary>Written apart so the locale test does not take these names for text keys.</summary>
    private const string LimitPrefix = "limit.";

    private const string FiveHoursAndWeek = FiveHours + "+week";
    private const string WeekModel = Week + ".model";
    private const string Projection = ".projection";
    private const char ScopeMark = '@';
    private const double PercentScale = 100;
    private static readonly TimeSpan SessionWindow = TimeSpan.FromHours(5);

    /// <summary>The names a format may use, for showing them to whoever writes one.</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        "cost.today",
        WidgetExtraValues.CostWeek,
        "cost.month",
        WidgetExtraValues.CostAll,
        WidgetExtraValues.CostSession,
        WidgetExtraValues.CostProjection,
        WidgetExtraValues.BudgetToday,
        FiveHours,
        FiveHours + Reset,
        FiveHours + Projection,
        Week,
        Week + Reset,
        WeekModel,
        FiveHoursAndWeek,
        Pressing,
        Pressing + Reset,
        "burn.cost",
        WidgetExtraValues.BurnTokens,
        "tokens.today",
        "tokens.month",
        WidgetExtraValues.TokensInput,
        WidgetExtraValues.TokensOutput,
        WidgetExtraValues.TokensCacheRead,
        WidgetExtraValues.TokensCacheWrite,
        WidgetExtraValues.CacheHitRate,
        "responses.today",
        WidgetExtraValues.SessionsActive,
        WidgetExtraValues.ModelTop,
        WidgetExtraValues.PlanValue,
        "account",
    ];

    public static Func<string, FormatValue?> Lookup(WidgetFacts facts, DateTimeOffset now) =>
        name => ValueOf(name, facts, now);

    /// <summary>
    /// A name may end in "@account" to read that account instead of the first shown one,
    /// matched by nickname, short label or email, ignoring case.
    /// </summary>
    private static FormatValue? ValueOf(string fullName, WidgetFacts facts, DateTimeOffset now)
    {
        var mark = fullName.IndexOf(ScopeMark);
        var name = mark < 0 ? fullName : fullName[..mark];
        var scope = mark < 0 ? null : fullName[(mark + 1)..];
        var account = scope is null ? facts.Accounts.FirstOrDefault() : Find(facts, scope);
        if (scope is not null && account is null)
        {
            return null;
        }

        if (scope is null && facts.Extras is { } extras && extras.TryGetValue(name, out var extra))
        {
            return extra;
        }

        var costs = scope is null ? null : account?.Costs;
        var snapshot = account?.Snapshot.ProjectedTo(now);
        return name switch
        {
            "cost.today" => FormatValue.Money(costs?.Today ?? facts.CostToday, facts.Currency),
            "cost.month" => FormatValue.Money(
                costs?.ThisMonth ?? facts.CostThisMonth, facts.Currency),
            "cost.hour" or "burn.cost" => FormatValue.Money(
                costs?.LastHour ?? facts.CostLastHour, facts.Currency),
            "tokens.today" => FormatValue.Tokens(costs?.TokensToday ?? facts.TokensToday),
            "tokens.month" => FormatValue.Tokens(costs?.TokensThisMonth ?? facts.TokensThisMonth),
            "replies.today" or "responses.today" =>
                FormatValue.Count(costs?.RepliesToday ?? facts.RepliesToday),
            FiveHours => PercentOf(account, Pick(snapshot, LimitPicks.Session)),
            FiveHours + Reset => ResetOf(Pick(snapshot, LimitPicks.Session), now),
            FiveHours + Projection => ProjectionOf(
                account, Pick(snapshot, LimitPicks.Session), now),
            Week => PercentOf(account, Pick(snapshot, LimitPicks.Weekly)),
            Week + Reset => ResetOf(Pick(snapshot, LimitPicks.Weekly), now),
            WeekModel => PercentOf(account, Pick(snapshot, ModelWeekly)),
            FiveHoursAndWeek => Both(account, snapshot, facts),
            Pressing => PercentOf(account, Pick(snapshot, LimitPriority.MostPressing)),
            Pressing + Reset => ResetOf(Pick(snapshot, LimitPriority.MostPressing), now),
            "account" => account is null
                ? null
                : FormatValue.Words(AccountLabels.Short(
                    account.Account,
                    facts.NicknameOf(account.AccountId))),
            _ => null,
        };
    }

    private static WidgetAccount? Find(WidgetFacts facts, string scope) =>
        facts.Accounts.FirstOrDefault(account =>
            string.Equals(facts.NicknameOf(account.AccountId), scope, Ignore)
            || string.Equals(AccountLabels.Short(account.Account), scope, Ignore)
            || string.Equals(account.Account?.Email, scope, Ignore)
            || (account.Account?.Email?.StartsWith(scope + "@", Ignore) ?? false));

    private const StringComparison Ignore = StringComparison.OrdinalIgnoreCase;

    /// <summary>The weekly limit that covers one model, such as Fable's.</summary>
    private static UsageLimit? ModelWeekly(LimitSnapshot snapshot) =>
        snapshot.Limits.FirstOrDefault(limit =>
            limit.Group == LimitPicks.WeeklyGroup && limit.ScopeName is not null);

    /// <summary>
    /// "≈ 86%": where the 5-hour limit ends up at its reset if the pace so far holds, from
    /// how far into its window it is. Nothing until a minute in, when there is no pace yet.
    /// </summary>
    private static FormatValue? ProjectionOf(
        WidgetAccount? account,
        UsageLimit? limit,
        DateTimeOffset now)
    {
        if (account is null || limit?.ResetsAt is not { } resetsAt)
        {
            return null;
        }

        var elapsed = SessionWindow - (resetsAt - now);
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return null;
        }

        var projected = account.PercentOf(limit) * (SessionWindow / elapsed);
        return FormatValue.Words("≈ " + PercentText.Format(Math.Min(projected, 999), 0));
    }

    /// <summary>"5h 5% · Week 93%".</summary>
    private static FormatValue? Both(
        WidgetAccount? account,
        LimitSnapshot? snapshot,
        WidgetFacts facts)
    {
        var session = Pick(snapshot, LimitPicks.Session);
        var weekly = Pick(snapshot, LimitPicks.Weekly);
        if (account is null || (session is null && weekly is null))
        {
            return null;
        }

        var parts = new[] { session, weekly }
            .OfType<UsageLimit>()
            .Select(limit => LimitNames.Short(limit) + " "
                + facts.FormatPercent(account.PercentOf(limit)));
        return FormatValue.Words(string.Join(" · ", parts));
    }
    private static UsageLimit? Pick(
        LimitSnapshot? snapshot,
        Func<LimitSnapshot, UsageLimit?> pick) =>
        snapshot is null ? null : pick(snapshot);

    private static FormatValue? PercentOf(WidgetAccount? account, UsageLimit? limit) =>
        account is null || limit is null
            ? null
            : FormatValue.Percent(account.PercentOf(limit));

    private static FormatValue? ResetOf(UsageLimit? limit, DateTimeOffset now) =>
        limit?.ResetsAt is { } resetsAt
            ? FormatValue.Duration(resetsAt - now)
            : null;
}
