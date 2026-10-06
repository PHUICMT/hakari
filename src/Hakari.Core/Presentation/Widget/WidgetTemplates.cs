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
            new(WidgetItem.SessionAndWeeklyLimits, WidgetSlotStyle.Ring),
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

    /// <summary>
    /// How many text slots a template shows at once. Line templates join any more onto their
    /// last line, so only the one-value templates (minimal, accounts) have a limit.
    /// </summary>
    public static int TextLines(WidgetTemplate template) => template switch
    {
        WidgetTemplate.Minimal or WidgetTemplate.Accounts => 1,
        _ => WidgetLayout.MaximumSlots,
    };

    /// <summary>
    /// Which slots show up in the widget. Text slots fill the template's lines in order, unless
    /// one-line layouts cycle through them all. One ring shows. A sparkline slot draws the line
    /// of recent spending and also takes a line for its value, so it shows if either fits. In
    /// columns every slot has a column of its own.
    /// </summary>
    public static IReadOnlyList<bool> Shown(WidgetLayout layout)
    {
        var template = layout.Template;
        var cycles = layout.CycleSeconds > 0 && TextLines(template) == 1
            || layout.CycleSeconds > 0 && template == WidgetTemplate.SingleLine;
        var shown = new List<bool>();
        var (texts, rings, sparks) = (0, 0, 0);
        foreach (var slot in layout.Slots.Take(WidgetLayout.MaximumSlots))
        {
            var lineFits = cycles || texts < TextLines(template);
            shown.Add(template == WidgetTemplate.Columns || slot.Style switch
            {
                WidgetSlotStyle.Ring => rings++ == 0,
                WidgetSlotStyle.Sparkline => sparks++ == 0 | lineFits,
                _ => lineFits,
            });
            if (slot.Style != WidgetSlotStyle.Ring)
            {
                texts++;
            }
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
