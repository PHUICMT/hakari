using Hakari.Core.Limits;
using Hakari.Core.Presentation;
using Hakari.Taskbar.Rendering;

namespace Hakari.Feed;

/// <summary>Turns the most pressing limit into the widget's second line.</summary>
internal static class LimitLine
{
    private const string ResetSymbol = "↺";
    private const string LastKnownPrefix = "≈ ";

    public static (string Text, WidgetTone Tone)? From(LimitResult result, DateTimeOffset now)
    {
        if (result.Snapshot is not { } snapshot
            || LimitPriority.MostPressing(snapshot) is not { } limit)
        {
            return null;
        }

        var isLastKnown = snapshot.Freshness == LimitFreshness.LastKnown;
        var prefix = isLastKnown ? LastKnownPrefix : string.Empty;
        var reset = limit.ResetsAt is { } resetsAt
            ? $" · {ResetSymbol} {ResetText.Short(resetsAt, now)}"
            : string.Empty;
        var text = $"{prefix}{LimitNames.Short(limit)} {limit.Percent}%{reset}";
        return (text, Tone(limit, snapshot.Freshness));
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
