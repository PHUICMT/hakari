using Hakari.Core.Localization;
using Hakari.Core.Presentation.Widget;
using Hakari.Core.Querying;
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

    public static WidgetContent Build(UsageQuery query, LimitPoller limits, WidgetLayout layout)
    {
        var now = DateTimeOffset.Now;
        var facts = new WidgetFacts(
            CostToday: query.Total(new UsageFilter(From: TimePeriods.StartOfToday(now))).Cost,
            CostThisMonth: query.Total(new UsageFilter(From: TimePeriods.StartOfMonth(now))).Cost,
            CostLastHour: query.Total(new UsageFilter(From: now.AddHours(-1))).Cost,
            Currency: query.Currency,
            Accounts: limits.Accounts,
            FullAt: limits.FullAt);
        return ToContent(WidgetComposer.Compose(layout, facts, now));
    }

    private static WidgetContent ToContent(ComposedWidget widget) => new(
        widget.Top.Text,
        widget.Bottom.Text,
        ToWidgetTone(widget.Bottom.Tone),
        ToWidgetTone(widget.Top.Tone),
        widget.Ring is { } ring ? new WidgetRing(ring.Fraction, ToWidgetTone(ring.Tone)) : null);

    private static WidgetTone ToWidgetTone(LineTone tone) => tone switch
    {
        LineTone.Muted => WidgetTone.Muted,
        LineTone.Warning => WidgetTone.Warning,
        LineTone.Critical => WidgetTone.Critical,
        _ => WidgetTone.Normal,
    };
}
