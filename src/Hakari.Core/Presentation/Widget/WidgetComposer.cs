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
    private const string WeeklyGroup = "weekly";
    private const double PercentScale = 100.0;
    private const int TwoAccounts = 2;

    public static ComposedWidget Compose(
        WidgetLayout layout,
        WidgetFacts facts,
        DateTimeOffset now)
    {
        var top = layout.Top;
        var bottom = layout.Bottom;
        if (top == WidgetItem.Nothing && bottom == WidgetItem.Nothing)
        {
            top = WidgetItem.Automatic;
        }

        return new ComposedWidget(
            Line(top, isTop: true, facts, now),
            Line(bottom, isTop: false, facts, now),
            Ring(layout.Ring, facts, now));
    }

    private static ComposedLine Line(
        WidgetItem item,
        bool isTop,
        WidgetFacts facts,
        DateTimeOffset now)
    {
        var labelled = facts.Accounts.Count >= TwoAccounts;
        var first = facts.Accounts.FirstOrDefault();
        return item switch
        {
            WidgetItem.Automatic => Automatic(isTop, facts, now),
            WidgetItem.CostToday => Today(facts),
            WidgetItem.CostThisMonth => Month(facts),
            WidgetItem.BurnRate => PerHour(facts),
            WidgetItem.SessionLimit => LimitLine(first, Session, labelled, facts, now),
            WidgetItem.WeeklyLimit => LimitLine(first, Weekly, labelled, facts, now),
            WidgetItem.MostPressingLimit =>
                LimitLine(first, LimitPriority.MostPressing, labelled, facts, now),
            WidgetItem.SecondAccount => facts.Accounts.Count >= TwoAccounts
                ? LimitLine(facts.Accounts[1], LimitPriority.MostPressing, true, facts, now)
                : new ComposedLine(Texts.Get("widget.noSecondAccount"), LineTone.Muted),
            _ => new ComposedLine(string.Empty),
        };
    }

    /// <summary>
    /// Two accounts: one line each. One: money on top, its pressing limit below. None:
    /// money on top, burn rate and month below.
    /// </summary>
    private static ComposedLine Automatic(bool isTop, WidgetFacts facts, DateTimeOffset now)
    {
        var accounts = facts.Accounts;
        if (accounts.Count >= TwoAccounts)
        {
            var account = isTop ? accounts[0] : accounts[1];
            return LimitLine(account, LimitPriority.MostPressing, true, facts, now);
        }

        if (isTop)
        {
            return Today(facts);
        }

        return accounts.Count == 1
            ? LimitLine(accounts[0], LimitPriority.MostPressing, false, facts, now)
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
        DateTimeOffset now)
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
        var label = labelled ? $"{AccountLabels.Short(account.Account)} " : string.Empty;
        var fullAt = facts.FullAt?.Invoke(account.AccountId, limit, now);
        var text = $"{prefix}{label}{LimitText.Compact(limit, fullAt, now)}";
        return new ComposedLine(text, ToneOf(limit, snapshot.Freshness));
    }

    private static ComposedRing? Ring(
        WidgetRingSource source,
        WidgetFacts facts,
        DateTimeOffset now)
    {
        if (source == WidgetRingSource.Off || facts.Accounts.FirstOrDefault() is not { } first)
        {
            return null;
        }

        var snapshot = first.Snapshot.ProjectedTo(now);
        var limit = source switch
        {
            WidgetRingSource.Weekly => Weekly(snapshot),
            WidgetRingSource.MostPressing => LimitPriority.MostPressing(snapshot),
            _ => Session(snapshot),
        };
        return limit is null
            ? null
            : new ComposedRing(
                Math.Clamp(limit.Percent / PercentScale, 0, 1),
                ToneOf(limit, snapshot.Freshness));
    }

    public static LineTone ToneOf(UsageLimit limit, LimitFreshness freshness)
    {
        if (limit.Percent >= LimitForecaster.FullPercent)
        {
            return LineTone.Critical;
        }

        return LimitPriority.SeverityRank(limit.Severity) switch
        {
            0 when freshness == LimitFreshness.LastKnown => LineTone.Muted,
            0 => LineTone.Normal,
            1 => LineTone.Warning,
            _ => LineTone.Critical,
        };
    }

    private static UsageLimit? Session(LimitSnapshot snapshot) =>
        snapshot.Limits.FirstOrDefault(limit => limit.Group == LimitPriority.SessionGroup);

    /// <summary>The all-models weekly limit, else the fullest weekly one.</summary>
    private static UsageLimit? Weekly(LimitSnapshot snapshot) =>
        snapshot.Limits
            .Where(limit => limit.Group == WeeklyGroup)
            .OrderBy(limit => limit.ScopeName is null ? 0 : 1)
            .ThenByDescending(limit => limit.Percent)
            .FirstOrDefault();

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
