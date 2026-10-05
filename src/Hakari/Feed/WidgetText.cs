using System.Globalization;
using Hakari.Core.Currency;
using Hakari.Core.Limits;
using Hakari.Core.Querying;
using Hakari.Taskbar.Rendering;

namespace Hakari.Feed;

/// <summary>Money on top; below, the most pressing limit, else the burn rate.</summary>
internal static class WidgetText
{
    private const decimal WholeUnitsFrom = 1000m;
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static WidgetContent Loading { get; } =
        new("Hakari", "Reading logs…", WidgetTone.Muted);

    public static WidgetContent Paused { get; } =
        new("Hakari", "Paused", WidgetTone.Muted);

    public static WidgetContent Build(UsageQuery query, LimitResult? limits)
    {
        var now = DateTimeOffset.Now;
        var symbol = CurrencySymbols.PrefixFor(query.Currency);
        var today = query.Total(new UsageFilter(From: TimePeriods.StartOfToday(now)));
        var primary = $"{Money(symbol, today.Cost)} today";

        if (limits is not null && LimitLine.From(limits, now) is var (text, tone))
        {
            return new WidgetContent(primary, text, tone);
        }

        var lastHour = query.Total(new UsageFilter(From: now.AddHours(-1)));
        var month = query.Total(new UsageFilter(From: TimePeriods.StartOfMonth(now)));
        var secondary = $"{Money(symbol, lastHour.Cost)}/h · month {Money(symbol, month.Cost)}";
        return new WidgetContent(primary, secondary);
    }

    /// <summary>Cents only while they still matter.</summary>
    private static string Money(string symbol, decimal amount)
    {
        var format = amount >= WholeUnitsFrom ? "N0" : "N2";
        return symbol + amount.ToString(format, Culture);
    }
}
