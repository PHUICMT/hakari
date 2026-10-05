using System.Globalization;

namespace Hakari.Core.Presentation;

/// <summary>A countdown under a day ("1:48"), a weekday and time beyond ("Tue 21:59").</summary>
public static class ResetText
{
    private const string WeekdayAndTime = "ddd HH:mm";
    private static readonly TimeSpan CountdownLimit = TimeSpan.FromHours(24);
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Short(DateTimeOffset resetsAt, DateTimeOffset now)
    {
        var remaining = resetsAt - now;
        if (remaining < TimeSpan.Zero)
        {
            return "now";
        }

        return remaining < CountdownLimit
            ? $"{(int)remaining.TotalHours}:{remaining.Minutes:00}"
            : resetsAt.ToLocalTime().ToString(WeekdayAndTime, Culture);
    }

    /// <summary>For sentences: "in 1 h 48 min · 18:00" or "Tue 21:59".</summary>
    public static string Long(DateTimeOffset resetsAt, DateTimeOffset now)
    {
        var remaining = resetsAt - now;
        var clock = resetsAt.ToLocalTime().ToString("HH:mm", Culture);
        if (remaining < TimeSpan.Zero)
        {
            return "now";
        }

        return remaining < CountdownLimit
            ? $"in {(int)remaining.TotalHours} h {remaining.Minutes} min · {clock}"
            : resetsAt.ToLocalTime().ToString(WeekdayAndTime, Culture);
    }
}
