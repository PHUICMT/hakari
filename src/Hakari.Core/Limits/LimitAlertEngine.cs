namespace Hakari.Core.Limits;

/// <summary>
/// Decides when a reading deserves a notification. Each threshold fires once as a limit rises
/// through it, and fires again only after the limit has dropped well below it, so a reading
/// hovering around a line stays quiet. Starting out above a threshold fires nothing. A reset
/// is told only when the limit had been well used and then fell clearly: a drop to zero from
/// 5% is not news.
/// </summary>
public sealed class LimitAlertEngine(int warnAt, int criticalAt)
{
    /// <summary>How far below a threshold a limit must fall before it can fire again.</summary>
    public const double RearmMargin = 5;

    /// <summary>The share a limit must have reached for its reset to be worth telling.</summary>
    public const double WorthTellingResetFrom = 75;

    /// <summary>A fall smaller than this between two readings is not a reset.</summary>
    public const double ResetDrop = 5;

    /// <summary>A window that started over is near empty; a fall to more is not a reset.</summary>
    public const double ResetBelow = 25;

    private readonly Dictionary<(string AccountId, string Kind), Track> tracks = [];

    public IReadOnlyList<LimitAlert> Observe(string accountId, UsageLimit limit, double percent)
    {
        var key = (accountId, limit.Kind);
        if (!tracks.TryGetValue(key, out var track))
        {
            tracks[key] = new Track(percent, percent < warnAt, percent < criticalAt, percent);
            return [];
        }

        var alerts = new List<LimitAlert>();
        var warnArmed = track.WarnArmed;
        var criticalArmed = track.CriticalArmed;
        var peak = track.Peak;

        var fell = track.Last - percent >= ResetDrop && percent <= ResetBelow;
        if (fell && peak >= WorthTellingResetFrom)
        {
            alerts.Add(new LimitAlert(accountId, limit, LimitAlertKind.Reset, percent));
            peak = percent;
        }

        if (percent >= criticalAt && criticalArmed)
        {
            alerts.Add(new LimitAlert(accountId, limit, LimitAlertKind.Critical, percent));
            criticalArmed = false;
            warnArmed = false;
        }
        else if (percent >= warnAt && warnArmed)
        {
            alerts.Add(new LimitAlert(accountId, limit, LimitAlertKind.Warning, percent));
            warnArmed = false;
        }

        warnArmed |= percent < warnAt - RearmMargin;
        criticalArmed |= percent < criticalAt - RearmMargin;
        tracks[key] = new Track(percent, warnArmed, criticalArmed, Math.Max(peak, percent));
        return alerts;
    }

    private sealed record Track(double Last, bool WarnArmed, bool CriticalArmed, double Peak);
}
