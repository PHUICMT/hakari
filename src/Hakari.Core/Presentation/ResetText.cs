using System.Globalization;
using Hakari.Core.Localization;

namespace Hakari.Core.Presentation;

/// <summary>A countdown under a day ("1:48"), a weekday and time beyond ("Tue 21:59").</summary>
public static class ResetText
{
    private const string WeekdayAndTime = "ddd HH:mm";
    private const string Clock = "HH:mm";
    private static readonly TimeSpan CountdownLimit = TimeSpan.FromHours(24);

    public static string Short(DateTimeOffset resetsAt, DateTimeOffset now)
    {
        var remaining = resetsAt - now;
        if (remaining < TimeSpan.Zero)
        {
            return Texts.Get("reset.now");
        }

        return remaining < CountdownLimit
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{(int)remaining.TotalHours}:{remaining.Minutes:00}")
            : Weekday(resetsAt);
    }

    /// <summary>For sentences: "in 1 h 48 min · 18:00" or "Tue 21:59".</summary>
    public static string Long(DateTimeOffset resetsAt, DateTimeOffset now)
    {
        var remaining = resetsAt - now;
        if (remaining < TimeSpan.Zero)
        {
            return Texts.Get("reset.now");
        }

        var clock = resetsAt.ToLocalTime().ToString(Clock, CultureInfo.InvariantCulture);
        return remaining < CountdownLimit
            ? Texts.Format("reset.in", (int)remaining.TotalHours, remaining.Minutes, clock)
            : Weekday(resetsAt);
    }

    /// <summary>The weekday in the chosen language; the time stays 24-hour.</summary>
    private static string Weekday(DateTimeOffset resetsAt) =>
        resetsAt.ToLocalTime().ToString(WeekdayAndTime, Texts.Culture);
}
