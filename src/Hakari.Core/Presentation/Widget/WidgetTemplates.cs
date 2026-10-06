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

    /// <summary>Starts a template over: its slots, and no custom text to override them.</summary>
    public static WidgetLayout Apply(WidgetLayout layout, WidgetTemplate template) => layout with
    {
        Template = template,
        Slots = SlotsOf(template),
        CustomFormat = null,
    };
}
