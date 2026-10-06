using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Presentation.Widget;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Hakari.Taskbar.Rendering;
using Hakari.Taskbar.Tray;

namespace Hakari.Feed;

/// <summary>Gathers the facts, lets the shared composer lay them out, maps the colors.</summary>
internal static class WidgetText
{
    private const string ProductName = "Hakari";

    public static WidgetContent Loading =>
        new(ProductName, Texts.Get("widget.loading"), WidgetTone.Muted);

    /// <summary>Reading the logs: how far along, and how many bytes of how many.</summary>
    public static WidgetContent Indexing(IndexProgress progress) => new(
        Texts.Format(
            "widget.indexing",
            PercentText.Format(progress.Fraction * 100, 0)),
        Texts.Format(
            "widget.indexingBytes",
            ByteText.Format(progress.BytesDone),
            ByteText.Format(progress.BytesTotal)),
        WidgetTone.Muted,
        Ring: new WidgetRing(progress.Fraction));

    /// <summary>A PAUSED pill, and until when, or how to resume.</summary>
    public static WidgetContent Paused(HakariSettings settings) => new(
        string.Empty,
        settings.PausedUntil is { } until
            ? Texts.Format("widget.pausedUntil", until.ToLocalTime().ToString("HH:mm"))
            : Texts.Get("widget.pausedManual"),
        WidgetTone.Muted,
        Pill: Texts.Get("widget.pausedPill"));

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
            presentation.PercentDecimals,
            withHourlySpend: UsesSparkline(presentation.Widget));
    }

    private static bool UsesSparkline(WidgetLayout layout) =>
        layout.Slots.Any(slot => slot.Style == WidgetSlotStyle.Sparkline);

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

    /// <summary>The first shown account's most pressing limit, for the tray icon.</summary>
    public static TrayBadge? Badge(WidgetFacts facts)
    {
        if (facts.Accounts.FirstOrDefault() is not { } account)
        {
            return null;
        }

        var snapshot = account.Snapshot.ProjectedTo(DateTimeOffset.Now);
        return LimitPriority.MostPressing(snapshot) is { } limit
            ? new TrayBadge(
                (int)Math.Floor(account.PercentOf(limit)),
                ToWidgetTone(WidgetComposer.ToneOf(limit, snapshot.Freshness)))
            : null;
    }

    private static WidgetContent ToContent(ComposedWidget widget) => new(
        widget.Top.Text,
        widget.Bottom.Text,
        ToWidgetTone(widget.Bottom.Tone),
        ToWidgetTone(widget.Top.Tone),
        widget.Ring is { } ring ? ToRing(ring) : null,
        TurnIndex: widget.Turn?.Index ?? 0,
        TurnCount: widget.Turn?.Count ?? 0,
        Spark: widget.Spark,
        PrimaryBar: widget.TopBar is { } top ? ToBar(top) : null,
        SecondaryBar: widget.BottomBar is { } bottom ? ToBar(bottom) : null);

    private static WidgetBar ToBar(ComposedBar bar) =>
        new(bar.Fraction, ToWidgetTone(bar.Tone));

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
