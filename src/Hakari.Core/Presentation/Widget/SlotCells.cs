using System.Globalization;
using Hakari.Core.Limits;
using Hakari.Core.Localization;

namespace Hakari.Core.Presentation.Widget;

/// <summary>Turns a widget item into a value and a label, for columns, bars and rings.</summary>
internal static class SlotCells
{
    private const string Dash = "—";
    private const string CountFormat = "N0";
    private const string Both = " · ";

    public static SlotCell Of(
        WidgetItem item,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules)
    {
        var account = facts.Accounts.FirstOrDefault();
        return item switch
        {
            WidgetItem.Automatic or WidgetItem.CostToday =>
                Plain(Money(facts.CostToday, facts), "widget.label.today"),
            WidgetItem.CostThisMonth =>
                Plain(Money(facts.CostThisMonth, facts), "widget.label.month"),
            WidgetItem.BurnRate => Plain(Money(facts.CostLastHour, facts), "widget.label.burn"),
            WidgetItem.TokensToday =>
                Plain(TokenText.Format(facts.TokensToday), "widget.label.tokensToday"),
            WidgetItem.TokensThisMonth =>
                Plain(TokenText.Format(facts.TokensThisMonth), "widget.label.tokensMonth"),
            WidgetItem.RepliesToday => Plain(
                facts.RepliesToday.ToString(CountFormat, CultureInfo.InvariantCulture),
                "widget.label.replies"),
            WidgetItem.SessionLimit => Limit(account, LimitPicks.Session, facts, now, rules),
            WidgetItem.WeeklyLimit => Limit(account, LimitPicks.Weekly, facts, now, rules),
            WidgetItem.MostPressingLimit =>
                Limit(account, LimitPriority.MostPressing, facts, now, rules),
            WidgetItem.SessionAndWeeklyLimits => BothLimits(account, facts, now, rules),
            WidgetItem.SecondAccount => facts.Accounts.Count > 1
                ? Limit(facts.Accounts[1], LimitPriority.MostPressing, facts, now, rules, true)
                : new SlotCell(Dash, Texts.Get("widget.noSecondAccount"), LineTone.Muted),
            _ => new SlotCell(string.Empty, string.Empty, LineTone.Normal),
        };
    }

    private static SlotCell Plain(string value, string labelKey) =>
        new(value, Texts.Get(labelKey), LineTone.Normal);

    private static string Money(decimal amount, WidgetFacts facts) =>
        MoneyText.Format(amount, facts.Currency);

    private static SlotCell Limit(
        WidgetAccount? account,
        Func<LimitSnapshot, UsageLimit?> pick,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules,
        bool labelWithAccount = false)
    {
        if (account is null)
        {
            return new SlotCell(Dash, Texts.Get("widget.noLimits"), LineTone.Muted);
        }

        var snapshot = account.Snapshot.ProjectedTo(now);
        if (pick(snapshot) is not { } limit)
        {
            return new SlotCell(Dash, Texts.Get("widget.noLimits"), LineTone.Muted);
        }

        var percent = account.PercentOf(limit);
        var label = labelWithAccount
            ? AccountLabels.Short(account.Account, facts.NicknameOf(account.AccountId))
            : LimitNames.Short(limit);
        return new SlotCell(
            facts.FormatPercent(percent),
            label,
            rules.Of(percent, snapshot.Freshness),
            WidgetComposer.FractionOf(account, limit));
    }

    /// <summary>"5% · 93%" over "5h · Week", in the color of the worse of the two.</summary>
    private static SlotCell BothLimits(
        WidgetAccount? account,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules)
    {
        var session = Limit(account, LimitPicks.Session, facts, now, rules);
        var weekly = Limit(account, LimitPicks.Weekly, facts, now, rules);
        return new SlotCell(
            session.Value + Both + weekly.Value,
            session.Label + Both + weekly.Label,
            (LineTone)Math.Max((int)session.Tone, (int)weekly.Tone),
            weekly.Fraction ?? session.Fraction);
    }
}
