using System.Globalization;
using Hakari.Core.Limits;
using Hakari.Taskbar.Rendering;

namespace Hakari.TaskbarSpike;

/// <summary>Turns the most pressing limit into the widget's second line.</summary>
internal static class LimitLine
{
    private const string SessionKind = "session";
    private const string WeeklyAllKind = "weekly_all";
    private const string ResetSymbol = "↺";
    private const string LastKnownPrefix = "≈ ";
    private static readonly TimeSpan CountdownLimit = TimeSpan.FromHours(24);
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static (string Text, WidgetTone Tone)? From(LimitResult result, DateTimeOffset now)
    {
        if (result.Snapshot is not { } snapshot
            || LimitPriority.MostPressing(snapshot) is not { } limit)
        {
            return null;
        }

        var isLastKnown = snapshot.Freshness == LimitFreshness.LastKnown;
        var prefix = isLastKnown ? LastKnownPrefix : string.Empty;
        var text = $"{prefix}{Name(limit)} {limit.Percent}%{Reset(limit, now)}";
        return (text, Tone(limit, snapshot.Freshness));
    }

    private static string Name(UsageLimit limit) => limit.Kind switch
    {
        SessionKind => "5h",
        WeeklyAllKind => "Week",
        _ => limit.ScopeName ?? limit.Kind,
    };

    private static string Reset(UsageLimit limit, DateTimeOffset now)
    {
        if (limit.ResetsAt is not { } resetsAt)
        {
            return string.Empty;
        }

        var remaining = resetsAt - now;
        var when = remaining < CountdownLimit
            ? $"{(int)remaining.TotalHours}:{remaining.Minutes:00}"
            : resetsAt.ToLocalTime().ToString("ddd HH:mm", Culture);
        return $" · {ResetSymbol} {when}";
    }

    private static WidgetTone Tone(UsageLimit limit, LimitFreshness freshness) =>
        LimitPriority.SeverityRank(limit.Severity) switch
        {
            0 when freshness == LimitFreshness.LastKnown => WidgetTone.Muted,
            0 => WidgetTone.Normal,
            1 => WidgetTone.Warning,
            _ => WidgetTone.Critical,
        };
}
