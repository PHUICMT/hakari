using System.Globalization;
using System.Text.RegularExpressions;
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

    /// <summary>What sessions are told unless the user writes their own.</summary>
    public const string DefaultTemplate =
        "Hakari usage note for this Claude account: {limits}.{readAt} {advice}";

    /// <summary>A note longer than this is cut, well below what Claude Code accepts.</summary>
    public const int MaximumLength = 2000;

    /// <summary>The names a template can use in braces, in the order Settings lists them.</summary>
    public static readonly string[] Placeholders =
        ["limits", "limit", "percent", "state", "resets", "in", "advice", "readAt"];

    private static readonly Regex Placeholder = new(@"\{(\w+)\}", RegexOptions.Compiled);

    /// <param name="template">The user's text with {placeholders}; empty for the default.</param>
    public static string NearlyFull(
        IReadOnlyList<UsageLimit> limits,
        DateTimeOffset fetchedAt,
        DateTimeOffset now,
        TimeZoneInfo zone,
        string? template = null)
    {
        var values = Values(limits, fetchedAt, now, zone);
        var text = Placeholder.Replace(
            string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template,
            match => values.TryGetValue(match.Groups[1].Value, out var value)
                ? value
                : match.Value);
        return text.Length > MaximumLength ? text[..MaximumLength] : text;
    }

    /// <summary>
    /// The note as a session would get it now, with a made-up limit read a while ago, so
    /// {readAt}, which only shows for old readings, can be seen too.
    /// </summary>
    public static string Sample(string? template, DateTimeOffset now, TimeZoneInfo zone) =>
        NearlyFull(
        [
            new UsageLimit(SessionKind, SessionKind, 92, "warning",
                now + SampleResetIn, null, IsActive: true),
        ],
            now - SampleReadAgo,
            now,
            zone,
            template);

    private static readonly TimeSpan SampleResetIn = TimeSpan.FromMinutes(95);

    /// <summary>Older than <see cref="StaleAfter"/>, so the sample says when it was read.</summary>
    public static readonly TimeSpan SampleReadAgo = TimeSpan.FromMinutes(20);

    /// <summary>
    /// What each placeholder stands for. The single-limit ones describe the fullest limit, as
    /// that is the one that stops work first.
    /// </summary>
    private static Dictionary<string, string> Values(
        IReadOnlyList<UsageLimit> limits,
        DateTimeOffset fetchedAt,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        var fullest = limits.MaxBy(limit => limit.Percent)!;
        var anyFull = fullest.Percent >= NoteLevel.Full;
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["limits"] = string.Join("; ", limits.Select(limit => Describe(limit, now, zone))),
            ["limit"] = Name(fullest),
            ["percent"] = $"{fullest.Percent}%",
            ["state"] = anyFull ? "full" : "nearly full",
            ["resets"] = fullest.ResetsAt is { } at ? At(at, now, zone) : string.Empty,
            ["in"] = fullest.ResetsAt is { } until ? Span(until - now) : string.Empty,
            ["advice"] = anyFull ? FullAdvice : NearlyFullAdvice,
            ["readAt"] = Taken(fetchedAt, now, zone),
        };
    }

    private const string FullAdvice =
        "Requests can be refused until the reset. Save progress now (commit, or write "
        + "down what is done and what is next) and stop at a safe point; the work "
        + "can carry on after the reset.";

    private const string NearlyFullAdvice =
        "When a limit reaches 100%, requests are refused until it resets. Before "
        + "starting long work, save progress (commit, or write down what is done "
        + "and what is next) so the work can carry on after the reset.";

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
