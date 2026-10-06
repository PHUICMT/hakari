using Hakari.Core.Localization;
using Hakari.Core.Presentation.Widget;

namespace Hakari.Surfaces.Settings;

/// <summary>What a line or a slot of the widget can show, as the choices a list offers.</summary>
internal static class WidgetItemChoices
{
    public static readonly (WidgetItem Value, string TextKey)[] Keys =
    [
        (WidgetItem.Automatic, "settings.item.automatic"),
        (WidgetItem.CostToday, "settings.item.costToday"),
        (WidgetItem.CostThisMonth, "settings.item.costMonth"),
        (WidgetItem.BurnRate, "settings.item.burnRate"),
        (WidgetItem.TokensToday, "settings.item.tokensToday"),
        (WidgetItem.TokensThisMonth, "settings.item.tokensMonth"),
        (WidgetItem.RepliesToday, "settings.item.repliesToday"),
        (WidgetItem.SessionLimit, "settings.item.session"),
        (WidgetItem.WeeklyLimit, "settings.item.weekly"),
        (WidgetItem.SessionAndWeeklyLimits, "settings.item.both"),
        (WidgetItem.MostPressingLimit, "settings.item.pressing"),
        (WidgetItem.SecondAccount, "settings.item.secondAccount"),
        (WidgetItem.Nothing, "settings.item.nothing"),
    ];

    /// <summary>The metrics a slot can show: every item but the automatic and empty ones.</summary>
    public static IEnumerable<(object Value, string Text)> ForSlots() =>
        Keys.Where(choice => choice.Value is not (WidgetItem.Automatic or WidgetItem.Nothing))
            .Select(choice => ((object)choice.Value, Texts.Get(choice.TextKey)));

    public static bool IsLimit(WidgetItem item) => item is WidgetItem.SessionLimit
        or WidgetItem.WeeklyLimit
        or WidgetItem.MostPressingLimit
        or WidgetItem.SessionAndWeeklyLimits;

    public static bool IsSpending(WidgetItem item) => item is WidgetItem.CostToday
        or WidgetItem.CostThisMonth
        or WidgetItem.BurnRate;

    /// <summary>
    /// The metrics that suit a style: a ring only shows a limit and a sparkline only spending;
    /// text shows anything.
    /// </summary>
    public static IEnumerable<(object Value, string Text)> ForSlotStyle(WidgetSlotStyle style) =>
        ForSlots().Where(choice => style switch
        {
            WidgetSlotStyle.Ring => IsLimit((WidgetItem)choice.Value),
            WidgetSlotStyle.Sparkline => IsSpending((WidgetItem)choice.Value),
            _ => true,
        });

    /// <summary>A ring needs a limit and a sparkline needs spending; text suits anything.</summary>
    public static IEnumerable<(object Value, string Text)> StylesFor(WidgetItem item)
    {
        yield return (WidgetSlotStyle.Text, Texts.Get("settings.slotStyle.text"));
        if (IsLimit(item))
        {
            yield return (WidgetSlotStyle.Ring, Texts.Get("settings.slotStyle.ring"));
        }

        if (IsSpending(item))
        {
            yield return (WidgetSlotStyle.Sparkline, Texts.Get("settings.slotStyle.sparkline"));
        }
    }
}
