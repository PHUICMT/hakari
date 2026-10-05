using Hakari.Core.Limits;
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
        var arranged = AccountArrangement.Arrange(
            limits.Accounts,
            account => account.AccountId,
            account => LimitPriority.Rank(account.Snapshot),
            presentation);
        return WidgetFactsBuilder.Build(
            query,
            arranged,
            withAccountUsage: presentation.AccountsMode != MultiAccountMode.Together,
            presentation.AccountNicknames,
            now,
            limits.FullAt,
            presentation.PercentDecimals);
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
