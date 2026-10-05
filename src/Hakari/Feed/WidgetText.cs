using Hakari.Core.Limits;
using Hakari.Core.Presentation;
using Hakari.Core.Querying;
using Hakari.Taskbar.Rendering;

namespace Hakari.Feed;

/// <summary>Money on top; below, the most pressing limit, else the burn rate.</summary>
internal static class WidgetText
{
    public static WidgetContent Loading { get; } =
        new("Hakari", "Reading logs…", WidgetTone.Muted);

    public static WidgetContent Paused { get; } =
        new("Hakari", "Paused", WidgetTone.Muted);

    public static WidgetContent Build(UsageQuery query, LimitResult? limits)
    {
        var now = DateTimeOffset.Now;
        var currency = query.Currency;
        var today = query.Total(new UsageFilter(From: TimePeriods.StartOfToday(now)));
        var primary = $"{MoneyText.Format(today.Cost, currency)} today";

        if (limits is not null && LimitLine.From(limits, now) is var (text, tone))
        {
            return new WidgetContent(primary, text, tone);
        }

        var lastHour = query.Total(new UsageFilter(From: now.AddHours(-1)));
        var month = query.Total(new UsageFilter(From: TimePeriods.StartOfMonth(now)));
        var secondary = $"{MoneyText.Format(lastHour.Cost, currency)}/h · "
            + $"month {MoneyText.Format(month.Cost, currency)}";
        return new WidgetContent(primary, secondary);
    }
}
