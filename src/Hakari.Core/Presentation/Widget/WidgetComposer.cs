using Hakari.Core.Limits;
using Hakari.Core.Localization;

namespace Hakari.Core.Presentation.Widget;

/// <summary>
/// Builds the widget's two lines and ring from the user's layout. Shared by Hakari.exe and
/// the settings preview, so the preview shows exactly what the taskbar will.
/// </summary>
public static class WidgetComposer
{
    private const string LastKnownPrefix = "≈ ";
    private const double PercentScale = 100.0;
    private const int TwoAccounts = 2;
    private const string Separator = " · ";

    public static ComposedWidget Compose(
        WidgetLayout layout,
        WidgetFacts facts,
        DateTimeOffset now)
    {
        var rules = ToneRules.Of(layout);
        var widget = layout.UsesSlots
            ? SlotComposer.Compose(layout, facts, now, rules)
            : Classic(layout, facts, now, rules);
        return layout.CustomFormat is { Length: > 0 } format
            ? widget with
            {
                Top = new ComposedLine(
                    WidgetFormat.Apply(format, WidgetValues.Lookup(facts, now)),
                    widget.Top.Tone),
                Bottom = new ComposedLine(string.Empty),
                TopBar = null,
                BottomBar = null,
            }
            : widget;
    }

    private static ComposedWidget Classic(
        WidgetLayout layout,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules)
    {
        var top = layout.Top;
        var bottom = layout.Bottom;
        if (top == WidgetItem.Nothing && bottom == WidgetItem.Nothing)
        {
            top = WidgetItem.Automatic;
        }

        return new ComposedWidget(
            Line(top, isTop: true, facts, now, rules),
            Line(bottom, isTop: false, facts, now, rules),
            Ring(layout.Ring, facts, now, rules));
    }

    internal static ComposedLine Line(
        WidgetItem item,
        bool isTop,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules)
    {
        var labelled = facts.Accounts.Count >= TwoAccounts;
        var first = facts.Accounts.FirstOrDefault();
        return item switch
        {
            WidgetItem.Automatic => Automatic(isTop, facts, now, rules),
            WidgetItem.CostToday => Today(facts),
            WidgetItem.CostThisMonth => Month(facts),
            WidgetItem.BurnRate => PerHour(facts),
            WidgetItem.TokensToday => Money(
                Texts.Format("widget.tokensToday", TokenText.Format(facts.TokensToday))),
            WidgetItem.TokensThisMonth => Money(
                Texts.Format("widget.tokensMonth", TokenText.Format(facts.TokensThisMonth))),
            WidgetItem.RepliesToday => Money(
                Texts.Format("widget.repliesToday", TokenText.Format(facts.RepliesToday))),
            WidgetItem.SessionLimit =>
                LimitLine(first, LimitPicks.Session, labelled, facts, now, rules),
            WidgetItem.WeeklyLimit =>
                LimitLine(first, LimitPicks.Weekly, labelled, facts, now, rules),
            WidgetItem.MostPressingLimit =>
                LimitLine(first, LimitPriority.MostPressing, labelled, facts, now, rules),
            WidgetItem.SessionAndWeeklyLimits => BothLimits(first, labelled, facts, now, rules),
            WidgetItem.SecondAccount => facts.Accounts.Count >= TwoAccounts
                ? LimitLine(
                    facts.Accounts[1], LimitPriority.MostPressing, true, facts, now, rules)
                : new ComposedLine(Texts.Get("widget.noSecondAccount"), LineTone.Muted),
            _ => new ComposedLine(string.Empty),
        };
    }

    /// <summary>
    /// Two accounts: one line each. One: money on top, its pressing limit below. None:
    /// money on top, burn rate and month below.
    /// </summary>
    private static ComposedLine Automatic(
        bool isTop,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules)
    {
        var accounts = facts.Accounts;
        if (accounts.Count >= TwoAccounts)
        {
            var account = isTop ? accounts[0] : accounts[1];
            return LimitLine(account, LimitPriority.MostPressing, true, facts, now, rules);
        }

        if (isTop)
        {
            return Today(facts);
        }

        return accounts.Count == 1
            ? LimitLine(accounts[0], LimitPriority.MostPressing, false, facts, now, rules)
            : Money(Texts.Format(
                "widget.burnAndMonth",
                Format(facts.CostLastHour, facts),
                Format(facts.CostThisMonth, facts)));
    }

    private static ComposedLine LimitLine(
        WidgetAccount? account,
        Func<LimitSnapshot, UsageLimit?> pick,
        bool labelled,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules)
    {
        if (account is null)
        {
            return NoLimits;
        }

        var snapshot = account.Snapshot.ProjectedTo(now);
        if (pick(snapshot) is not { } limit)
        {
            return NoLimits;
        }

        var prefix = snapshot.Freshness == LimitFreshness.LastKnown
            ? LastKnownPrefix
            : string.Empty;
        var nickname = facts.NicknameOf(account.AccountId);
        var label = labelled
            ? $"{AccountLabels.Short(account.Account, nickname)} "
            : string.Empty;
        var fullAt = facts.FullAt?.Invoke(account.AccountId, limit, now);
        var percent = facts.FormatPercent(account.PercentOf(limit));
        var text = $"{prefix}{label}{LimitText.Compact(limit, fullAt, now, percent)}";
        return new ComposedLine(
            text,
            rules.Of(account.PercentOf(limit), snapshot.Freshness));
    }

    /// <summary>"5h 5% · Week full": both windows at a glance, in the color of the worse.</summary>
    private static ComposedLine BothLimits(
        WidgetAccount? account,
        bool labelled,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules)
    {
        if (account is null)
        {
            return NoLimits;
        }

        var snapshot = account.Snapshot.ProjectedTo(now);
        var limits = new[] { LimitPicks.Session(snapshot), LimitPicks.Weekly(snapshot) }
            .OfType<UsageLimit>()
            .ToList();
        if (limits.Count == 0)
        {
            return NoLimits;
        }

        var label = labelled
            ? $"{AccountLabels.Short(account.Account, facts.NicknameOf(account.AccountId))} "
            : string.Empty;
        var parts = limits.Select(limit => limit.Percent >= LimitForecaster.FullPercent
            ? Texts.Format("limit.full", LimitNames.Short(limit))
            : $"{LimitNames.Short(limit)} {facts.FormatPercent(account.PercentOf(limit))}");
        var tone = limits
            .Select(limit => rules.Of(account.PercentOf(limit), snapshot.Freshness))
            .Max();
        return new ComposedLine(label + string.Join(Separator, parts), tone);
    }

    internal static ComposedRing? Ring(
        WidgetRingSource source,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules)
    {
        if (source == WidgetRingSource.Off || facts.Accounts.FirstOrDefault() is not { } first)
        {
            return null;
        }

        var snapshot = first.Snapshot.ProjectedTo(now);
        if (source == WidgetRingSource.SessionAndWeekly)
        {
            return TwoRings(first, snapshot, rules);
        }

        var limit = source switch
        {
            WidgetRingSource.Weekly => LimitPicks.Weekly(snapshot),
            WidgetRingSource.MostPressing => LimitPriority.MostPressing(snapshot),
            _ => LimitPicks.Session(snapshot),
        };
        return limit is null
            ? null
            : new ComposedRing(
                FractionOf(first, limit),
                rules.Of(first.PercentOf(limit), snapshot.Freshness));
    }

    /// <summary>The week outside, since it is the bigger window; the 5 hours inside.</summary>
    private static ComposedRing? TwoRings(
        WidgetAccount account,
        LimitSnapshot snapshot,
        ToneRules rules)
    {
        var weekly = LimitPicks.Weekly(snapshot);
        var session = LimitPicks.Session(snapshot);
        if (weekly is null || session is null)
        {
            return (weekly ?? session) is { } only
                ? new ComposedRing(
                    FractionOf(account, only),
                    rules.Of(account.PercentOf(only), snapshot.Freshness))
                : null;
        }

        return new ComposedRing(
            FractionOf(account, weekly),
            rules.Of(account.PercentOf(weekly), snapshot.Freshness),
            FractionOf(account, session),
            rules.Of(account.PercentOf(session), snapshot.Freshness));
    }

    internal static double FractionOf(WidgetAccount account, UsageLimit limit) =>
        Math.Clamp(account.PercentOf(limit) / PercentScale, 0, 1);

    /// <summary>The tone of a limit with the default thresholds, as for the tray icon.</summary>
    public static LineTone ToneOf(UsageLimit limit, LimitFreshness freshness) =>
        ToneRules.Default.Of(limit.Percent, freshness);

    private static ComposedLine NoLimits => new(Texts.Get("widget.noLimits"), LineTone.Muted);

    private static ComposedLine Money(string text) => new(text);

    private static ComposedLine Today(WidgetFacts facts) =>
        Money(Texts.Format("widget.today", Format(facts.CostToday, facts)));

    private static ComposedLine Month(WidgetFacts facts) =>
        Money(Texts.Format("widget.month", Format(facts.CostThisMonth, facts)));

    private static ComposedLine PerHour(WidgetFacts facts) =>
        Money(Texts.Format("widget.perHour", Format(facts.CostLastHour, facts)));

    private static string Format(decimal amount, WidgetFacts facts) =>
        MoneyText.Format(amount, facts.Currency);
}
