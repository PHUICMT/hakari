using Hakari.Core.Localization;
using Hakari.Core.Presentation.Widget;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Hakari.Taskbar.Rendering;

namespace Hakari.Feed;

/// <summary>Gathers the facts, lets the shared composer lay them out, maps the colors.</summary>
internal static class WidgetText
{
    private const string ProductName = "Hakari";

    public static WidgetContent Loading =>
        new(ProductName, Texts.Get("widget.loading"), WidgetTone.Muted);

    public static WidgetContent Paused =>
        new(ProductName, Texts.Get("widget.paused"), WidgetTone.Muted);

    /// <summary>The database part: run on new data or every half minute, not every turn.</summary>
    public static WidgetFacts Facts(
        UsageQuery query,
        LimitPoller limits,
        HakariSettings presentation)
    {
        var now = DateTimeOffset.Now;
        var accounts = presentation.AccountsMode == MultiAccountMode.Together
            ? limits.Accounts
            : [.. limits.Accounts.Select(account => account with
            {
                Costs = CostsOf(query, account.AccountId, now),
            })];
        return new WidgetFacts(
            CostToday: CostSince(query, TimePeriods.StartOfToday(now), null),
            CostThisMonth: CostSince(query, TimePeriods.StartOfMonth(now), null),
            CostLastHour: CostSince(query, now.AddHours(-1), null),
            Currency: query.Currency,
            Accounts: accounts,
            FullAt: limits.FullAt,
            Nicknames: presentation.AccountNicknames);
    }

    /// <summary>The cheap part: lays the facts out, such as for the next account's turn.</summary>
    public static WidgetContent Content(WidgetFacts facts, HakariSettings presentation)
    {
        var now = DateTimeOffset.Now;
        var panels = WidgetPanels.Compose(
            presentation.AccountsMode,
            presentation.Widget,
            presentation.LayoutOf,
            facts,
            now,
            presentation.TurnLength);
        var contents = panels.Select(ToContent).ToList();
        return contents[0] with { MorePanels = contents.Count > 1 ? contents[1..] : null };
    }

    private static AccountCosts CostsOf(UsageQuery query, string accountId, DateTimeOffset now) =>
        new(
            CostSince(query, TimePeriods.StartOfToday(now), accountId),
            CostSince(query, TimePeriods.StartOfMonth(now), accountId),
            CostSince(query, now.AddHours(-1), accountId));

    private static decimal CostSince(UsageQuery query, DateTimeOffset from, string? accountId) =>
        query.Total(new UsageFilter(From: from, AccountId: accountId)).Cost;

    private static WidgetContent ToContent(ComposedWidget widget) => new(
        widget.Top.Text,
        widget.Bottom.Text,
        ToWidgetTone(widget.Bottom.Tone),
        ToWidgetTone(widget.Top.Tone),
        widget.Ring is { } ring ? ToRing(ring) : null);

    private static WidgetRing ToRing(ComposedRing ring) => new(
        ring.Fraction,
        ToWidgetTone(ring.Tone),
        ring.InnerFraction,
        ToWidgetTone(ring.InnerTone));

    private static WidgetTone ToWidgetTone(LineTone tone) => tone switch
    {
        LineTone.Muted => WidgetTone.Muted,
        LineTone.Warning => WidgetTone.Warning,
        LineTone.Critical => WidgetTone.Critical,
        _ => WidgetTone.Normal,
    };
}
