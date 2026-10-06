namespace Hakari.Core.Presentation.Widget;

/// <summary>The slots each template starts with, which the user then changes.</summary>
public static class WidgetTemplates
{
    public static IReadOnlyList<WidgetTemplate> All { get; } =
    [
        WidgetTemplate.SingleLine,
        WidgetTemplate.TwoLines,
        WidgetTemplate.Columns,
        WidgetTemplate.Bars,
        WidgetTemplate.RingText,
        WidgetTemplate.Sparkline,
        WidgetTemplate.Accounts,
        WidgetTemplate.Minimal,
    ];

    public static IReadOnlyList<WidgetSlot> SlotsOf(WidgetTemplate template) => template switch
    {
        WidgetTemplate.SingleLine => [new(WidgetItem.CostToday)],
        WidgetTemplate.Columns =>
        [
            new(WidgetItem.CostToday),
            new(WidgetItem.BurnRate),
            new(WidgetItem.SessionLimit),
            new(WidgetItem.WeeklyLimit),
        ],
        WidgetTemplate.Bars => [new(WidgetItem.SessionLimit), new(WidgetItem.WeeklyLimit)],
        WidgetTemplate.RingText =>
        [
            new(WidgetItem.MostPressingLimit, WidgetSlotStyle.Ring),
            new(WidgetItem.CostToday),
            new(WidgetItem.BurnRate),
        ],
        WidgetTemplate.Sparkline =>
        [
            new(WidgetItem.BurnRate, WidgetSlotStyle.Sparkline),
            new(WidgetItem.CostToday),
            new(WidgetItem.MostPressingLimit),
        ],
        WidgetTemplate.Accounts => [new(WidgetItem.MostPressingLimit)],
        WidgetTemplate.Minimal => [new(WidgetItem.MostPressingLimit)],
        _ => [new(WidgetItem.CostToday), new(WidgetItem.MostPressingLimit)],
    };

    /// <summary>How many text slots a template shows at once; the rest wait their turn.</summary>
    public static int TextLines(WidgetTemplate template) => template switch
    {
        WidgetTemplate.SingleLine or WidgetTemplate.Minimal or WidgetTemplate.Accounts => 1,
        WidgetTemplate.Columns => WidgetLayout.MaximumSlots,
        _ => 2,
    };

    /// <summary>
    /// Which slots show up in the widget. Text slots fill the template's lines in order, unless
    /// one-line layouts cycle through them all; one ring shows, and a sparkline only where no
    /// ring takes its place. In columns every slot has a column of its own.
    /// </summary>
    public static IReadOnlyList<bool> Shown(WidgetLayout layout)
    {
        var template = layout.Template;
        var cycles = layout.CycleSeconds > 0 && TextLines(template) == 1;
        var hasRing = template == WidgetTemplate.Minimal
            || layout.Slots.Any(slot => slot.Style == WidgetSlotStyle.Ring);
        var shown = new List<bool>();
        var (texts, rings, sparks) = (0, 0, 0);
        foreach (var slot in layout.Slots.Take(WidgetLayout.MaximumSlots))
        {
            shown.Add(template == WidgetTemplate.Columns || slot.Style switch
            {
                WidgetSlotStyle.Ring => rings++ == 0,
                WidgetSlotStyle.Sparkline => !hasRing && sparks++ == 0,
                _ => cycles || texts++ < TextLines(template),
            });
        }

        return shown;
    }

    /// <summary>Starts a template over: its slots, and no custom text to override them.</summary>
    public static WidgetLayout Apply(WidgetLayout layout, WidgetTemplate template) => layout with
    {
        Template = template,
        Slots = SlotsOf(template),
        CustomFormat = null,
    };
}
