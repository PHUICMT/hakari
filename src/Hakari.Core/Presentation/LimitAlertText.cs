using Hakari.Core.Limits;
using Hakari.Core.Localization;

namespace Hakari.Core.Presentation;

/// <summary>Says a limit alert in words.</summary>
public static class LimitAlertText
{
    private const string Separator = " · ";
    private const double PercentScale = 100;
    private const string TimeFormat = "HH:mm";

    /// <param name="accountName">Shown in the body, or empty with only one account.</param>
    /// <param name="fullAt">When the limit fills at the recent pace; null when unknown.</param>
    /// <param name="roomElsewhere">
    /// Another account with room on the same limit and its percent used, for a limit that is
    /// full or nearly so; null when there is none.
    /// </param>
    public static LimitAlertMessage Compose(
        LimitAlert alert,
        string accountName,
        DateTimeOffset now,
        DateTimeOffset? fullAt = null,
        (string Name, double Percent)? roomElsewhere = null)
    {
        var name = LimitNames.Long(alert.Limit);
        var percent = PercentText.Format(alert.Percent, 0);
        var isFull = alert.Percent >= LimitForecaster.FullPercent;
        var title = alert.Kind switch
        {
            LimitAlertKind.Reset => Texts.Format("alert.reset", name),
            LimitAlertKind.Critical when isFull => Texts.Format("alert.full", name),
            LimitAlertKind.Critical => Texts.Format("alert.critical", name, percent),
            _ => Texts.Format("alert.warning", name, percent),
        };

        var resets = alert.Kind != LimitAlertKind.Reset && alert.Limit.ResetsAt is { } at
            ? Texts.Format("flyout.resets", ResetText.Long(at, now))
            : string.Empty;
        var body = string.Join(
            Separator,
            new[] { accountName, resets }.Where(part => part.Length > 0));
        return new LimitAlertMessage(
            title,
            body,
            Detail(alert, isFull, fullAt, roomElsewhere),
            Math.Clamp(alert.Percent / PercentScale, 0, 1),
            name,
            isFull ? Texts.Get("flyout.full") : percent,
            alert.Kind != LimitAlertKind.Reset);
    }

    /// <summary>
    /// After a reset, where the limit stands now. Before it fills, when it will at this pace
    /// if that comes before the reset. Once full, which other account still has room.
    /// </summary>
    private static string Detail(
        LimitAlert alert,
        bool isFull,
        DateTimeOffset? fullAt,
        (string Name, double Percent)? roomElsewhere)
    {
        if (alert.Kind == LimitAlertKind.Reset)
        {
            return Texts.Format(
                "alert.nowAt",
                LimitNames.Long(alert.Limit),
                PercentText.Format(alert.Percent, 0));
        }

        if (roomElsewhere is { } other && (isFull || alert.Kind == LimitAlertKind.Critical))
        {
            return Texts.Format(
                "alert.roomElsewhere",
                other.Name,
                PercentText.Format(PercentScale - other.Percent, 0));
        }

        var beforeReset = alert.Limit.ResetsAt is not { } resetsAt || fullAt < resetsAt;
        return !isFull && fullAt is { } when && beforeReset
            ? Texts.Format("alert.forecast", when.ToLocalTime().ToString(TimeFormat))
            : string.Empty;
    }
}
