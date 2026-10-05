using Hakari.Core.Limits;
using Hakari.Core.Presentation;
using Hakari.Taskbar.Rendering;

namespace Hakari.Feed;

/// <summary>Turns an account's most pressing limit into a widget line and ring.</summary>
internal static class LimitLine
{
    private const string LastKnownPrefix = "≈ ";
    private const double PercentScale = 100.0;

    /// <param name="withLabel">True when several accounts share the widget.</param>
    public static (string Text, WidgetTone Tone)? From(
        AccountLimits account,
        LimitPoller poller,
        DateTimeOffset now,
        bool withLabel)
    {
        var snapshot = account.Snapshot.ProjectedTo(now);
        if (LimitPriority.MostPressing(snapshot) is not { } limit)
        {
            return null;
        }

        var prefix = snapshot.Freshness == LimitFreshness.LastKnown
            ? LastKnownPrefix
            : string.Empty;
        var label = withLabel ? $"{AccountLabels.Short(account.Account)} " : string.Empty;
        var fullAt = poller.FullAt(account.AccountId, limit, now);
        var text = $"{prefix}{label}{LimitText.Compact(limit, fullAt, now)}";
        return (text, Tone(limit, snapshot.Freshness));
    }

    /// <summary>The 5-hour session: the limit that runs out within a working day.</summary>
    public static WidgetRing? Ring(AccountLimits account, DateTimeOffset now)
    {
        var snapshot = account.Snapshot.ProjectedTo(now);
        var limit = snapshot.Limits.FirstOrDefault(
                candidate => candidate.Group == LimitPriority.SessionGroup)
            ?? LimitPriority.MostPressing(snapshot);
        return limit is null
            ? null
            : new WidgetRing(
                Math.Clamp(limit.Percent / PercentScale, 0, 1),
                Tone(limit, snapshot.Freshness));
    }

    private static WidgetTone Tone(UsageLimit limit, LimitFreshness freshness)
    {
        if (limit.Percent >= LimitForecaster.FullPercent)
        {
            return WidgetTone.Critical;
        }

        return LimitPriority.SeverityRank(limit.Severity) switch
        {
            0 when freshness == LimitFreshness.LastKnown => WidgetTone.Muted,
            0 => WidgetTone.Normal,
            1 => WidgetTone.Warning,
            _ => WidgetTone.Critical,
        };
    }
}
