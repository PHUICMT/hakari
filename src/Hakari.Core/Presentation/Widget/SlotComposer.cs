namespace Hakari.Core.Presentation.Widget;

/// <summary>
/// Builds a widget from the layout's slots. Text slots become lines (the first two), a ring
/// slot becomes the ring, a sparkline slot the small line of recent spending. Columns give
/// every slot a block of its own, and bars put a thin bar beside each limit line.
/// </summary>
internal static class SlotComposer
{
    public static ComposedWidget Compose(
        WidgetLayout layout,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules)
    {
        var slots = Active(layout, now);
        var texts = slots.Where(slot => slot.Style != WidgetSlotStyle.Ring)
            .Select(slot => slot.Item)
            .ToList();
        var ring = RingOf(layout, slots, facts, now, rules);
        var spark = slots.Any(slot => slot.Style == WidgetSlotStyle.Sparkline)
            ? facts.HourlySpend?.Select(spend => (double)spend).ToList()
            : null;

        var widget = layout.Template switch
        {
            WidgetTemplate.SingleLine or WidgetTemplate.Accounts =>
                Lines(texts, 1, facts, now, rules),
            WidgetTemplate.Minimal => Minimal(texts, facts, now, rules),
            WidgetTemplate.Bars => Bars(texts, facts, now, rules),
            _ => Lines(texts, 2, facts, now, rules),
        };
        return widget with { Ring = ring, Spark = spark };
    }

    /// <summary>Every slot as a block of its own: its value over its label.</summary>
    public static IReadOnlyList<ComposedWidget> Columns(
        WidgetLayout layout,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules) =>
    [
        .. layout.Slots.Take(WidgetLayout.MaximumSlots).Select(slot =>
        {
            var cell = SlotCells.Of(slot.Item, facts, now, rules);
            return new ComposedWidget(
                new ComposedLine(cell.Value, cell.Tone),
                new ComposedLine(cell.Label, LineTone.Muted),
                slot.Style == WidgetSlotStyle.Ring ? RingFor(slot.Item, facts, now, rules) : null,
                Spark: slot.Style == WidgetSlotStyle.Sparkline
                    ? facts.HourlySpend?.Select(spend => (double)spend).ToList()
                    : null);
        }),
    ];

    /// <summary>
    /// With cycling on, a one-line layout shows one text slot at a time, the next every few
    /// seconds, the same answer for everyone at the same moment.
    /// </summary>
    private static List<WidgetSlot> Active(WidgetLayout layout, DateTimeOffset now)
    {
        var slots = layout.Slots.Take(WidgetLayout.MaximumSlots).ToList();
        var oneLine = layout.Template is WidgetTemplate.SingleLine or WidgetTemplate.Minimal;
        var texts = slots.Where(slot => slot.Style != WidgetSlotStyle.Ring).ToList();
        if (!oneLine || layout.CycleSeconds <= 0 || texts.Count < 2)
        {
            return slots;
        }

        var step = now.ToUnixTimeSeconds() / layout.CycleSeconds;
        var shown = texts[(int)(step % texts.Count)];
        return [.. slots.Where(slot => slot.Style == WidgetSlotStyle.Ring), shown];
    }

    private static ComposedWidget Lines(
        List<WidgetItem> texts,
        int count,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules)
    {
        // No text slot means no text: only the ring or line the other slots draw.
        var first = texts.Count > 0 ? texts[0] : WidgetItem.Nothing;
        var second = count > 1 && texts.Count > 1 ? texts[1] : WidgetItem.Nothing;
        return new ComposedWidget(
            WidgetComposer.Line(first, isTop: true, facts, now, rules),
            WidgetComposer.Line(second, isTop: false, facts, now, rules),
            null);
    }

    /// <summary>A ring and one number, with no label, for when space runs out.</summary>
    private static ComposedWidget Minimal(
        List<WidgetItem> texts,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules)
    {
        var item = texts.Count > 0 ? texts[0] : WidgetItem.MostPressingLimit;
        var cell = SlotCells.Of(item, facts, now, rules);
        return new ComposedWidget(
            new ComposedLine(cell.Value, cell.Tone),
            new ComposedLine(string.Empty),
            null);
    }

    private static ComposedWidget Bars(
        List<WidgetItem> texts,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules)
    {
        var plain = Lines(texts, 2, facts, now, rules);
        return plain with
        {
            TopBar = BarFor(texts.ElementAtOrDefault(0), facts, now, rules),
            BottomBar = BarFor(texts.ElementAtOrDefault(1), facts, now, rules),
        };
    }

    private static ComposedBar? BarFor(
        WidgetItem item,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules)
    {
        var cell = SlotCells.Of(item, facts, now, rules);
        return cell.Fraction is { } fraction ? new ComposedBar(fraction, cell.Tone) : null;
    }

    private static ComposedRing? RingOf(
        WidgetLayout layout,
        List<WidgetSlot> slots,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules)
    {
        if (slots.FirstOrDefault(slot => slot.Style == WidgetSlotStyle.Ring) is { } ringSlot)
        {
            return RingFor(ringSlot.Item, facts, now, rules);
        }

        return layout.Template == WidgetTemplate.Minimal
            ? RingFor(WidgetItem.MostPressingLimit, facts, now, rules)
            : null;
    }

    private static ComposedRing? RingFor(
        WidgetItem item,
        WidgetFacts facts,
        DateTimeOffset now,
        ToneRules rules)
    {
        var source = item switch
        {
            WidgetItem.SessionLimit => WidgetRingSource.Session,
            WidgetItem.WeeklyLimit => WidgetRingSource.Weekly,
            WidgetItem.MostPressingLimit => WidgetRingSource.MostPressing,
            WidgetItem.SessionAndWeeklyLimits => WidgetRingSource.SessionAndWeekly,
            _ => WidgetRingSource.Off,
        };
        return WidgetComposer.Ring(source, facts, now, rules);
    }
}
