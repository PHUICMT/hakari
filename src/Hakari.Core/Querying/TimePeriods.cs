namespace Hakari.Core.Querying;

public static class TimePeriods
{
    public const string All = "all";
    public const string Today = "today";
    public const string Week = "week";
    public const string Month = "month";
    public const char DaysSuffix = 'd';
    public const char HoursSuffix = 'h';

    public static DateTimeOffset StartOfToday(DateTimeOffset now) => new(now.Date, now.Offset);

    public static DateTimeOffset StartOfWeek(
        DateTimeOffset now,
        DayOfWeek firstDayOfWeek = DayOfWeek.Monday)
    {
        var daysSinceWeekStart = (7 + (now.DayOfWeek - firstDayOfWeek)) % 7;
        return StartOfToday(now).AddDays(-daysSinceWeekStart);
    }

    public static DateTimeOffset StartOfMonth(DateTimeOffset now) =>
        new(now.Year, now.Month, 1, 0, 0, 0, now.Offset);

    public static UsageFilter ToFilter(string? period, DateTimeOffset now)
    {
        var normalizedPeriod = period?.Trim().ToLowerInvariant() ?? All;

        return normalizedPeriod switch
        {
            "" or All => UsageFilter.Everything,
            Today => new UsageFilter(From: StartOfToday(now)),
            Week => new UsageFilter(From: StartOfWeek(now)),
            Month => new UsageFilter(From: StartOfMonth(now)),
            _ when TryParseCount(normalizedPeriod, DaysSuffix, out var days) =>
                new UsageFilter(From: StartOfToday(now).AddDays(-(days - 1))),
            _ when TryParseCount(normalizedPeriod, HoursSuffix, out var hours) =>
                new UsageFilter(From: now.AddHours(-hours)),
            _ => throw new ArgumentException(
                $"Unknown period '{period}'. Use all, today, week, month, <n>d or <n>h."),
        };
    }

    private static bool TryParseCount(string period, char suffix, out int count)
    {
        count = 0;
        return period.EndsWith(suffix)
            && int.TryParse(period[..^1], out count)
            && count > 0;
    }
}
