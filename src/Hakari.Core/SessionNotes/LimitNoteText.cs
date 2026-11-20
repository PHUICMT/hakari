using System.Globalization;
using Hakari.Core.Limits;

namespace Hakari.Core.SessionNotes;

/// <summary>
/// What a session is told. It is read by the model, not shown to the user, so it is plain
/// English whatever language Hakari shows, and says what to do, not only the numbers.
/// </summary>
public static class LimitNoteText
{
    private const string SessionKind = "session";
    private const string ScopedKind = "weekly_scoped";
    private const string TimeFormat = "HH:mm";
    private const string DayAndTimeFormat = "ddd HH:mm";
    private const int MinutesPerHour = 60;
    private const int HoursPerDay = 24;

    /// <summary>Readings older than this say when they were taken.</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string NearlyFull(
        IReadOnlyList<UsageLimit> limits,
        DateTimeOffset fetchedAt,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        var parts = string.Join("; ", limits.Select(limit => Describe(limit, now, zone)));
        var anyFull = limits.Any(limit => limit.Percent >= NoteLevel.Full);
        var advice = anyFull
            ? "Requests can be refused until the reset. Save progress now (commit, or write "
                + "down what is done and what is next) and stop at a safe point; the work "
                + "can carry on after the reset."
            : "When a limit reaches 100%, requests are refused until it resets. Before "
                + "starting long work, save progress (commit, or write down what is done "
                + "and what is next) so the work can carry on after the reset.";
        return $"Hakari usage note for this Claude account: {parts}.{Taken(fetchedAt, now, zone)}"
            + $" {advice}";
    }

    public static string Reset(IReadOnlyList<UsageLimit> limits) =>
        "Hakari usage note: "
        + string.Join("; ", limits.Select(limit =>
            $"the {Name(limit)} has reset and is at {limit.Percent}%"))
        + ". Work that was paused for it can carry on.";

    private static string Describe(UsageLimit limit, DateTimeOffset now, TimeZoneInfo zone)
    {
        var state = limit.Percent >= NoteLevel.Full ? "is full" : $"is at {limit.Percent}%";
        var reset = limit.ResetsAt is { } resetsAt
            ? $", resets {At(resetsAt, now, zone)} local time (in {Span(resetsAt - now)})"
            : string.Empty;
        return $"the {Name(limit)} {state}{reset}";
    }

    private static string Name(UsageLimit limit) => limit.Kind switch
    {
        SessionKind => "5-hour limit",
        ScopedKind when limit.ScopeName is { Length: > 0 } scope => $"weekly {scope} limit",
        _ => "weekly limit",
    };

    private static string Taken(DateTimeOffset fetchedAt, DateTimeOffset now, TimeZoneInfo zone) =>
        now - fetchedAt > StaleAfter
            ? $" (read at {At(fetchedAt, now, zone)}; it may be higher now.)"
            : string.Empty;

    private static string At(DateTimeOffset moment, DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(moment, zone);
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        return local.ToString(local.Date == today ? TimeFormat : DayAndTimeFormat, Invariant);
    }

    private static string Span(TimeSpan span)
    {
        var minutes = Math.Max(0, (int)Math.Ceiling(span.TotalMinutes));
        var hours = minutes / MinutesPerHour;
        return hours >= HoursPerDay
            ? $"{hours / HoursPerDay} d {hours % HoursPerDay} h"
            : hours > 0
                ? $"{hours} h {minutes % MinutesPerHour} min"
                : $"{minutes} min";
    }
}
