using Hakari.Core.Localization;

namespace Hakari.Core.Presentation.Widget;

/// <summary>
/// When the widget has heard nothing new for a while, such as after the PC slept or while a
/// source is offline, it keeps the last value but marks it with a dot and says how old it is.
/// </summary>
public static class StaleWidget
{
    public const string Mark = "· ";

    public static readonly TimeSpan After = TimeSpan.FromMinutes(10);

    public static bool IsStale(DateTimeOffset lastHeard, DateTimeOffset now) =>
        now - lastHeard >= After;

    /// <summary>The last value with its dot in front; nothing when there was none.</summary>
    public static string Value(string lastValue) =>
        lastValue.Length == 0 ? string.Empty : Mark + lastValue;

    /// <summary>"updated 14 min ago".</summary>
    public static string Age(DateTimeOffset lastHeard, DateTimeOffset now) =>
        Texts.Format("widget.stale", AgeText.Format(now - lastHeard));
}
