using Hakari.Core.Presentation;
using Hakari.Core.Querying;
using Hakari.Taskbar.Rendering;

namespace Hakari.Feed;

/// <summary>
/// One account: money on top, its most pressing limit below. Two or more: one line per
/// account, the most pressing on top. No limits known: money and burn rate.
/// </summary>
internal static class WidgetText
{
    private const int MaximumAccountLines = 2;

    public static WidgetContent Loading { get; } =
        new("Hakari", "Reading logs…", WidgetTone.Muted);

    public static WidgetContent Paused { get; } =
        new("Hakari", "Paused", WidgetTone.Muted);

    public static WidgetContent Build(UsageQuery query, LimitPoller limits)
    {
        var now = DateTimeOffset.Now;
        var accounts = limits.Accounts;
        if (accounts.Count >= MaximumAccountLines && AccountLines(limits, now) is { } multiple)
        {
            return multiple;
        }

        var currency = query.Currency;
        var today = query.Total(new UsageFilter(From: TimePeriods.StartOfToday(now)));
        var primary = $"{MoneyText.Format(today.Cost, currency)} today";
        if (accounts.Count > 0
            && LimitLine.From(accounts[0], limits, now, withLabel: false) is var (text, tone))
        {
            return new WidgetContent(primary, text, tone, Ring: LimitLine.Ring(accounts[0], now));
        }

        var lastHour = query.Total(new UsageFilter(From: now.AddHours(-1)));
        var month = query.Total(new UsageFilter(From: TimePeriods.StartOfMonth(now)));
        var secondary = $"{MoneyText.Format(lastHour.Cost, currency)}/h · "
            + $"month {MoneyText.Format(month.Cost, currency)}";
        return new WidgetContent(primary, secondary);
    }

    private static WidgetContent? AccountLines(LimitPoller limits, DateTimeOffset now)
    {
        var top = LimitLine.From(limits.Accounts[0], limits, now, withLabel: true);
        var bottom = LimitLine.From(limits.Accounts[1], limits, now, withLabel: true);
        if (top is not var (topText, topTone) || bottom is not var (bottomText, bottomTone))
        {
            return null;
        }

        return new WidgetContent(
            topText,
            bottomText,
            bottomTone,
            topTone,
            LimitLine.Ring(limits.Accounts[0], now));
    }
}
