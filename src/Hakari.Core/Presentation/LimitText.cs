using System.Globalization;
using Hakari.Core.Limits;
using Hakari.Core.Localization;

namespace Hakari.Core.Presentation;

/// <summary>One limit in a few characters, for the taskbar.</summary>
public static class LimitText
{
    private const string ResetSymbol = "↺";
    private const string Separator = " · ";
    private const string ClockFormat = "HH:mm";

    /// <summary>
    /// "5h 67% · ↺ 1:48" normally; "5h 82% · full ~18:40" when the current pace fills it
    /// before it resets; "5h full · ↺ 1:48" once it is full, counting down to the reset.
    /// </summary>
    /// <param name="percentText">The share used as written, such as "93.4%".</param>
    public static string Compact(
        UsageLimit limit,
        DateTimeOffset? fullAt,
        DateTimeOffset now,
        string? percentText = null)
    {
        var percent = percentText ?? $"{limit.Percent}%";
        var name = LimitNames.Short(limit);
        var reset = limit.ResetsAt is { } resetsAt
            ? $"{Separator}{ResetSymbol} {ResetText.Short(resetsAt, now)}"
            : string.Empty;

        if (limit.Percent >= LimitForecaster.FullPercent)
        {
            return Texts.Format("limit.full", name) + reset;
        }

        if (fullAt is { } filling)
        {
            var clock = filling.ToLocalTime().ToString(ClockFormat, CultureInfo.InvariantCulture);
            return $"{name} {percent}{Separator}{Texts.Format("limit.fullAt", clock)}";
        }

        return $"{name} {percent}{reset}";
    }
}
