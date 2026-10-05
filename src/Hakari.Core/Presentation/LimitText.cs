using System.Globalization;
using Hakari.Core.Limits;

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
    public static string Compact(UsageLimit limit, DateTimeOffset? fullAt, DateTimeOffset now)
    {
        var name = LimitNames.Short(limit);
        var reset = limit.ResetsAt is { } resetsAt
            ? $"{Separator}{ResetSymbol} {ResetText.Short(resetsAt, now)}"
            : string.Empty;

        if (limit.Percent >= LimitForecaster.FullPercent)
        {
            return $"{name} full{reset}";
        }

        if (fullAt is { } filling)
        {
            var clock = filling.ToLocalTime().ToString(ClockFormat, CultureInfo.InvariantCulture);
            return $"{name} {limit.Percent}%{Separator}full ~{clock}";
        }

        return $"{name} {limit.Percent}%{reset}";
    }
}
