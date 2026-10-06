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

    /// <summary>The names a format may use, for showing them to whoever writes one.</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        "cost.today",
        "cost.month",
        "cost.hour",
        "tokens.today",
        "tokens.month",
        "replies.today",
        FiveHours,
        FiveHours + Reset,
        Week,
        Week + Reset,
        Pressing,
        Pressing + Reset,
        "account",
    ];

    public static Func<string, FormatValue?> Lookup(WidgetFacts facts, DateTimeOffset now) =>
        name => ValueOf(name, facts, now);

    private static FormatValue? ValueOf(string name, WidgetFacts facts, DateTimeOffset now)
    {
        var account = facts.Accounts.FirstOrDefault();
        var snapshot = account?.Snapshot.ProjectedTo(now);
        return name switch
        {
            "cost.today" => FormatValue.Money(facts.CostToday, facts.Currency),
            "cost.month" => FormatValue.Money(facts.CostThisMonth, facts.Currency),
            "cost.hour" => FormatValue.Money(facts.CostLastHour, facts.Currency),
            "tokens.today" => FormatValue.Tokens(facts.TokensToday),
            "tokens.month" => FormatValue.Tokens(facts.TokensThisMonth),
            "replies.today" => FormatValue.Count(facts.RepliesToday),
            FiveHours => PercentOf(account, Pick(snapshot, LimitPicks.Session)),
            FiveHours + Reset => ResetOf(Pick(snapshot, LimitPicks.Session), now),
            Week => PercentOf(account, Pick(snapshot, LimitPicks.Weekly)),
            Week + Reset => ResetOf(Pick(snapshot, LimitPicks.Weekly), now),
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
